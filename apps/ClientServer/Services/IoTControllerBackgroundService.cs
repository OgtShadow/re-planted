using ClientServer.Contracts;
using ClientServer.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace ClientServer.Services;

public sealed class IoTControllerBackgroundService : BackgroundService
{
    private readonly IMainServerTopologyClient _topologyClient;
    private readonly IMockDeviceClient _mockDeviceClient;
    private readonly IControllerStateStore _stateStore;
    private readonly IHubContext<ControllerHub> _hubContext;
    private readonly IoTControllerOptions _options;
    private readonly ILogger<IoTControllerBackgroundService> _logger;

    public IoTControllerBackgroundService(
        IMainServerTopologyClient topologyClient,
        IMockDeviceClient mockDeviceClient,
        IControllerStateStore stateStore,
        IHubContext<ControllerHub> hubContext,
        IOptions<IoTControllerOptions> options,
        ILogger<IoTControllerBackgroundService> logger)
    {
        _topologyClient = topologyClient;
        _mockDeviceClient = mockDeviceClient;
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
        var topology = await _topologyClient.GetTopologyAsync(cancellationToken);
        if (topology is not null)
        {
            _stateStore.UpdateTopology(topology);
        }

        var currentTopology = _stateStore.Topology;
        if (currentTopology is null || currentTopology.Plants.Count == 0)
        {
            return;
        }

        var nowUtc = DateTime.UtcNow;
        _stateStore.PumpStateMachine.Refresh(nowUtc);

        var activePlant = SelectPlantNeedingWater(currentTopology.Plants);
        if (activePlant is not null && !_stateStore.PumpStateMachine.IsInSoak(nowUtc))
        {
            var freshSnapshot = await _mockDeviceClient.ReadTelemetryAsync(
                currentTopology.ClientId,
                _stateStore.PumpStateMachine.Phase,
                activePlant.Name,
                _stateStore.PumpStateMachine.WarningMessage,
                _stateStore.PumpStateMachine.SoakUntilUtc,
                cancellationToken);

            if (freshSnapshot is not null && IsWaterLevelTooLow(freshSnapshot.WaterLevelCm))
            {
                var warningMessage = $"Brak wody w zbiorniku. Zablokowano uruchomienie pompy dla rośliny {activePlant.Name}.";
                _logger.LogWarning(warningMessage);
                _stateStore.PumpStateMachine.MarkBlocked(warningMessage);
                await PublishTelemetryAsync(currentTopology.ClientId, freshSnapshot with
                {
                    ControllerState = PumpControlPhase.Idle.ToString(),
                    ActivePlantName = activePlant.Name,
                    WarningMessage = warningMessage
                }, cancellationToken);
                return;
            }

            if (freshSnapshot is not null)
            {
                _stateStore.PumpStateMachine.BeginWatering(activePlant.Name);

                var pumpStarted = await _mockDeviceClient.TurnPumpOnAsync(_options.PumpRunSeconds, cancellationToken);
                if (!pumpStarted)
                {
                    _stateStore.PumpStateMachine.MarkBlocked($"Nie udało się uruchomić pompy dla rośliny {activePlant.Name}.");
                    return;
                }

                _stateStore.PumpStateMachine.BeginSoak(nowUtc, TimeSpan.FromSeconds(Math.Clamp(_options.SoakTimeSeconds, 10, 600)));
                _logger.LogInformation("Uruchomiono pompę dla rośliny {PlantName}. Rozpoczynam okres wchłaniania.", activePlant.Name);
            }
        }

        var telemetry = await _mockDeviceClient.ReadTelemetryAsync(
            currentTopology.ClientId,
            _stateStore.PumpStateMachine.Phase,
            activePlant?.Name,
            _stateStore.PumpStateMachine.WarningMessage,
            _stateStore.PumpStateMachine.SoakUntilUtc,
            cancellationToken);

        if (telemetry is null)
        {
            return;
        }

        var enrichedTelemetry = telemetry with
        {
            ControllerState = _stateStore.PumpStateMachine.Phase.ToString(),
            ActivePlantName = activePlant?.Name,
            WarningMessage = _stateStore.PumpStateMachine.WarningMessage,
            LastSyncUtc = currentTopology.SyncedAtUtc
        };

        _stateStore.UpdateTelemetry(enrichedTelemetry);
        await PublishTelemetryAsync(currentTopology.ClientId, enrichedTelemetry, cancellationToken);
    }

    private static ControllerPlantDto? SelectPlantNeedingWater(IReadOnlyList<ControllerPlantDto> plants)
    {
        return plants
            .Where(plant => plant.Devices.Any(device => device.IsEnabled && string.Equals(device.TargetParameter, "soilMoisture", StringComparison.OrdinalIgnoreCase)))
            .OrderBy(plant => plant.Parameters.HumidityMin)
            .FirstOrDefault(plant => IsBelowTarget(plant));
    }

    private static bool IsBelowTarget(ControllerPlantDto plant)
    {
        var currentSoilMoisturePercent = 0;
        return currentSoilMoisturePercent < plant.Parameters.HumidityMin;
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