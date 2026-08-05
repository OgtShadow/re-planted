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

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
}