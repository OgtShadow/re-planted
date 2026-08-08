namespace RePlanted.Server.Contracts.Devices;

public class UpsertSensorDeviceRequest
{
    public string? Name { get; set; }
    public List<string>? SensorFields { get; set; }
    public string? ExternalDeviceId { get; set; }
    public bool? IsEnabled { get; set; }
}
