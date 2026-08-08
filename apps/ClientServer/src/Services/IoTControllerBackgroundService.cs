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
    private bool _reportedEmptyClientSet;

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

        var activePlant = SelectPlantNeedingWater(currentTopology.Plants, telemetry.SoilMoistureAnalog, _options.MoistureThresholdBufferPercent);
        if (activePlant is not null && !pumpStateMachine.IsInSoak(nowUtc))
        {
            if (IsWaterLevelTooLow(telemetry.WaterLevelCm))
            {
                var warningMessage = $"Brak wody w zbiorniku. Zablokowano uruchomienie pompy dla rośliny {activePlant.Name}.";
                _logger.LogWarning(warningMessage);
                pumpStateMachine.MarkBlocked(warningMessage);

                var blockedTelemetry = telemetry with
                {
                    ControllerState = PumpControlPhase.Idle.ToString(),
                    ActivePlantName = activePlant.Name,
                    WarningMessage = warningMessage,
                    LastSyncUtc = currentTopology.SyncedAtUtc
                };

                _stateStore.UpdateTelemetry(clientId, blockedTelemetry);
                await PublishTelemetryAsync(currentTopology.ClientId, blockedTelemetry, cancellationToken);
                return;
            }

            pumpStateMachine.BeginWatering(activePlant.Name);

            var pumpStarted = await _mockDeviceClient.TurnPumpOnAsync(_options.PumpRunSeconds, cancellationToken);
            if (!pumpStarted)
            {
                pumpStateMachine.MarkBlocked($"Nie udało się uruchomić pompy dla rośliny {activePlant.Name}.");
                return;
            }

            pumpStateMachine.BeginSoak(nowUtc, TimeSpan.FromSeconds(Math.Clamp(_options.SoakTimeSeconds, 10, 600)));
            _logger.LogInformation("Uruchomiono pompę dla rośliny {PlantName}. Rozpoczynam okres wchłaniania.", activePlant.Name);
        }

        var enrichedTelemetry = telemetry with
        {
            ControllerState = pumpStateMachine.Phase.ToString(),
            ActivePlantName = activePlant?.Name,
            WarningMessage = pumpStateMachine.WarningMessage,
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

    private static ControllerPlantDto? SelectPlantNeedingWater(IReadOnlyList<ControllerPlantDto> plants, int soilMoistureAnalog, int bufferPercent)
    {
        var soilMoisturePercent = Math.Clamp(soilMoistureAnalog / 10, 0, 100);

        return plants
            .Where(plant => plant.Devices.Any(device =>
                device.IsEnabled &&
                (string.Equals(device.TargetParameter, "soilMoisture", StringComparison.OrdinalIgnoreCase)
                 || device.SensorFields.Any(field => string.Equals(field, "soilMoistureAnalog", StringComparison.OrdinalIgnoreCase)))))
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