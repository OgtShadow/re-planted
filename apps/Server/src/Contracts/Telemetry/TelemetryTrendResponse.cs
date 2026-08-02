namespace RePlanted.Server.Contracts.Telemetry;

public sealed class TelemetryTrendResponse
{
    public string DeviceId { get; set; } = string.Empty;
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }
    public int IntervalMinutes { get; set; }
    public IReadOnlyList<TelemetryTrendPoint> Points { get; set; } = [];
}