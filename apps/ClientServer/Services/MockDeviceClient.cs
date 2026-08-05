using ClientServer.Contracts;
using Microsoft.Extensions.Options;

namespace ClientServer.Services;

public interface IMockDeviceClient
{
    Task<ControllerTelemetryDto?> ReadTelemetryAsync(int clientId, PumpControlPhase phase, string? activePlantName, string? warningMessage, DateTime? soakUntilUtc, CancellationToken cancellationToken);
    Task<bool> TurnPumpOnAsync(int durationSeconds, CancellationToken cancellationToken);
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

    public async Task<bool> TurnPumpOnAsync(int durationSeconds, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.PostAsJsonAsync(_options.PumpCommandPath, new
            {
                state = true,
                durationSeconds = Math.Max(1, durationSeconds)
            }, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Nie udało się włączyć pompy w mocku. Kod: {StatusCode}, Treść: {Body}", response.StatusCode, body);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Nie udało się wysłać komendy do pompy w mocku.");
            return false;
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