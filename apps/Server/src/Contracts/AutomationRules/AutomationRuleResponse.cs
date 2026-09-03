namespace RePlanted.Server.Contracts.AutomationRules;

public sealed class AutomationRuleResponse
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int PlantId { get; set; }
    public string PlantName { get; set; } = string.Empty;

    public int SensorDeviceId { get; set; }
    public string SensorDeviceName { get; set; } = string.Empty;
    public string SensorField { get; set; } = string.Empty;

    public string Condition { get; set; } = string.Empty;
    public double Threshold { get; set; }

    public int ActuatorDeviceId { get; set; }
    public string ActuatorDeviceName { get; set; } = string.Empty;
    public string ActuatorExternalDeviceId { get; set; } = string.Empty;
    public string ActuatorTargetParameter { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public int DurationSeconds { get; set; }

    public TimeSpan? ScheduleStartTime { get; set; }
    public TimeSpan? ScheduleEndTime { get; set; }

    public int Priority { get; set; }
    public int CooldownMinutes { get; set; }
    public string Status { get; set; } = string.Empty;

    public DateTime? LastTriggeredUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
