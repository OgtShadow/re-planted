using ClientServer.Contracts;
using Microsoft.Extensions.Options;

namespace ClientServer.Services;

public sealed class IoTControllerBackgroundService : BackgroundService
{
    private readonly IMainServerTopologyClient _topologyClient;
    private readonly IMockDeviceClient _mockDeviceClient;
    private readonly IMqttBridgeService _mqttBridgeService;
    private readonly IParameterAutomationEngine _automationEngine;
    private readonly IPumpSafetyGuard _pumpSafetyGuard;
    private readonly IControllerStateStore _stateStore;
    private readonly IControllerTelemetryPublisher _telemetryPublisher;
    private readonly IoTControllerOptions _options;
    private readonly OfflineModeOptions _offlineOptions;
    private readonly ILogger<IoTControllerBackgroundService> _logger;
    private readonly Dictionary<string, DateTime> _lastActuatorActionUtc = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTime> _lastRangeAlertUtc = new(StringComparer.OrdinalIgnoreCase);
    private bool _reportedEmptyClientSet;

    public IoTControllerBackgroundService(
        IMainServerTopologyClient topologyClient,
        IMockDeviceClient mockDeviceClient,
        IMqttBridgeService mqttBridgeService,
        IParameterAutomationEngine automationEngine,
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
        _automationEngine = automationEngine;
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
            var existingConfiguration = _stateStore.GetConfiguration(clientId);
            if (existingConfiguration is not null)
            {
                downloadedConfiguration = downloadedConfiguration with
                {
                    Topology = RetainLatestWateringTimestamps(downloadedConfiguration.Topology, existingConfiguration.Topology)
                };
            }

            _stateStore.UpdateConfiguration(downloadedConfiguration);
        }

        var configuration = _stateStore.GetConfiguration(clientId);
        var currentTopology = configuration?.Topology ?? _stateStore.GetTopology(clientId);
        if (currentTopology is null || currentTopology.Plants.Count == 0)
        {
            return;
        }

        var nowUtc = DateTime.UtcNow;
        var nowLocal = DateTime.Now;
        var configurationExpired = configuration is null || configuration.ExpiresAtUtc <= nowUtc;
        var automationAllowed = configuration is not null &&
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

