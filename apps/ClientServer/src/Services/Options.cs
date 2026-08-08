namespace ClientServer.Services;

public sealed class MainServerApiOptions
{
    public const string SectionName = "MainServerApi";

    public string BaseUrl { get; set; } = "http://localhost:8080";
    public string PlantsPath { get; set; } = "/api/users/{clientId}/plants";
}

public sealed class MockDeviceApiOptions
{
    public const string SectionName = "MockDeviceApi";

    public string BaseUrl { get; set; } = "http://localhost:8085";
    public string SensorsPath { get; set; } = "/sensors";
    public string PumpCommandPath { get; set; } = "/command/pump";
}

public sealed class IoTControllerOptions
{
    public const string SectionName = "IoTController";

    public List<int> ClientIds { get; set; } = new();
    public int PollingIntervalSeconds { get; set; } = 15;
    public int PumpRunSeconds { get; set; } = 2;
    public int SoakTimeSeconds { get; set; } = 60;
    public int LowWaterThresholdCm { get; set; } = 2;
    public int MoistureThresholdBufferPercent { get; set; } = 5;
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
    public int KeepAliveSeconds { get; set; } = 30;
    public int ReconnectDelaySeconds { get; set; } = 5;
    public int QosLevel { get; set; } = 1;
}

public sealed class ControllerStateBackupOptions
{
    public const string SectionName = "ControllerStateBackup";

    public bool Enabled { get; set; } = true;
    public int SaveIntervalSeconds { get; set; } = 60;
    public string FilePath { get; set; } = "data/controller-state.json";
}

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
}