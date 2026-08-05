namespace RePlanted.Server.Services;

public sealed class TelemetryCollectorOptions
{
    public const string SectionName = "TelemetryCollector";

    public string BaseUrl { get; set; } = "http://localhost:8080";
    public string SensorsPath { get; set; } = "/api/client-server/controllers/1/telemetry/current";
    public int PollingIntervalSeconds { get; set; } = 15;
    public int RetentionDays { get; set; } = 30;
}