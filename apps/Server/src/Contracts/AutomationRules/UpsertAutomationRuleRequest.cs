namespace RePlanted.Server.Contracts.AutomationRules;

public class UpsertAutomationRuleRequest
{
    public int? PlantId { get; set; }

    public int? SensorDeviceId { get; set; }
    public string? SensorField { get; set; }

    public string? Condition { get; set; }
    public double? Threshold { get; set; }

    public int? ActuatorDeviceId { get; set; }
    public string? Action { get; set; }
    public int? DurationSeconds { get; set; }

    public TimeSpan? ScheduleStartTime { get; set; }
    public TimeSpan? ScheduleEndTime { get; set; }

    public int? Priority { get; set; }
    public int? CooldownMinutes { get; set; }
    public string? Status { get; set; }
}
