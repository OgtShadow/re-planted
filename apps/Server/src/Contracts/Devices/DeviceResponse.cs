using System.Text.Json.Serialization;

namespace RePlanted.Server.Contracts.Devices;

public sealed class DevicePlantSummaryResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Species { get; set; } = string.Empty;
}

public sealed class DeviceResponse
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DeviceKind { get; set; } = "actuator";

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TargetParameter { get; set; }

    public List<string> SensorFields { get; set; } = [];
    public string ExternalDeviceId { get; set; } = string.Empty;
    public string EffectType { get; set; } = "increase";
    public double EffectStrength { get; set; } = 1;
    public bool IsEnabled { get; set; } = true;
    public List<DevicePlantSummaryResponse> Plants { get; set; } = [];
}
