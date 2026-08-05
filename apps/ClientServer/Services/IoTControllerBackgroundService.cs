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

        var telemetry = await _mockDeviceClient.ReadTelemetryAsync(
            currentTopology.ClientId,
            _stateStore.PumpStateMachine.Phase,
            null,
            _stateStore.PumpStateMachine.WarningMessage,
            _stateStore.PumpStateMachine.SoakUntilUtc,
            cancellationToken);

        if (telemetry is null)
        {
            return;
        }

        var activePlant = SelectPlantNeedingWater(currentTopology.Plants, telemetry.SoilMoistureAnalog, _options.MoistureThresholdBufferPercent);
        if (activePlant is not null && !_stateStore.PumpStateMachine.IsInSoak(nowUtc))
        {
            if (IsWaterLevelTooLow(telemetry.WaterLevelCm))
            {
                var warningMessage = $"Brak wody w zbiorniku. Zablokowano uruchomienie pompy dla rośliny {activePlant.Name}.";
                _logger.LogWarning(warningMessage);
                _stateStore.PumpStateMachine.MarkBlocked(warningMessage);

                var blockedTelemetry = telemetry with
                {
                    ControllerState = PumpControlPhase.Idle.ToString(),
                    ActivePlantName = activePlant.Name,
                    WarningMessage = warningMessage,
                    LastSyncUtc = currentTopology.SyncedAtUtc
                };

                _stateStore.UpdateTelemetry(blockedTelemetry);
                await PublishTelemetryAsync(currentTopology.ClientId, blockedTelemetry, cancellationToken);
                return;
            }

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

    private static ControllerPlantDto? SelectPlantNeedingWater(IReadOnlyList<ControllerPlantDto> plants, int soilMoistureAnalog, int bufferPercent)
    {
        var soilMoisturePercent = Math.Clamp(soilMoistureAnalog / 10, 0, 100);

        return plants
            .Where(plant => plant.Devices.Any(device => device.IsEnabled && string.Equals(device.TargetParameter, "soilMoisture", StringComparison.OrdinalIgnoreCase)))
            .OrderBy(plant => plant.Parameters.HumidityMin)
            .FirstOrDefault(plant => IsBelowTarget(plant, soilMoisturePercent, bufferPercent));
    }

    private static bool IsBelowTarget(ControllerPlantDto plant, int soilMoisturePercent, int bufferPercent)
    {
        var effectiveThreshold = Math.Max(0, plant.Parameters.HumidityMin - Math.Max(0, bufferPercent));
        return soilMoisturePercent < effectiveThreshold;
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