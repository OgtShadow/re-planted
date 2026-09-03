using RePlanted.Server.Contracts.AutomationRules;
using RePlanted.Server.Contracts.Devices;
using RePlanted.Server.Contracts.Plants;
using RePlanted.Server.Models;

namespace RePlanted.Server.Contracts;

public static class ResponseMappings
{
    public static AutomationRuleResponse ToResponse(this AutomationRule rule)
    {
        return new AutomationRuleResponse
        {
            Id = rule.Id,
            UserId = rule.UserId,
            PlantId = rule.PlantId,
            PlantName = rule.Plant?.Name ?? string.Empty,
            SensorDeviceId = rule.SensorDeviceId,
            SensorDeviceName = rule.SensorDevice?.Name ?? string.Empty,
            SensorField = rule.SensorField,
            Condition = rule.Condition,
            Threshold = rule.Threshold,
            ActuatorDeviceId = rule.ActuatorDeviceId,
            ActuatorDeviceName = rule.ActuatorDevice?.Name ?? string.Empty,
            ActuatorExternalDeviceId = rule.ActuatorDevice?.ExternalDeviceId ?? string.Empty,
            ActuatorTargetParameter = rule.ActuatorDevice?.TargetParameter ?? string.Empty,
            ActuatorEffectType = rule.ActuatorDevice?.EffectType ?? string.Empty,
            ActuatorEffectStrength = rule.ActuatorDevice?.EffectStrength ?? 0,
            Action = rule.Action,
            DurationSeconds = rule.DurationSeconds,
            ScheduleStartTime = rule.ScheduleStartTime,
            ScheduleEndTime = rule.ScheduleEndTime,
            Priority = rule.Priority,
            CooldownMinutes = rule.CooldownMinutes,
            Status = rule.Status,
            LastTriggeredUtc = rule.LastTriggeredUtc,
            CreatedAtUtc = rule.CreatedAtUtc,
            UpdatedAtUtc = rule.UpdatedAtUtc
        };
    }

    public static DeviceResponse ToResponse(this ActuatorDevice device, bool includePlants)
    {
        var isSensor = string.Equals(device.DeviceKind, "sensor", StringComparison.OrdinalIgnoreCase);

        return new DeviceResponse
        {
            Id = device.Id,
            UserId = device.UserId,
            Name = device.Name,
            DeviceKind = device.DeviceKind,
            TargetParameter = isSensor || string.IsNullOrWhiteSpace(device.TargetParameter) ? null : device.TargetParameter,
            SensorFields = device.SensorFields ?? [],
            ExternalDeviceId = device.ExternalDeviceId,
            EffectType = device.EffectType,
            EffectStrength = device.EffectStrength,
            IsEnabled = device.IsEnabled,
            Plants = includePlants
                ? device.Plants.Select(plant => new DevicePlantSummaryResponse
                {
                    Id = plant.Id,
                    Name = plant.Name,
                    Species = plant.Species
                }).ToList()
                : []
        };
    }

    public static PlantResponse ToResponse(this Plant plant)
    {
        return new PlantResponse
        {
            Id = plant.Id,
            UserId = plant.UserId,
            Name = plant.Name,
            Species = plant.Species,
            ImageUrl = plant.ImageUrl,
            PlantedDate = plant.PlantedDate,
            HealthStatus = plant.HealthStatus,
            LastWatered = plant.LastWatered,
            Parameters = plant.Parameters,
            Devices = plant.Devices.Select(device => device.ToResponse(includePlants: false)).ToList()
        };
    }
}
