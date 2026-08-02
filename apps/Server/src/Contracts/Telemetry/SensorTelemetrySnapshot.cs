using System.Text.Json.Serialization;

namespace RePlanted.Server.Contracts.Telemetry;

public sealed class SensorTelemetrySnapshot
{
    [JsonPropertyName("deviceId")]
    public string DeviceId { get; set; } = string.Empty;

    [JsonPropertyName("soilMoistureAnalog")]
    public int SoilMoistureAnalog { get; set; }

    [JsonPropertyName("temperature")]
    public int Temperature { get; set; }

    [JsonPropertyName("humidity")]
    public int Humidity { get; set; }

    [JsonPropertyName("waterLevelCm")]
    public int WaterLevelCm { get; set; }

    [JsonPropertyName("pumpState")]
    public bool PumpState { get; set; }

    [JsonPropertyName("lampState")]
    public bool LampState { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }
}