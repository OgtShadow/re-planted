namespace ClientServer.Contracts;

public sealed record ControllerPlantParametersDto(
    int WateringIntervalDays,
    int HumidityMin,
    int HumidityMax,
    int LightHoursPerDay,
    int TemperatureMin,
    int TemperatureMax);

public sealed record ControllerDeviceDto(
    int Id,
    string Name,
    string TargetParameter,
    string EffectType,
    double EffectStrength,
    bool IsEnabled);

public sealed record ControllerPlantDto(
    int Id,
    string Name,
    string Species,
    ControllerPlantParametersDto Parameters,
    IReadOnlyList<ControllerDeviceDto> Devices);

public sealed record ControllerTopologyDto(
    int ClientId,
    DateTime SyncedAtUtc,
    IReadOnlyList<ControllerPlantDto> Plants);

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