using ClientServer.Contracts;
using Microsoft.Extensions.Options;

namespace ClientServer.Services;

public sealed class IoTControllerBackgroundService : BackgroundService
{
    private readonly IMainServerTopologyClient _topologyClient;
    private readonly IMockDeviceClient _mockDeviceClient;
    private readonly IMqttBridgeService _mqttBridgeService;
    private readonly IAutomationRuleEngine _ruleEngine;
    private readonly IPumpSafetyGuard _pumpSafetyGuard;
    private readonly IControllerStateStore _stateStore;
    private readonly IControllerTelemetryPublisher _telemetryPublisher;
    private readonly IoTControllerOptions _options;
    private readonly OfflineModeOptions _offlineOptions;
    private readonly ILogger<IoTControllerBackgroundService> _logger;
    private bool _reportedEmptyClientSet;

    public IoTControllerBackgroundService(
        IMainServerTopologyClient topologyClient,
        IMockDeviceClient mockDeviceClient,
        IMqttBridgeService mqttBridgeService,
        IAutomationRuleEngine ruleEngine,
        IPumpSafetyGuard pumpSafetyGuard,
        IControllerStateStore stateStore,
        IControllerTelemetryPublisher telemetryPublisher,
        IOptions<IoTControllerOptions> options,
        IOptions<OfflineModeOptions> offlineOptions,
        ILogger<IoTControllerBackgroundService> logger)
    {
        _topologyClient = topologyClient;
        _mockDeviceClient = mockDeviceClient;
        _mqttBridgeService = mqttBridgeService;
        _ruleEngine = ruleEngine;
        _pumpSafetyGuard = pumpSafetyGuard;
        _stateStore = stateStore;
        _telemetryPublisher = telemetryPublisher;
        _options = options.Value;
        _offlineOptions = offlineOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunCycleAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Nie udało się wykonać cyklu sterowania IoT.");
            }

