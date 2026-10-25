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
    IReadOnlySet<string> OutOfRangeKeys,
    IReadOnlyList<int> WateringSkippedPlantIds);

public interface IParameterAutomationEngine
{
    ParameterAutomationEvaluation Evaluate(
        ControllerTopologyDto topology,
        ControllerTelemetryDto telemetry,
        DateTime nowUtc,
        DateTime nowLocal,
        IReadOnlyDictionary<int, DateOnly> wateringSkippedDates,
        int fallbackDurationSeconds,
        int maximumDurationSeconds);
}

public sealed class ParameterAutomationEngine : IParameterAutomationEngine
{
    private sealed record Metric(string Name, string SensorField, string ActuatorTarget, double Min, double Max, double Value);

    public ParameterAutomationEvaluation Evaluate(
        ControllerTopologyDto topology,
        ControllerTelemetryDto telemetry,
        DateTime nowUtc,
        DateTime nowLocal,
        IReadOnlyDictionary<int, DateOnly> wateringSkippedDates,
        int fallbackDurationSeconds,
        int maximumDurationSeconds)
    {
        var actions = new List<ParameterControlAction>();
        var unhandledRanges = new List<ParameterRangeIssue>();
        var outOfRangeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var wateringSkippedPlantIds = new HashSet<int>();

        foreach (var plant in topology.Plants)
        {
            var sensor = plant.Devices.FirstOrDefault(device =>
                string.Equals(device.DeviceKind, "sensor", StringComparison.OrdinalIgnoreCase) &&
                MatchesTelemetryDevice(device, telemetry) &&
                device.SensorFields.Any(field =>
                    IsSupportedField(field, "temperature") ||
                    IsSupportedField(field, "humidity") ||
                    IsSupportedField(field, "soilMoistureAnalog")));

            if (sensor is not null)
            {
                EvaluateRangeMetric(
                    plant,
                    sensor,
                    telemetry,
                    "temperature",
                    "temperature",
                    "temperature",
                    telemetry.Temperature / 10d,
                    plant.Parameters.TemperatureMin,
                    plant.Parameters.TemperatureMax,
                    "lamp",
                    actions,
                    unhandledRanges,
                    outOfRangeKeys,
                    fallbackDurationSeconds,
                    maximumDurationSeconds);

                EvaluateRangeMetric(
                    plant,
                    sensor,
                    telemetry,
                    "humidity",
                    "humidity",
                    "humidity",
                    telemetry.Humidity / 10d,
                    plant.Parameters.HumidityMin,
                    plant.Parameters.HumidityMax,
                    "pump",
                    actions,
                    unhandledRanges,
                    outOfRangeKeys,
                    fallbackDurationSeconds,
                    maximumDurationSeconds);

                EvaluateSoilMoisture(
                    plant,
                    sensor,
                    telemetry,
                    nowUtc,
                    nowLocal,
                    wateringSkippedDates,
                    actions,
                    unhandledRanges,
                    outOfRangeKeys,
                    wateringSkippedPlantIds,
                    fallbackDurationSeconds,
                    maximumDurationSeconds);
            }

        }

        AddLightScheduleActions(topology, telemetry, nowLocal, actions);

        return new ParameterAutomationEvaluation(actions, unhandledRanges, outOfRangeKeys, wateringSkippedPlantIds.ToList());
    }

    private static void EvaluateRangeMetric(
        ControllerPlantDto plant,
        ControllerDeviceDto sensor,
        ControllerTelemetryDto telemetry,
        string name,
        string sensorField,
        string actuatorTarget,
        double value,
        double min,
        double max,
        string command,
        List<ParameterControlAction> actions,
        List<ParameterRangeIssue> unhandledRanges,
        HashSet<string> outOfRangeKeys,
        int fallbackDurationSeconds,
        int maximumDurationSeconds)
    {
        if (!HasAvailableSensorField(sensor, telemetry, sensorField) || min > max || value >= min && value <= max)
        {
            return;
        }

        var isBelowRange = value < min;
        outOfRangeKeys.Add($"{plant.Id}:{name}");
        var matchingActuators = plant.Devices
            .Where(device => IsCompatibleActuator(device, actuatorTarget, isBelowRange))
            .ToList();

        if (matchingActuators.Count == 0)
        {
            unhandledRanges.Add(new ParameterRangeIssue(plant.Id, plant.Name, name, value, min, max, sensor.ExternalDeviceId));
            return;
        }

        var gap = isBelowRange ? min - value : value - max;
        foreach (var actuator in matchingActuators)
        {
            var state = !string.Equals(actuator.EffectType.Trim(), "set", StringComparison.OrdinalIgnoreCase) || isBelowRange;
            actions.Add(new ParameterControlAction(
                plant.Id,
                plant.Name,
                name,
                actuator.Id,
                actuator.ExternalDeviceId,
                command,
                state,
                state ? ResolveDurationSeconds(gap, actuator.EffectStrength, fallbackDurationSeconds, maximumDurationSeconds) : 0));
        }
    }

