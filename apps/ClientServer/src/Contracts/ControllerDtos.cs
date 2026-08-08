namespace ClientServer.Contracts;

/// <summary>Represents plant control thresholds and target ranges used by the IoT Controller.</summary>
public sealed record ControllerPlantParametersDto(
    int WateringIntervalDays,
    int HumidityMin,
    int HumidityMax,
    int LightHoursPerDay,
    int TemperatureMin,
    int TemperatureMax);

/// <summary>Represents a single actuator device assigned to a plant.</summary>
public sealed record ControllerDeviceDto(
    int Id,
    string Name,
    string DeviceKind,
    string TargetParameter,
    IReadOnlyList<string> SensorFields,
    string ExternalDeviceId,
    string EffectType,
    double EffectStrength,
    bool IsEnabled);

/// <summary>Represents a plant synchronized from the main server.</summary>
public sealed record ControllerPlantDto(
    int Id,
    string Name,
    string Species,
    ControllerPlantParametersDto Parameters,
    IReadOnlyList<ControllerDeviceDto> Devices);

/// <summary>Represents the synchronized topology for one ClientId.</summary>
public sealed record ControllerTopologyDto(
    int ClientId,
    DateTime SyncedAtUtc,
    IReadOnlyList<ControllerPlantDto> Plants);

/// <summary>Represents the latest aggregated telemetry snapshot produced by the IoT Controller.</summary>
public sealed record ControllerTelemetryDto(
    string DeviceId,
    int SoilMoistureAnalog,
    int Temperature,
    int Humidity,
    int WaterLevelCm,
    bool PumpState,
    bool LampState,
    DateTime Timestamp,
    int ClientId,
    string ControllerState,
    string? ActivePlantName,
    string? WarningMessage,
    DateTime LastSyncUtc);

/// <summary>Represents the runtime status of the IoT Controller for a given client.</summary>
public sealed record ControllerStatusDto(
    int ClientId,
    string ControllerState,
    bool IsInSoak,
    DateTime? SoakUntilUtc,
    DateTime LastSyncUtc,
    string? WarningMessage,
    int MonitoredPlants);

public sealed record PlantOverviewDto(
    int Id,
    string Name,
    string Species,
    ControllerPlantParametersDto Parameters,
    IReadOnlyList<ControllerDeviceDto> Devices);

public sealed record TelemetryPayload(
    string DeviceId,
    string SourceType,
    int? SoilMoisture,
    int? LightLevel,
    int? Temperature,
    int? Humidity,
    int? WaterLevel,
    bool? WaterLevelOk,
    bool? PumpState,
    bool? LampState,
    DateTime TimestampUtc);

public sealed record CommandPayload(
    string DeviceId,
    string Command,
    bool State,
    int DurationMs,
    DateTime RequestedAtUtc);

public sealed record PumpCommandRequest(int DurationMs);