            var delaySeconds = Math.Clamp(_options.PollingIntervalSeconds, 5, 300);
            await Task.Delay(TimeSpan.FromSeconds(delaySeconds), stoppingToken);
        }
    }

    private async Task RunCycleAsync(CancellationToken cancellationToken)
    {
        var clientIds = ResolveClientIds();
        if (clientIds.Count == 0)
        {
            if (!_reportedEmptyClientSet)
            {
                _logger.LogWarning("Brak skonfigurowanych identyfikatorów klientów. Ustaw IoTController:ClientIds.");
                _reportedEmptyClientSet = true;
            }

            return;
        }

        _reportedEmptyClientSet = false;
        foreach (var clientId in clientIds)
        {
            await RunCycleForClientAsync(clientId, cancellationToken);
        }
    }

    private async Task RunCycleForClientAsync(int clientId, CancellationToken cancellationToken)
    {
        var downloadedConfiguration = await _topologyClient.GetConfigurationAsync(clientId, cancellationToken);
        if (downloadedConfiguration is not null)
        {
            _stateStore.UpdateConfiguration(downloadedConfiguration);
        }

        var configuration = _stateStore.GetConfiguration(clientId);
        var currentTopology = configuration?.Topology ?? _stateStore.GetTopology(clientId);
        if (currentTopology is null || currentTopology.Plants.Count == 0)
        {
            return;
        }

        var nowUtc = DateTime.UtcNow;
        var configurationExpired = configuration is null || configuration.ExpiresAtUtc <= nowUtc;
        var offlineAutomationAllowed = configuration is not null &&
            (!configurationExpired || (_offlineOptions.Enabled && _offlineOptions.ContinueWithExpiredSnapshot));

        var pumpStateMachine = _stateStore.GetPumpStateMachine(clientId);
        pumpStateMachine.Refresh(nowUtc);

        using var telemetryTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        telemetryTimeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(_options.TelemetryTimeoutSeconds, 1, 60)));
        ControllerTelemetryDto? telemetry;
        try
        {
            if (string.Equals(_options.TelemetrySource, "Mqtt", StringComparison.OrdinalIgnoreCase))
            {
                telemetry = ReadMqttTelemetry(currentTopology, clientId, pumpStateMachine);
            }
            else
            {
                telemetry = await _mockDeviceClient.ReadTelemetryAsync(
                    currentTopology.ClientId,
                    pumpStateMachine.Phase,
                    null,
                    pumpStateMachine.WarningMessage,
                    pumpStateMachine.SoakUntilUtc,
                    telemetryTimeout.Token);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Odczyt telemetrii przekroczył limit czasu. Pomijam cykl sterowania.");
            return;
        }

        if (telemetry is null)
        {
            return;
        }

        string? activePlantName = null;
        var warningMessage = pumpStateMachine.WarningMessage;

        var rules = offlineAutomationAllowed ? configuration!.Rules : [];
        if (configurationExpired)
        {
            warningMessage ??= "Konfiguracja automatyzacji wygasła. Sterowanie automatyczne jest w trybie bezpiecznej degradacji.";
        }
        if (rules.Count > 0 && !pumpStateMachine.IsInSoak(nowUtc))
        {
            var decisions = _ruleEngine.Evaluate(rules, telemetry, nowUtc);
            foreach (var decision in decisions)
            {
                var isPumpAction = decision.State && string.Equals(decision.Command, "pump", StringComparison.OrdinalIgnoreCase);
                var durationMs = Math.Max(1, decision.DurationSeconds) * 1000;
                if (isPumpAction)
                {
                    var safety = _pumpSafetyGuard.ValidateStart(clientId, durationMs, telemetry);
                    if (!safety.Allowed)
                    {
                        warningMessage = safety.RejectionReason;
                        _logger.LogWarning("Zablokowano automatyczne uruchomienie pompy dla reguły {RuleId}: {Reason}", decision.Rule.Id, safety.RejectionReason);
                        pumpStateMachine.MarkBlocked(safety.RejectionReason ?? "Pompa zablokowana przez zabezpieczenie.");
                        continue;
                    }

                    durationMs = safety.DurationMs;
                }

                var published = await _mqttBridgeService.PublishActuatorCommandAsync(
                    decision.ActuatorExternalDeviceId,
                    decision.Command,
                    decision.State,
                    durationMs,
                    cancellationToken);

                if (!published)
                {
                    _logger.LogWarning("Nie udało się wykonać reguły {RuleId} dla rośliny {PlantName}.", decision.Rule.Id, decision.Rule.PlantName);
                    continue;
                }

                await _topologyClient.NotifyRuleTriggeredAsync(clientId, decision.Rule.Id, cancellationToken);
                activePlantName = decision.Rule.PlantName;
                _logger.LogInformation(
                    "Wykonano regułę {RuleId}: {Command} na urządzeniu {ActuatorId} dla rośliny {PlantName}.",
                    decision.Rule.Id,
                    decision.Command,
                    decision.ActuatorExternalDeviceId,
                    decision.Rule.PlantName);

                if (isPumpAction)
                {
                    pumpStateMachine.BeginWatering(decision.Rule.PlantName);
                    pumpStateMachine.BeginSoak(nowUtc, TimeSpan.FromSeconds(Math.Clamp(_options.SoakTimeSeconds, 10, 600)));
                    break;
                }
            }
        }

        var enrichedTelemetry = telemetry with
        {
            ControllerState = pumpStateMachine.Phase.ToString(),
            ActivePlantName = activePlantName,
            WarningMessage = warningMessage,
            LastSyncUtc = currentTopology.SyncedAtUtc
        };

        _stateStore.UpdateTelemetry(clientId, enrichedTelemetry);
        await PublishTelemetryAsync(currentTopology.ClientId, enrichedTelemetry, cancellationToken);
    }

    private ControllerTelemetryDto? ReadMqttTelemetry(
        ControllerTopologyDto topology,
        int clientId,
        PumpControlStateMachine pumpStateMachine)
    {
        if (!_mqttBridgeService.TryGetLatestTelemetryForClient(topology, out var payload) || payload is null)
        {
            _logger.LogWarning("Brak telemetrii MQTT dla sensorów klienta {ClientId}.", clientId);
            return null;
        }

        return new ControllerTelemetryDto(
            payload.DeviceId,
            payload.SoilMoisture ?? 0,
            payload.Temperature ?? 0,
            payload.Humidity ?? 0,
            payload.WaterLevel ?? 0,
            payload.PumpState ?? false,
            payload.LampState ?? false,
            payload.TimestampUtc,
            clientId,
            pumpStateMachine.Phase.ToString(),
            null,
            pumpStateMachine.WarningMessage,
            DateTime.UtcNow);
    }

    private IReadOnlyList<int> ResolveClientIds()
    {
        return _options.ClientIds
            .Where(id => id > 0)
            .Distinct()
            .ToList();
    }

    private async Task PublishTelemetryAsync(int clientId, ControllerTelemetryDto telemetry, CancellationToken cancellationToken)
    {
        await _telemetryPublisher.PublishAsync(telemetry, cancellationToken);
        _logger.LogInformation("Zaktualizowano telemetrię dla klienta {ClientId}.", clientId);
    }
}