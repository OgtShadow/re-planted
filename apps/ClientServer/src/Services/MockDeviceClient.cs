using ClientServer.Contracts;
using Microsoft.Extensions.Options;

namespace ClientServer.Services;

public interface IMockDeviceClient
{
    Task<ControllerTelemetryDto?> ReadTelemetryAsync(int clientId, PumpControlPhase phase, string? activePlantName, string? warningMessage, DateTime? soakUntilUtc, CancellationToken cancellationToken);
}

public sealed class MockDeviceClient : IMockDeviceClient
{
    private readonly HttpClient _httpClient;
    private readonly MockDeviceApiOptions _options;
    private readonly ILogger<MockDeviceClient> _logger;

    public MockDeviceClient(
        HttpClient httpClient,
        IOptions<MockDeviceApiOptions> options,
        ILogger<MockDeviceClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ControllerTelemetryDto?> ReadTelemetryAsync(int clientId, PumpControlPhase phase, string? activePlantName, string? warningMessage, DateTime? soakUntilUtc, CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = await _httpClient.GetFromJsonAsync<SensorTelemetryDto>(_options.SensorsPath, cancellationToken);
            if (snapshot is null)
            {
                return null;
            }

            return new ControllerTelemetryDto(
                string.IsNullOrWhiteSpace(snapshot.DeviceId) ? $"client-{clientId}" : $"{snapshot.DeviceId}-client-{clientId}",
                snapshot.SoilMoistureAnalog,
                snapshot.Temperature,
                snapshot.Humidity,
                snapshot.WaterLevelCm,
                snapshot.PumpState,
                snapshot.LampState,
                snapshot.Timestamp == default ? DateTime.UtcNow : snapshot.Timestamp.ToUniversalTime(),
                clientId,
                phase.ToString(),
                activePlantName,
                warningMessage,
                soakUntilUtc ?? DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Nie udało się odczytać danych z mocka urządzenia.");
            return null;
        }
    }

    private sealed record SensorTelemetryDto
    {
        public string DeviceId { get; set; } = string.Empty;
        public int SoilMoistureAnalog { get; set; }
        public int Temperature { get; set; }
        public int Humidity { get; set; }
        public int WaterLevelCm { get; set; }
        public bool PumpState { get; set; }
        public bool LampState { get; set; }
        public DateTime Timestamp { get; set; }
    }
}