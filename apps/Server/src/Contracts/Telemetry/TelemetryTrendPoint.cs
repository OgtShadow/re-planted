namespace RePlanted.Server.Contracts.Telemetry;

public sealed class TelemetryTrendPoint
{
    public DateTime BucketStartUtc { get; set; }
    public double TemperatureAvg { get; set; }
    public int TemperatureMin { get; set; }
    public int TemperatureMax { get; set; }

    public double HumidityAvg { get; set; }
    public int HumidityMin { get; set; }
    public int HumidityMax { get; set; }

    public double SoilMoistureAvg { get; set; }
    public int SoilMoistureMin { get; set; }
    public int SoilMoistureMax { get; set; }

    public double WaterLevelAvg { get; set; }
    public int WaterLevelMin { get; set; }
    public int WaterLevelMax { get; set; }

    public double LightOnMinutes { get; set; }
    public double LightOffMinutes { get; set; }
    public double LightOnPercent { get; set; }

    public int SampleCount { get; set; }
}