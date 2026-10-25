namespace ClientServer.Contracts;

/// <summary>Represents plant control thresholds and target ranges used by the IoT Controller.</summary>
public sealed record ControllerPlantParametersDto(
    int WateringIntervalDays,
    int HumidityMin,
    int HumidityMax,
    int LightHoursPerDay,
    int TemperatureMin,
    int TemperatureMax,
    int SoilMoistureMin = 300,
    int SoilMoistureMax = 700,
    TimeSpan? LightScheduleStart = null,
    TimeSpan? LightScheduleEnd = null);

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
    IReadOnlyList<ControllerDeviceDto> Devices,
    DateTime? LastWateredUtc = null);

/// <summary>Represents the synchronized topology for one ClientId.</summary>
public sealed record ControllerTopologyDto(
    int ClientId,
    DateTime SyncedAtUtc,
    IReadOnlyList<ControllerPlantDto> Plants);

/// <summary>Atomic, locally persisted controller configuration used during main-server outages.</summary>
public sealed record ControllerConfigurationSnapshot(
    int ClientId,
    string Version,
    DateTime FetchedAtUtc,
    DateTime ExpiresAtUtc,
    ControllerTopologyDto Topology);

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
    DateTime LastSyncUtc,
    IReadOnlyList<string>? AvailableSensorFields = null);

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

public sealed record ActuatorCommandRequest(
    string Command,
    bool State,
    int DurationMs);

/// <summary>Handshake payload sent by an ESP32 node on first contact with the discovery topic.</summary>
public sealed record DeviceRegistrationRequest(
    string DeviceId,
    IReadOnlyList<string> Capabilities);

/// <summary>Base offline schedule persisted to ESP32 non-volatile memory to sustain Fail-safe mode.</summary>
public sealed record DeviceOfflineScheduleDto(
    int WateringIntervalHours,
    int SoilMoistureMinThreshold,
    int SoilMoistureMaxThreshold,
    int LightOnHour,
    int LightOffHour,
    int TelemetryIntervalSeconds);

/// <summary>Registration acknowledgement published back to the node's config topic.</summary>
public sealed record DeviceRegistrationAckDto(
    string DeviceId,
    string Status,
    IReadOnlyList<string> Capabilities,
    DeviceOfflineScheduleDto Schedule,
    DateTime IssuedAtUtc);

/// <summary>Represents a device currently known to the IoT Controller's in-memory registry.</summary>
public sealed record RegisteredDeviceDto(
    string DeviceId,
    IReadOnlyList<string> Capabilities,
    DateTime FirstSeenUtc,
    DateTime LastSeenUtc);