        if (configurationExpired)
        {
            warningMessage ??= "Konfiguracja automatyzacji wygasła. Sterowanie automatyczne jest w trybie bezpiecznej degradacji.";
        }
        if (automationAllowed)
        {
            var evaluation = _automationEngine.Evaluate(
                currentTopology,
                telemetry,
                nowUtc,
                nowLocal,
                _stateStore.GetWateringSkippedDates(),
                _options.PumpRunSeconds,
                _options.MaxPumpRunSeconds);
            var localDate = DateOnly.FromDateTime(nowLocal);
            foreach (var skippedPlantId in evaluation.WateringSkippedPlantIds)
            {
                _stateStore.MarkWateringSkipped(skippedPlantId, localDate);
            }

            var activeActionPrefixes = evaluation.OutOfRangeKeys
                .Select(key => $"{clientId}:{key}:")
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var staleKey in _lastActuatorActionUtc.Keys
                         .Where(key => key.StartsWith($"{clientId}:", StringComparison.OrdinalIgnoreCase) &&
                             !activeActionPrefixes.Any(prefix => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                         .ToList())
            {
                _lastActuatorActionUtc.Remove(staleKey);
            }

            foreach (var issue in evaluation.UnhandledRanges)
            {
                var alertKey = $"{clientId}:{issue.PlantId}:{issue.Parameter}";
                if (_lastRangeAlertUtc.TryGetValue(alertKey, out var lastAlertUtc) && nowUtc - lastAlertUtc < TimeSpan.FromMinutes(1))
                {
                    continue;
                }

                await _topologyClient.ReportParameterRangeIssueAsync(clientId, issue, cancellationToken);
                _lastRangeAlertUtc[alertKey] = nowUtc;
            }

            foreach (var action in evaluation.Actions)
            {
                var actionKey = $"{clientId}:{action.PlantId}:{action.Parameter}:{action.ActuatorDeviceId}";
                if (_lastActuatorActionUtc.TryGetValue(actionKey, out var lastActionUtc) && nowUtc - lastActionUtc < TimeSpan.FromSeconds(Math.Max(30, _options.PollingIntervalSeconds * 2)))
                {
                    continue;
                }

                var isPumpAction = action.State && string.Equals(action.Command, "pump", StringComparison.OrdinalIgnoreCase);
                var isSoilWateringAction = isPumpAction && string.Equals(action.Parameter, "soilMoisture", StringComparison.OrdinalIgnoreCase);
                if (isPumpAction && pumpStateMachine.IsInSoak(nowUtc))
                {
                    continue;
                }

                var durationMs = Math.Max(0, action.DurationSeconds) * 1000;
                if (isPumpAction)
                {
                    var safety = _pumpSafetyGuard.ValidateStart(clientId, durationMs, telemetry);
                    if (!safety.Allowed)
                    {
                        warningMessage = safety.RejectionReason;
                        _logger.LogWarning("Zablokowano automatyczne uruchomienie pompy dla rośliny {PlantName}: {Reason}", action.PlantName, safety.RejectionReason);
                        pumpStateMachine.MarkBlocked(safety.RejectionReason ?? "Pompa zablokowana przez zabezpieczenie.");
                        continue;
                    }

                    durationMs = safety.DurationMs;
                }

                var published = await _mqttBridgeService.PublishActuatorCommandAsync(
                    action.ActuatorExternalDeviceId,
                    action.Command,
                    action.State,
                    durationMs,
                    cancellationToken);

                if (!published)
                {
                    _logger.LogWarning("Nie udało się sterować parametrem {Parameter} aktuatora {ActuatorId} dla rośliny {PlantName}.", action.Parameter, action.ActuatorExternalDeviceId, action.PlantName);
                    continue;
                }

                _lastActuatorActionUtc[actionKey] = nowUtc;
                activePlantName = action.PlantName;
                _logger.LogInformation(
                    "Skorygowano parametr {Parameter} rośliny {PlantName} komendą {Command} na urządzeniu {ActuatorId}.",
                    action.Parameter,
                    action.PlantName,
                    action.Command,
                    action.ActuatorExternalDeviceId);

                if (isSoilWateringAction)
                {
                    var wateredTopology = currentTopology with
                    {
                        Plants = currentTopology.Plants
                            .Select(plant => plant.Id == action.PlantId ? plant with { LastWateredUtc = nowUtc } : plant)
                            .ToList()
                    };
                    currentTopology = wateredTopology;
                    if (configuration is not null)
                    {
                        configuration = configuration with { Topology = wateredTopology };
                        _stateStore.UpdateConfiguration(configuration);
                    }
                    else
                    {
                        _stateStore.UpdateTopology(clientId, wateredTopology);
                    }

                    await _topologyClient.MarkPlantWateredAsync(clientId, action.PlantId, cancellationToken);
                    pumpStateMachine.BeginWatering(action.PlantName);
                    pumpStateMachine.BeginSoak(nowUtc, TimeSpan.FromSeconds(Math.Clamp(_options.SoakTimeSeconds, 10, 600)));
                    break;
                }
                if (!action.State && string.Equals(action.Command, "pump", StringComparison.OrdinalIgnoreCase))
                {
                    pumpStateMachine.MarkIdle();
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
                DateTime.UtcNow,
                GetAvailableSensorFields(payload));
    }

            private static IReadOnlyList<string> GetAvailableSensorFields(TelemetryPayload payload)
            {
            var fields = new List<string>();
            if (payload.SoilMoisture.HasValue) fields.Add("soilMoistureAnalog");
            if (payload.LightLevel.HasValue) fields.Add("lightIsDark");
            if (payload.Temperature.HasValue) fields.Add("temperature");
            if (payload.Humidity.HasValue) fields.Add("humidity");
            if (payload.WaterLevel.HasValue) fields.Add("waterLevelCm");
            return fields;
            }

        private static ControllerTopologyDto RetainLatestWateringTimestamps(
            ControllerTopologyDto downloadedTopology,
            ControllerTopologyDto existingTopology)
        {
            var existingWateringTimes = existingTopology.Plants.ToDictionary(plant => plant.Id, plant => plant.LastWateredUtc);
            var plants = downloadedTopology.Plants
                .Select(plant =>
                {
                    if (!existingWateringTimes.TryGetValue(plant.Id, out var existingWateredUtc) ||
                        existingWateredUtc is null ||
                        plant.LastWateredUtc >= existingWateredUtc)
                    {
                        return plant;
                    }

                    return plant with { LastWateredUtc = existingWateredUtc };
                })
                .ToList();

            return downloadedTopology with { Plants = plants };
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