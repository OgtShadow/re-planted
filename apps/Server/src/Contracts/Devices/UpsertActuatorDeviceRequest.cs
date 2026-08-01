namespace RePlanted.Server.Contracts.Devices;

public class UpsertActuatorDeviceRequest
{
    public string? Name { get; set; }
    public string? GoCommand { get; set; }
    public string? TargetParameter { get; set; }
    public string? EffectType { get; set; }
    public double? EffectStrength { get; set; }
    public bool? IsEnabled { get; set; }
}
