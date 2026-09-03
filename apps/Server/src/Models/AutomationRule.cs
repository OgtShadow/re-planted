namespace RePlanted.Server.Models;

public static class AutomationConditions
{
    public const string LessThan = "LessThan";
    public const string LessOrEqual = "LessOrEqual";
    public const string GreaterThan = "GreaterThan";
    public const string GreaterOrEqual = "GreaterOrEqual";

    public static readonly IReadOnlyCollection<string> All = new[] { LessThan, LessOrEqual, GreaterThan, GreaterOrEqual };
}

public static class AutomationActions
{
    public const string TurnOn = "TurnOn";
    public const string TurnOff = "TurnOff";

    public static readonly IReadOnlyCollection<string> All = new[] { TurnOn, TurnOff };
}

public static class AutomationRuleStatuses
{
    public const string Enabled = "Enabled";
    public const string Disabled = "Disabled";

    public static readonly IReadOnlyCollection<string> All = new[] { Enabled, Disabled };
}

/// <summary>A persistent automation rule: sensor condition on a plant triggers an actuator action.</summary>
public class AutomationRule
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    public int PlantId { get; set; }
    public Plant? Plant { get; set; }

    public int SensorDeviceId { get; set; }
    public ActuatorDevice? SensorDevice { get; set; }
    public string SensorField { get; set; } = string.Empty;

    public string Condition { get; set; } = AutomationConditions.LessThan;
    public double Threshold { get; set; }

    public int ActuatorDeviceId { get; set; }
    public ActuatorDevice? ActuatorDevice { get; set; }
    public string Action { get; set; } = AutomationActions.TurnOn;
    public int DurationSeconds { get; set; } = 5;

    /// <summary>Optional daily time-of-day window (harmonogram) in which the rule is allowed to fire.</summary>
    public TimeSpan? ScheduleStartTime { get; set; }
    public TimeSpan? ScheduleEndTime { get; set; }

    /// <summary>Lower value wins when multiple rules compete for the same actuator.</summary>
    public int Priority { get; set; } = 100;

    /// <summary>Minimum number of minutes between two consecutive triggers of this rule.</summary>
    public int CooldownMinutes { get; set; } = 30;

    public string Status { get; set; } = AutomationRuleStatuses.Enabled;

    public DateTime? LastTriggeredUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
