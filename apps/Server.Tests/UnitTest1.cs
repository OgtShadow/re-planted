using ClientServer.Contracts;
using ClientServer.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Server.Tests;

public sealed class OfflineControllerTests
{
    [Fact]
    public async Task UsesLastValidSnapshotAndPublishesCommandWhenMainServerIsOffline()
    {
        var clientId = 1;
        var commandPublished = new TaskCompletionSource<PublishedCommand>(TaskCreationOptions.RunContinuationsAsynchronously);
        var stateStore = new ControllerStateStore();
        var topology = new ControllerTopologyDto(
            clientId,
            DateTime.UtcNow,
            [new ControllerPlantDto(
                10,
                "Monstera",
                "Monstera deliciosa",
                new ControllerPlantParametersDto(2, 40, 70, 8, 18, 28),
                [])]);
        var rule = new ControllerAutomationRuleDto(
            20,
            10,
            "Monstera",
            30,
            "SoilMoistureAnalog",
            "LessThan",
            50,
            40,
            "pump-1",
            "pump",
            "water",
            1,
            "TurnOn",
            2,
            null,
            null,
            1,
            0,
            "Enabled",
            null);
        stateStore.UpdateConfiguration(new ControllerConfigurationSnapshot(
            clientId,
            "snapshot-version",
            DateTime.UtcNow.AddMinutes(-1),
            DateTime.UtcNow.AddMinutes(30),
            topology,
            [rule]));

        var service = new IoTControllerBackgroundService(
            new OfflineMainServerClient(),
            new FixedTelemetryClient(new ControllerTelemetryDto(
                "sensor-1",
                20,
                22,
                50,
                10,
                false,
                false,
                DateTime.UtcNow,
                clientId,
                "Idle",
                null,
                null,
                DateTime.UtcNow)),
            new CapturingMqttBridge(commandPublished),
            new AutomationRuleEngine(),
            new PumpSafetyGuard(stateStore, Options.Create(new IoTControllerOptions
            {
                MaxPumpRunSeconds = 30,
                MaxTelemetryAgeSeconds = 30,
                LowWaterThresholdCm = 2
            })),
            stateStore,
            new CapturingTelemetryPublisher(),
            Options.Create(new IoTControllerOptions
            {
                ClientIds = [clientId],
                PollingIntervalSeconds = 5,
                SoakTimeSeconds = 10,
                LowWaterThresholdCm = 2
            }),
            Options.Create(new OfflineModeOptions
            {
                Enabled = true,
                SnapshotValidityMinutes = 30,
                ContinueWithExpiredSnapshot = false
            }),
            NullLogger<IoTControllerBackgroundService>.Instance);

        await service.StartAsync(CancellationToken.None);
        var command = await commandPublished.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await service.StopAsync(CancellationToken.None);

        Assert.Equal("pump-1", command.DeviceId);
        Assert.Equal("pump", command.Command);
        Assert.True(command.State);
        Assert.Equal(2000, command.DurationMs);
    }

    [Fact]
    public void PumpSafetyGuardClampsMaximumDurationAndBlocksLowWater()
    {
        var stateStore = new ControllerStateStore();
        var guard = new PumpSafetyGuard(stateStore, Options.Create(new IoTControllerOptions
        {
            MaxPumpRunSeconds = 3,
            MaxTelemetryAgeSeconds = 30,
            LowWaterThresholdCm = 2
        }));
        var telemetry = new ControllerTelemetryDto("sensor-1", 20, 22, 50, 10, false, false, DateTime.UtcNow, 1, "Idle", null, null, DateTime.UtcNow);

        var clamped = guard.ValidateStart(1, 10000, telemetry);
        var blocked = guard.ValidateStart(1, 1000, telemetry with { WaterLevelCm = 2 });

        Assert.True(clamped.Allowed);
        Assert.Equal(3000, clamped.DurationMs);
        Assert.False(blocked.Allowed);
    }

