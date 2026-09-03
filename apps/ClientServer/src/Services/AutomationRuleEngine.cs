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
    /// should be executed this cycle. Multiple plants can share one actuator (e.g. one lamp), so when
    /// more than one rule becomes satisfied for the same actuator device, the conflict is resolved by
    /// Priority (lower wins), with a fairness tie-break so equal-priority rules can't starve each other.
    /// </summary>
    IReadOnlyList<RuleTriggerDecision> Evaluate(
        IReadOnlyList<ControllerAutomationRuleDto> rules,
        ControllerTelemetryDto telemetry,
        DateTime nowUtc);
}

public sealed class AutomationRuleEngine : IAutomationRuleEngine
{
    private readonly record struct SatisfiedRule(ControllerAutomationRuleDto Rule, double SensorValue);

    public IReadOnlyList<RuleTriggerDecision> Evaluate(
        IReadOnlyList<ControllerAutomationRuleDto> rules,
        ControllerTelemetryDto telemetry,
        DateTime nowUtc)
    {
        var satisfiedByActuator = new Dictionary<int, List<SatisfiedRule>>();

        foreach (var rule in rules.Where(rule => string.Equals(rule.Status, "Enabled", StringComparison.OrdinalIgnoreCase)))
        {
            if (!IsWithinSchedule(rule, nowUtc) || IsInCooldown(rule, nowUtc))
            {
                continue;
            }

            if (!TryReadSensorValue(telemetry, rule.SensorField, out var sensorValue) ||
                !IsConditionSatisfied(rule.Condition, sensorValue, rule.Threshold))
            {
                continue;
            }

            if (!satisfiedByActuator.TryGetValue(rule.ActuatorDeviceId, out var candidates))
            {
                candidates = new List<SatisfiedRule>();
                satisfiedByActuator[rule.ActuatorDeviceId] = candidates;
            }

            candidates.Add(new SatisfiedRule(rule, sensorValue));
        }

        var decisions = new List<RuleTriggerDecision>();
        foreach (var candidates in satisfiedByActuator.Values)
        {
            var winner = ResolveConflict(candidates);
            var state = string.Equals(winner.Rule.Action, "TurnOn", StringComparison.OrdinalIgnoreCase);
            var durationSeconds = state ? ResolveDurationSeconds(winner.Rule, winner.SensorValue) : winner.Rule.DurationSeconds;
            decisions.Add(new RuleTriggerDecision(winner.Rule, winner.Rule.ActuatorExternalDeviceId, winner.Rule.ActuatorCommand, state, durationSeconds));
        }

        return decisions.OrderBy(decision => decision.Rule.Priority).ToList();
    }

    /// <summary>
    /// Lowest Priority number wins. Ties go to whichever rule has waited longest since it last fired
    /// (never-triggered counts as longest), so equal-priority rules sharing an actuator take turns
    /// instead of one plant permanently starving another.
    /// </summary>
    private static SatisfiedRule ResolveConflict(List<SatisfiedRule> candidates)
    {
        return candidates
            .OrderBy(candidate => candidate.Rule.Priority)
            .ThenBy(candidate => candidate.Rule.LastTriggeredUtc ?? DateTime.MinValue)
            .ThenBy(candidate => candidate.Rule.Id)
            .First();
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

    /// <summary>
    /// "light" just follows the schedule window, so it always runs for the configured duration.
    /// Every other actuator runs only long enough to close the gap to its threshold, at
    /// EffectStrength units per second, capped by the rule's DurationSeconds as a safety limit.
    /// </summary>
    private static int ResolveDurationSeconds(ControllerAutomationRuleDto rule, double sensorValue)
    {
        if (string.Equals(rule.ActuatorCommand, "light", StringComparison.OrdinalIgnoreCase))
        {
            return Math.Max(1, rule.DurationSeconds);
        }

        var gap = rule.Condition is "GreaterThan" or "GreaterOrEqual"
            ? sensorValue - rule.Threshold
            : rule.Threshold - sensorValue;

        if (gap <= 0 || rule.ActuatorEffectStrength <= 0)
        {
            return Math.Max(1, rule.DurationSeconds);
        }

        var computedSeconds = (int)Math.Ceiling(gap / rule.ActuatorEffectStrength);
        return Math.Clamp(computedSeconds, 1, Math.Max(1, rule.DurationSeconds));
    }
}
