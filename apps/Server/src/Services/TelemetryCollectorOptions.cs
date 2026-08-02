namespace RePlanted.Server.Services;

public sealed class TelemetryCollectorOptions
{
    public const string SectionName = "TelemetryCollector";

    public string BaseUrl { get; set; } = "http://localhost:8085";
    public string SensorsPath { get; set; } = "/sensors";
    public int PollingIntervalSeconds { get; set; } = 15;
    public int RetentionDays { get; set; } = 30;
}