    [Fact]
    public async Task HungTelemetryReadDoesNotBlockControllerCycle()
    {
        var stateStore = new ControllerStateStore();
        stateStore.UpdateTopology(1, new ControllerTopologyDto(1, DateTime.UtcNow, [new ControllerPlantDto(1, "Plant", "Species", new ControllerPlantParametersDto(1, 1, 2, 1, 1, 2), [])]));
        var commandPublished = new TaskCompletionSource<PublishedCommand>(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new IoTControllerBackgroundService(
            new OfflineMainServerClient(),
            new HungTelemetryClient(),
            new CapturingMqttBridge(commandPublished),
            new AutomationRuleEngine(),
            new PumpSafetyGuard(stateStore, Options.Create(new IoTControllerOptions { TelemetryTimeoutSeconds = 1 })),
            stateStore,
            new CapturingTelemetryPublisher(),
            Options.Create(new IoTControllerOptions { ClientIds = [1], TelemetryTimeoutSeconds = 1 }),
            Options.Create(new OfflineModeOptions()),
            NullLogger<IoTControllerBackgroundService>.Instance);

        await service.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromMilliseconds(1200));
        await service.StopAsync(CancellationToken.None);

        Assert.False(commandPublished.Task.IsCompleted);
    }

    [Fact]
    public async Task ManualPumpCommandUsesMqttBridgeAndUpdatesStateMachine()
    {
        var clientId = 1;
        var deviceId = "esp32-node-01";
        var stateStore = new ControllerStateStore();
        stateStore.UpdateTelemetry(clientId, new ControllerTelemetryDto(
            "sensor-1", 20, 22, 50, 10, false, false, DateTime.UtcNow, clientId, "Idle", null, null, DateTime.UtcNow));

        var commandPublished = new TaskCompletionSource<PublishedCommand>(TaskCreationOptions.RunContinuationsAsynchronously);
        var mqttBridge = new CapturingMqttBridge(commandPublished);
        var safetyGuard = new PumpSafetyGuard(stateStore, Options.Create(new IoTControllerOptions
        {
            MaxPumpRunSeconds = 10,
            MaxTelemetryAgeSeconds = 30,
            LowWaterThresholdCm = 2
        }));

        var controller = new ClientServer.Controllers.IoTControllerController(
            stateStore,
            new OfflineMainServerClient(),
            mqttBridge,
            safetyGuard,
            Options.Create(new IoTControllerOptions { SoakTimeSeconds = 30 }),
            NullLogger<ClientServer.Controllers.IoTControllerController>.Instance);

        var actionResult = await controller.RunPumpWithMqtt(clientId, deviceId, new PumpCommandRequest(5000), CancellationToken.None);
        var acceptedResult = Assert.IsType<Microsoft.AspNetCore.Mvc.AcceptedResult>(actionResult);
        Assert.NotNull(acceptedResult.Value);

        var command = await commandPublished.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(deviceId, command.DeviceId);
        Assert.Equal("pump", command.Command);
        Assert.True(command.State);
        Assert.Equal(5000, command.DurationMs);

        var machine = stateStore.GetPumpStateMachine(clientId);
        Assert.Equal(PumpControlPhase.Soaking, machine.Phase);
        Assert.Equal("Sterowanie ręczne", machine.ActivePlantName);
    }

    [Fact]
    public async Task ManualActuatorCommandExecutesOverMqttBridge()
    {
        var clientId = 1;
        var deviceId = "esp32-lamp-01";
        var stateStore = new ControllerStateStore();
        var commandPublished = new TaskCompletionSource<PublishedCommand>(TaskCreationOptions.RunContinuationsAsynchronously);
        var mqttBridge = new CapturingMqttBridge(commandPublished);
        var safetyGuard = new PumpSafetyGuard(stateStore, Options.Create(new IoTControllerOptions()));

        var controller = new ClientServer.Controllers.IoTControllerController(
            stateStore,
            new OfflineMainServerClient(),
            mqttBridge,
            safetyGuard,
            Options.Create(new IoTControllerOptions()),
            NullLogger<ClientServer.Controllers.IoTControllerController>.Instance);

        var actionResult = await controller.ExecuteActuatorCommand(
            clientId,
            deviceId,
            new ActuatorCommandRequest("lamp", true, 0),
            CancellationToken.None);

        var acceptedResult = Assert.IsType<Microsoft.AspNetCore.Mvc.AcceptedResult>(actionResult);
        Assert.NotNull(acceptedResult.Value);

        var command = await commandPublished.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(deviceId, command.DeviceId);
        Assert.Equal("lamp", command.Command);
        Assert.True(command.State);
        Assert.Equal(0, command.DurationMs);
    }

