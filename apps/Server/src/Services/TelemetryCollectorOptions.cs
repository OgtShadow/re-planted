namespace RePlanted.Server.Services;

public sealed class TelemetryCollectorOptions
{
    public const string SectionName = "TelemetryCollector";

    public string BaseUrl { get; set; } = "http://localhost:8080";
    public string SensorsPath { get; set; } = "/api/client-server/controllers/telemetry/current";
    public int PollingIntervalSeconds { get; set; } = 15;
    public int RetentionDays { get; set; } = 30;

    // Extra full URLs, each returning a single SensorTelemetrySnapshot-shaped JSON object,
    // polled in addition to the primary aggregated endpoint (e.g. secondary mock devices).
    public List<string> AdditionalSensorUrls { get; set; } = new();
}