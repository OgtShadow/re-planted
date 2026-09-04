namespace RePlanted.Server.Models;

public static class AlertTypes
{
    public const string LowWater = "LowWater";
    public const string MissingTelemetry = "MissingTelemetry";
    public const string DeviceDisconnected = "DeviceDisconnected";
    public const string CommandFailed = "CommandFailed";
    public const string ThresholdExceeded = "ThresholdExceeded";
    public const string RuleConflict = "RuleConflict";
}

public static class AlertSeverities
{
    public const string Info = "Info";
    public const string Warning = "Warning";
    public const string Critical = "Critical";
}

public class Alert
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    public string Type { get; set; } = AlertTypes.ThresholdExceeded;
    public string Severity { get; set; } = AlertSeverities.Warning;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string SourceKey { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? AcknowledgedAtUtc { get; set; }
}
