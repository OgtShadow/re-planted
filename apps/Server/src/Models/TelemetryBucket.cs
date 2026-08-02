namespace RePlanted.Server.Models;

public class TelemetryBucket
{
    public int Id { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public DateTime BucketStartUtc { get; set; }
    public int SampleCount { get; set; }

    public double TemperatureSum { get; set; }
    public int TemperatureMin { get; set; }
    public int TemperatureMax { get; set; }

    public double HumiditySum { get; set; }
    public int HumidityMin { get; set; }
    public int HumidityMax { get; set; }

    public double SoilMoistureSum { get; set; }
    public int SoilMoistureMin { get; set; }
    public int SoilMoistureMax { get; set; }

    public double WaterLevelSum { get; set; }
    public int WaterLevelMin { get; set; }
    public int WaterLevelMax { get; set; }

    public bool LastPumpState { get; set; }
    public bool LastLampState { get; set; }
}