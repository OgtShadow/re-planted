namespace ClientServer.Services;

public sealed class MainServerApiOptions
{
    public const string SectionName = "MainServerApi";

    public string BaseUrl { get; set; } = "http://localhost:8080";
    public string PlantsPath { get; set; } = "/api/users/{clientId}/plants";
    public string AutomationRulesPath { get; set; } = "/api/users/{clientId}/automation-rules";
    public string AutomationRuleTriggerPath { get; set; } = "/api/users/{clientId}/automation-rules/{ruleId}/trigger";
}

public sealed class MockDeviceApiOptions
{
    public const string SectionName = "MockDeviceApi";

    public string BaseUrl { get; set; } = "http://localhost:8085";
    public string SensorsPath { get; set; } = "/sensors";
}

public sealed class IoTControllerOptions
{
    public const string SectionName = "IoTController";

    public List<int> ClientIds { get; set; } = new();
    public string TelemetrySource { get; set; } = "Mock";
    public int PollingIntervalSeconds { get; set; } = 15;
    public int PumpRunSeconds { get; set; } = 2;
    public int SoakTimeSeconds { get; set; } = 60;
    public int LowWaterThresholdCm { get; set; } = 2;
    public int MoistureThresholdBufferPercent { get; set; } = 5;
    public int MaxPumpRunSeconds { get; set; } = 30;
    public int TelemetryTimeoutSeconds { get; set; } = 5;
    public int MaxTelemetryAgeSeconds { get; set; } = 30;
}

public sealed class MqttOptions
{
    public const string SectionName = "Mqtt";

    public bool Enabled { get; set; } = true;
    public string BrokerHost { get; set; } = "localhost";
    public int BrokerPort { get; set; } = 1883;
    public string ClientId { get; set; } = "re-planted-client-server";
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string TelemetryTopicFilter { get; set; } = "replanted/telemetry/+/+";
    public string CommandsTopicTemplate { get; set; } = "replanted/commands/{deviceId}";
    public string DiscoveryRegisterTopic { get; set; } = "replanted/discovery/register";
    public string NodeConfigTopicTemplate { get; set; } = "replanted/node/{deviceId}/config";
    public int KeepAliveSeconds { get; set; } = 30;
    public int ReconnectDelaySeconds { get; set; } = 5;
    public int QosLevel { get; set; } = 1;
}

/// <summary>mDNS advertisement settings so ESP32 nodes can locate the MQTT broker without manual configuration.</summary>
public sealed class MdnsOptions
{
    public const string SectionName = "Mdns";

    public bool Enabled { get; set; } = true;
    public string InstanceName { get; set; } = "replanted-iot-controller";
    public string ServiceType { get; set; } = "_mqtt._tcp";
}

/// <summary>Default offline (Fail-safe) schedule handed to newly registered ESP32 nodes.</summary>
public sealed class DeviceRegistrationOptions
{
    public const string SectionName = "DeviceRegistration";

    public int DefaultWateringIntervalHours { get; set; } = 24;
    public int DefaultSoilMoistureMinThreshold { get; set; } = 300;
    public int DefaultSoilMoistureMaxThreshold { get; set; } = 700;
    public int DefaultLightOnHour { get; set; } = 6;
    public int DefaultLightOffHour { get; set; } = 20;
    public int DefaultTelemetryIntervalSeconds { get; set; } = 60;
}

public sealed class ControllerStateBackupOptions
{
    public const string SectionName = "ControllerStateBackup";

    public bool Enabled { get; set; } = true;
    public int SaveIntervalSeconds { get; set; } = 60;
    public string FilePath { get; set; } = "data/controller-state.json";
}

public sealed class OfflineModeOptions
{
    public const string SectionName = "OfflineMode";

    public bool Enabled { get; set; } = true;
    public int SnapshotValidityMinutes { get; set; } = 1440;
    public bool ContinueWithExpiredSnapshot { get; set; }
}

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
}