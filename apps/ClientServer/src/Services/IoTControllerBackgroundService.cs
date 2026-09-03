using ClientServer.Contracts;
using ClientServer.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace ClientServer.Services;

public sealed class IoTControllerBackgroundService : BackgroundService
{
    private readonly IMainServerTopologyClient _topologyClient;
    private readonly IMockDeviceClient _mockDeviceClient;
    private readonly IMqttBridgeService _mqttBridgeService;
    private readonly IAutomationRuleEngine _ruleEngine;
    private readonly IControllerStateStore _stateStore;
    private readonly IHubContext<ControllerHub> _hubContext;
    private readonly IoTControllerOptions _options;
    private readonly ILogger<IoTControllerBackgroundService> _logger;
    private bool _reportedEmptyClientSet;

    public IoTControllerBackgroundService(
        IMainServerTopologyClient topologyClient,
        IMockDeviceClient mockDeviceClient,
        IMqttBridgeService mqttBridgeService,
        IAutomationRuleEngine ruleEngine,
        IControllerStateStore stateStore,
        IHubContext<ControllerHub> hubContext,
        IOptions<IoTControllerOptions> options,
        ILogger<IoTControllerBackgroundService> logger)
    {
        _topologyClient = topologyClient;
        _mockDeviceClient = mockDeviceClient;
        _mqttBridgeService = mqttBridgeService;
        _ruleEngine = ruleEngine;
        _stateStore = stateStore;
        _hubContext = hubContext;
        _options = options.Value;
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
        var topology = await _topologyClient.GetTopologyAsync(clientId, cancellationToken);
        if (topology is not null)
        {
            _stateStore.UpdateTopology(clientId, topology);
        }

        var currentTopology = _stateStore.GetTopology(clientId);
        if (currentTopology is null || currentTopology.Plants.Count == 0)
        {
            return;
        }

        var nowUtc = DateTime.UtcNow;
        var pumpStateMachine = _stateStore.GetPumpStateMachine(clientId);
        pumpStateMachine.Refresh(nowUtc);

        var telemetry = await _mockDeviceClient.ReadTelemetryAsync(
            currentTopology.ClientId,
            pumpStateMachine.Phase,
            null,
            pumpStateMachine.WarningMessage,
            pumpStateMachine.SoakUntilUtc,
            cancellationToken);

        if (telemetry is null)
        {
            return;
        }

        string? activePlantName = null;
        var warningMessage = pumpStateMachine.WarningMessage;

        var rules = await _topologyClient.GetAutomationRulesAsync(clientId, cancellationToken);
        if (rules.Count > 0 && !pumpStateMachine.IsInSoak(nowUtc))
        {
            var decisions = _ruleEngine.Evaluate(rules, telemetry, nowUtc);
            foreach (var decision in decisions)
            {
                var isPumpAction = decision.State && string.Equals(decision.Command, "pump", StringComparison.OrdinalIgnoreCase);
                if (isPumpAction && IsWaterLevelTooLow(telemetry.WaterLevelCm))
                {
                    warningMessage = $"Brak wody w zbiorniku. Zablokowano regułę dla rośliny {decision.Rule.PlantName}.";
                    _logger.LogWarning(warningMessage);
                    pumpStateMachine.MarkBlocked(warningMessage);
                    continue;
                }

                var published = await _mqttBridgeService.PublishActuatorCommandAsync(
                    decision.ActuatorExternalDeviceId,
                    decision.Command,
                    decision.State,
                    Math.Max(1, decision.DurationSeconds) * 1000,
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

    private IReadOnlyList<int> ResolveClientIds()
    {
        return _options.ClientIds
            .Where(id => id > 0)
            .Distinct()
            .ToList();
    }

    private bool IsWaterLevelTooLow(int waterLevelCm)
    {
        return waterLevelCm <= Math.Max(0, _options.LowWaterThresholdCm);
    }

    private async Task PublishTelemetryAsync(int clientId, ControllerTelemetryDto telemetry, CancellationToken cancellationToken)
    {
        await _hubContext.Clients.All.SendAsync("TelemetryUpdated", telemetry, cancellationToken);
        _logger.LogInformation("Zaktualizowano telemetrię dla klienta {ClientId}.", clientId);
    }
}