    private static void EvaluateSoilMoisture(
        ControllerPlantDto plant,
        ControllerDeviceDto sensor,
        ControllerTelemetryDto telemetry,
        DateTime nowUtc,
        DateTime nowLocal,
        IReadOnlyDictionary<int, DateOnly> wateringSkippedDates,
        List<ParameterControlAction> actions,
        List<ParameterRangeIssue> unhandledRanges,
        HashSet<string> outOfRangeKeys,
        HashSet<int> wateringSkippedPlantIds,
        int fallbackDurationSeconds,
        int maximumDurationSeconds)
    {
        const string parameter = "soilMoisture";
        if (!HasAvailableSensorField(sensor, telemetry, "soilMoistureAnalog"))
        {
            return;
        }

        var value = telemetry.SoilMoistureAnalog;
        var min = plant.Parameters.SoilMoistureMin;
        var max = plant.Parameters.SoilMoistureMax;
        if (min > max)
        {
            return;
        }

        var isTooDry = value < min;
        var isTooWet = value > max;
        var rangeKey = $"{plant.Id}:{parameter}";
        var wasSkippedToday = wateringSkippedDates.TryGetValue(plant.Id, out var skippedDate) &&
            skippedDate == DateOnly.FromDateTime(nowLocal);

        if (wasSkippedToday)
        {
            return;
        }

        if (isTooWet)
        {
            outOfRangeKeys.Add(rangeKey);
            wateringSkippedPlantIds.Add(plant.Id);
            return;
        }

        var intervalDays = Math.Max(1, plant.Parameters.WateringIntervalDays);
        var isDueBySchedule = plant.LastWateredUtc is null ||
            nowUtc - plant.LastWateredUtc.Value >= TimeSpan.FromDays(intervalDays);

        if (!isTooDry && !isDueBySchedule)
        {
            return;
        }

        if (isTooDry)
        {
            outOfRangeKeys.Add(rangeKey);
        }

        var matchingActuators = plant.Devices
            .Where(device => IsCompatibleActuator(device, parameter, isBelowRange: true))
            .ToList();

        if (matchingActuators.Count == 0)
        {
            if (isTooDry)
            {
                unhandledRanges.Add(new ParameterRangeIssue(plant.Id, plant.Name, parameter, value, min, max, sensor.ExternalDeviceId));
            }

            return;
        }

        var gap = isTooDry ? min - value : 0;
        foreach (var actuator in matchingActuators)
        {
            actions.Add(new ParameterControlAction(
                plant.Id,
                plant.Name,
                parameter,
                actuator.Id,
                actuator.ExternalDeviceId,
                "pump",
                true,
                ResolveDurationSeconds(gap, actuator.EffectStrength, fallbackDurationSeconds, maximumDurationSeconds)));
        }
    }

    private static void AddLightScheduleActions(
        ControllerTopologyDto topology,
        ControllerTelemetryDto telemetry,
        DateTime nowLocal,
        List<ParameterControlAction> actions)
    {
        var lightActuators = topology.Plants
            .SelectMany(plant => plant.Devices
                .Where(device => device.IsEnabled &&
                    string.Equals(device.DeviceKind, "actuator", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(device.TargetParameter, "light", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(device.ExternalDeviceId))
                .Select(device => (Plant: plant, Device: device)))
            .GroupBy(item => item.Device.ExternalDeviceId, StringComparer.OrdinalIgnoreCase);

        foreach (var actuatorGroup in lightActuators)
        {
            var activePlant = actuatorGroup.FirstOrDefault(item => IsLightOnAt(item.Plant.Parameters, nowLocal));
            var desiredState = activePlant.Plant is not null;
            if (desiredState == telemetry.LampState)
            {
                continue;
            }

            var selected = desiredState ? activePlant : actuatorGroup.First();
            actions.Add(new ParameterControlAction(
                selected.Plant.Id,
                selected.Plant.Name,
                "light",
                selected.Device.Id,
                selected.Device.ExternalDeviceId,
                "lamp",
                desiredState,
                0));
        }
    }

    private static bool IsLightOnAt(ControllerPlantParametersDto parameters, DateTime nowLocal)
    {
        var durationMinutes = Math.Clamp(parameters.LightHoursPerDay, 0, 24) * 60;
        if (durationMinutes == 0)
        {
            return false;
        }

        var startMinutes = (int)(parameters.LightScheduleStart ?? TimeSpan.FromHours(6)).TotalMinutes;
        if (parameters.LightScheduleEnd is { } allowedEnd)
        {
            var allowedMinutes = ((int)allowedEnd.TotalMinutes - startMinutes + 1440) % 1440;
            durationMinutes = Math.Min(durationMinutes, allowedMinutes == 0 ? 1440 : allowedMinutes);
        }

        var elapsedMinutes = ((int)nowLocal.TimeOfDay.TotalMinutes - startMinutes + 1440) % 1440;
        return elapsedMinutes < durationMinutes;
    }

    private static bool HasAvailableSensorField(ControllerDeviceDto sensor, ControllerTelemetryDto telemetry, string sensorField)
    {
        return sensor.SensorFields.Any(field => IsSupportedField(field, sensorField)) &&
            (telemetry.AvailableSensorFields is null || telemetry.AvailableSensorFields.Any(field => IsSupportedField(field, sensorField)));
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
        var requestedSeconds = gap <= 0
            ? Math.Max(1, fallbackDurationSeconds)
            : effectStrength > 0
            ? (int)Math.Ceiling(gap / effectStrength)
            : Math.Max(1, fallbackDurationSeconds);

        return Math.Clamp(requestedSeconds, 1, Math.Max(1, maximumDurationSeconds));
    }
}