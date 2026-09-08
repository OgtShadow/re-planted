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

        public Task<bool> TurnPumpOnAsync(int durationSeconds, CancellationToken cancellationToken) => Task.FromResult(true);
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
