using ClientServer.Contracts;

namespace ClientServer.Services;

public interface IMqttBridgeService
{
    Task<bool> PublishPumpCommandAsync(string deviceId, int durationMs, CancellationToken cancellationToken = default);
    bool TryGetLatestTelemetry(string deviceId, out TelemetryPayload? telemetry);
}
