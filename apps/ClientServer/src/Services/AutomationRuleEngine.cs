using ClientServer.Contracts;

namespace ClientServer.Services;

/// <summary>A single actuator action that an automation rule wants to execute this cycle.</summary>
public sealed record RuleTriggerDecision(
    ControllerAutomationRuleDto Rule,
    string ActuatorExternalDeviceId,
    string Command,
    bool State,
    int DurationSeconds);

public interface IAutomationRuleEngine
{
    /// <summary>
    /// Evaluates all enabled rules against the latest telemetry snapshot and returns the actions that
    /// should be executed this cycle, highest priority first. At most one decision is produced per
    /// actuator device: once a rule claims an actuator, lower-priority rules targeting the same
    /// actuator are skipped for this cycle.
    /// </summary>
    IReadOnlyList<RuleTriggerDecision> Evaluate(
        IReadOnlyList<ControllerAutomationRuleDto> rules,
        ControllerTelemetryDto telemetry,
        DateTime nowUtc);
}

public sealed class AutomationRuleEngine : IAutomationRuleEngine
{
    public IReadOnlyList<RuleTriggerDecision> Evaluate(
        IReadOnlyList<ControllerAutomationRuleDto> rules,
        ControllerTelemetryDto telemetry,
        DateTime nowUtc)
    {
        var decisions = new List<RuleTriggerDecision>();
        var claimedActuators = new HashSet<int>();

        foreach (var rule in rules
            .Where(rule => string.Equals(rule.Status, "Enabled", StringComparison.OrdinalIgnoreCase))
            .OrderBy(rule => rule.Priority))
        {
            if (claimedActuators.Contains(rule.ActuatorDeviceId))
            {
                continue;
            }

            if (!IsWithinSchedule(rule, nowUtc))
            {
                continue;
            }

            if (IsInCooldown(rule, nowUtc))
            {
                continue;
            }

            if (!TryReadSensorValue(telemetry, rule.SensorField, out var sensorValue))
            {
                continue;
            }

            if (!IsConditionSatisfied(rule.Condition, sensorValue, rule.Threshold))
            {
                continue;
            }

            var state = string.Equals(rule.Action, "TurnOn", StringComparison.OrdinalIgnoreCase);
            decisions.Add(new RuleTriggerDecision(rule, rule.ActuatorExternalDeviceId, rule.ActuatorCommand, state, rule.DurationSeconds));
            claimedActuators.Add(rule.ActuatorDeviceId);
        }

        return decisions;
    }

    private static bool IsInCooldown(ControllerAutomationRuleDto rule, DateTime nowUtc)
    {
        if (rule.LastTriggeredUtc is not { } lastTriggeredUtc)
        {
            return false;
        }

        return nowUtc - lastTriggeredUtc < TimeSpan.FromMinutes(Math.Max(0, rule.CooldownMinutes));
    }

    private static bool IsWithinSchedule(ControllerAutomationRuleDto rule, DateTime nowUtc)
    {
        if (rule.ScheduleStartTime is not { } start || rule.ScheduleEndTime is not { } end)
        {
            return true;
        }

        var timeOfDay = nowUtc.TimeOfDay;
        return start <= end
            ? timeOfDay >= start && timeOfDay <= end
            : timeOfDay >= start || timeOfDay <= end; // window wraps past midnight
    }

    private static bool TryReadSensorValue(ControllerTelemetryDto telemetry, string sensorField, out double value)
    {
        switch (sensorField.Trim().ToLowerInvariant())
        {
            case "soilmoistureanalog":
                value = telemetry.SoilMoistureAnalog;
                return true;
            case "temperature":
                value = telemetry.Temperature;
                return true;
            case "humidity":
                value = telemetry.Humidity;
                return true;
            case "waterlevelcm":
                value = telemetry.WaterLevelCm;
                return true;
            default:
                value = 0;
                return false;
        }
    }

    private static bool IsConditionSatisfied(string condition, double value, double threshold)
    {
        return condition switch
        {
            "LessThan" => value < threshold,
            "LessOrEqual" => value <= threshold,
            "GreaterThan" => value > threshold,
            "GreaterOrEqual" => value >= threshold,
            _ => false
        };
    }
}
