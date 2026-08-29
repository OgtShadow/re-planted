namespace RePlanted.Server.Contracts.Telemetry;

public sealed class TelemetryTrendResponse
{
    public string DeviceId { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string ExternalDeviceId { get; set; } = string.Empty;
    public IReadOnlyList<int> PlantIds { get; set; } = [];
    public IReadOnlyList<string> PlantNames { get; set; } = [];
    public int? PlantId { get; set; }
    public string SensorField { get; set; } = string.Empty;
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }
    public int IntervalMinutes { get; set; }
    public IReadOnlyList<TelemetryTrendPoint> Points { get; set; } = [];
}