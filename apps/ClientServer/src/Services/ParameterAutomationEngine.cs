using ClientServer.Contracts;

namespace ClientServer.Services;

public sealed record ParameterRangeIssue(
    int PlantId,
    string PlantName,
    string Parameter,
    double Value,
    double Min,
    double Max,
    string SensorDeviceId);

public sealed record ParameterControlAction(
    int PlantId,
    string PlantName,
    string Parameter,
    int ActuatorDeviceId,
    string ActuatorExternalDeviceId,
    string Command,
    bool State,
    int DurationSeconds);

public sealed record ParameterAutomationEvaluation(
    IReadOnlyList<ParameterControlAction> Actions,
    IReadOnlyList<ParameterRangeIssue> UnhandledRanges,
    IReadOnlySet<string> OutOfRangeKeys);

public interface IParameterAutomationEngine
{
    ParameterAutomationEvaluation Evaluate(
        ControllerTopologyDto topology,
        ControllerTelemetryDto telemetry,
        int fallbackDurationSeconds,
        int maximumDurationSeconds);
}

public sealed class ParameterAutomationEngine : IParameterAutomationEngine
{
    private sealed record Metric(string Name, string SensorField, string ActuatorTarget, double Min, double Max, double Value);

    public ParameterAutomationEvaluation Evaluate(
        ControllerTopologyDto topology,
        ControllerTelemetryDto telemetry,
        int fallbackDurationSeconds,
        int maximumDurationSeconds)
    {
        var actions = new List<ParameterControlAction>();
        var unhandledRanges = new List<ParameterRangeIssue>();
        var outOfRangeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var plant in topology.Plants)
        {
            var sensor = plant.Devices.FirstOrDefault(device =>
                string.Equals(device.DeviceKind, "sensor", StringComparison.OrdinalIgnoreCase) &&
                MatchesTelemetryDevice(device, telemetry) &&
                device.SensorFields.Any(field => IsSupportedField(field, "temperature") || IsSupportedField(field, "humidity")));

            if (sensor is null)
            {
                continue;
            }

            var metrics = new[]
            {
                new Metric("temperature", "temperature", "temperature", plant.Parameters.TemperatureMin, plant.Parameters.TemperatureMax, telemetry.Temperature / 10d),
                new Metric("humidity", "humidity", "humidity", plant.Parameters.HumidityMin, plant.Parameters.HumidityMax, telemetry.Humidity / 10d)
            };

            foreach (var metric in metrics)
            {
                if (!sensor.SensorFields.Any(field => IsSupportedField(field, metric.SensorField)) ||
                    telemetry.AvailableSensorFields is not null && !telemetry.AvailableSensorFields.Any(field => IsSupportedField(field, metric.SensorField)) ||
                    metric.Min > metric.Max ||
                    metric.Value >= metric.Min && metric.Value <= metric.Max)
                {
                    continue;
                }

                var isBelowRange = metric.Value < metric.Min;
                var rangeKey = $"{plant.Id}:{metric.Name}";
                outOfRangeKeys.Add(rangeKey);

                var matchingActuators = plant.Devices
                    .Where(device => IsCompatibleActuator(device, metric.ActuatorTarget, isBelowRange))
                    .ToList();

                if (matchingActuators.Count == 0)
                {
                    unhandledRanges.Add(new ParameterRangeIssue(
                        plant.Id,
                        plant.Name,
                        metric.Name,
                        metric.Value,
                        metric.Min,
                        metric.Max,
                        sensor.ExternalDeviceId));
                    continue;
                }

                foreach (var actuator in matchingActuators)
                {
                    var effectType = actuator.EffectType.Trim();
                    var state = string.Equals(effectType, "set", StringComparison.OrdinalIgnoreCase)
                        ? isBelowRange
                        : true;
                    var gap = isBelowRange ? metric.Min - metric.Value : metric.Value - metric.Max;
                    var durationSeconds = state
                        ? ResolveDurationSeconds(gap, actuator.EffectStrength, fallbackDurationSeconds, maximumDurationSeconds)
                        : 0;

                    actions.Add(new ParameterControlAction(
                        plant.Id,
                        plant.Name,
                        metric.Name,
                        actuator.Id,
                        actuator.ExternalDeviceId,
                        string.Equals(metric.ActuatorTarget, "temperature", StringComparison.OrdinalIgnoreCase) ? "light" : "pump",
                        state,
                        durationSeconds));
                }
            }
        }

        return new ParameterAutomationEvaluation(actions, unhandledRanges, outOfRangeKeys);
    }

    private static bool MatchesTelemetryDevice(ControllerDeviceDto device, ControllerTelemetryDto telemetry)
    {
        if (string.IsNullOrWhiteSpace(device.ExternalDeviceId))
        {
            return false;
        }

        return string.Equals(device.ExternalDeviceId, telemetry.DeviceId, StringComparison.OrdinalIgnoreCase) ||
            string.Equals($"{device.ExternalDeviceId}-client-{telemetry.ClientId}", telemetry.DeviceId, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCompatibleActuator(ControllerDeviceDto device, string parameter, bool isBelowRange)
    {
        if (!device.IsEnabled ||
            !string.Equals(device.DeviceKind, "actuator", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(device.ExternalDeviceId) ||
            !string.Equals(device.TargetParameter, parameter, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var effectType = device.EffectType.Trim();
        return string.Equals(effectType, "set", StringComparison.OrdinalIgnoreCase) ||
            isBelowRange && string.Equals(effectType, "increase", StringComparison.OrdinalIgnoreCase) ||
            !isBelowRange && string.Equals(effectType, "decrease", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSupportedField(string field, string expected)
    {
        return string.Equals(field.Trim(), expected, StringComparison.OrdinalIgnoreCase);
    }

    private static int ResolveDurationSeconds(double gap, double effectStrength, int fallbackDurationSeconds, int maximumDurationSeconds)
    {
        var requestedSeconds = effectStrength > 0
            ? (int)Math.Ceiling(gap / effectStrength)
            : Math.Max(1, fallbackDurationSeconds);

        return Math.Clamp(requestedSeconds, 1, Math.Max(1, maximumDurationSeconds));
    }
}