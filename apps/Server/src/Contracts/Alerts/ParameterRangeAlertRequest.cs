namespace RePlanted.Server.Contracts.Alerts;

public sealed class ParameterRangeAlertRequest
{
    public int PlantId { get; set; }
    public string? Parameter { get; set; }
    public double Value { get; set; }
    public double Min { get; set; }
    public double Max { get; set; }
    public string? SensorDeviceId { get; set; }
}