    [Fact]
    public async Task ManualEmergencyStopExecutesOverMqttBridge()
    {
        var clientId = 1;
        var deviceId = "esp32-node-01";
        var stateStore = new ControllerStateStore();
        var commandPublished = new TaskCompletionSource<PublishedCommand>(TaskCreationOptions.RunContinuationsAsynchronously);
        var mqttBridge = new CapturingMqttBridge(commandPublished);
        var safetyGuard = new PumpSafetyGuard(stateStore, Options.Create(new IoTControllerOptions()));

        var controller = new ClientServer.Controllers.IoTControllerController(
            stateStore,
            new OfflineMainServerClient(),
            mqttBridge,
            safetyGuard,
            Options.Create(new IoTControllerOptions()),
            NullLogger<ClientServer.Controllers.IoTControllerController>.Instance);

        var actionResult = await controller.StopPumpWithMqtt(clientId, deviceId, CancellationToken.None);
        var acceptedResult = Assert.IsType<Microsoft.AspNetCore.Mvc.AcceptedResult>(actionResult);
        Assert.NotNull(acceptedResult.Value);

        var command = await commandPublished.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(deviceId, command.DeviceId);
        Assert.Equal("pump", command.Command);
        Assert.False(command.State);
        Assert.Equal(0, command.DurationMs);

        var machine = stateStore.GetPumpStateMachine(clientId);
        Assert.Equal(PumpControlPhase.Idle, machine.Phase);
    }

    private sealed record PublishedCommand(string DeviceId, string Command, bool State, int DurationMs);

    private sealed class OfflineMainServerClient : IMainServerTopologyClient
    {
        public Task<ControllerTopologyDto?> GetTopologyAsync(int clientId, CancellationToken cancellationToken) => Task.FromResult<ControllerTopologyDto?>(null);
        public Task<IReadOnlyList<ControllerAutomationRuleDto>?> GetAutomationRulesAsync(int clientId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ControllerAutomationRuleDto>?>(null);
        public Task<ControllerConfigurationSnapshot?> GetConfigurationAsync(int clientId, CancellationToken cancellationToken) => Task.FromResult<ControllerConfigurationSnapshot?>(null);
        public Task NotifyRuleTriggeredAsync(int clientId, int ruleId, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FixedTelemetryClient : IMockDeviceClient
    {
        private readonly ControllerTelemetryDto _telemetry;

        public FixedTelemetryClient(ControllerTelemetryDto telemetry) => _telemetry = telemetry;

        public Task<ControllerTelemetryDto?> ReadTelemetryAsync(int clientId, PumpControlPhase phase, string? activePlantName, string? warningMessage, DateTime? soakUntilUtc, CancellationToken cancellationToken)
            => Task.FromResult<ControllerTelemetryDto?>(_telemetry with { ClientId = clientId });
    }

    private sealed class HungTelemetryClient : IMockDeviceClient
    {
        public async Task<ControllerTelemetryDto?> ReadTelemetryAsync(int clientId, PumpControlPhase phase, string? activePlantName, string? warningMessage, DateTime? soakUntilUtc, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return null;
        }
    }

    private sealed class CapturingMqttBridge : IMqttBridgeService
    {
        private readonly TaskCompletionSource<PublishedCommand> _commandPublished;

        public CapturingMqttBridge(TaskCompletionSource<PublishedCommand> commandPublished) => _commandPublished = commandPublished;

        public Task<bool> PublishPumpCommandAsync(string deviceId, int durationMs, CancellationToken cancellationToken)
            => PublishActuatorCommandAsync(deviceId, "pump", true, durationMs, cancellationToken);

        public Task<bool> PublishActuatorCommandAsync(string deviceId, string command, bool state, int durationMs, CancellationToken cancellationToken)
        {
            _commandPublished.TrySetResult(new PublishedCommand(deviceId, command, state, durationMs));
            return Task.FromResult(true);
        }

        public bool TryGetLatestTelemetry(string deviceId, out TelemetryPayload? telemetry)
        {
            telemetry = null;
            return false;
        }
    }

    private sealed class CapturingTelemetryPublisher : IControllerTelemetryPublisher
    {
        public Task PublishAsync(ControllerTelemetryDto telemetry, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
