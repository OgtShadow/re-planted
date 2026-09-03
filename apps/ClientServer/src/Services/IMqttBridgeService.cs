using ClientServer.Contracts;

namespace ClientServer.Services;

public interface IMqttBridgeService
{
    Task<bool> PublishPumpCommandAsync(string deviceId, int durationMs, CancellationToken cancellationToken = default);
    Task<bool> PublishActuatorCommandAsync(string deviceId, string command, bool state, int durationMs, CancellationToken cancellationToken = default);
    bool TryGetLatestTelemetry(string deviceId, out TelemetryPayload? telemetry);
}
