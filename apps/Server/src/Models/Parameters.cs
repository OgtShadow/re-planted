namespace RePlanted.Server.Models;

public record Range(int Min, int Max);

public class Parameters
{
    public int Id { get; set; }
    public int WateringIntervalDays { get; set; }
    public Range Humidity { get; set; }
    public int LightHoursPerDay { get; set; }
    public Range Temperature { get; set; }

    /// <summary>Daily time-of-day window in which automation is allowed to turn the light on (e.g. to avoid disturbing sleep).</summary>
    public TimeSpan? LightScheduleStart { get; set; }
    public TimeSpan? LightScheduleEnd { get; set; }

    public Parameters()
    {
        WateringIntervalDays = 0;
        Humidity = new Range(0, 100);
        LightHoursPerDay = 0;
        Temperature = new Range(0, 100);
    }

    public Parameters(string species)
    {
        WateringIntervalDays = 3;
        Humidity = new Range(30, 70);
        LightHoursPerDay = 6;
        Temperature = new Range(15, 25);
    }

    public Parameters(int wateringIntervalDays, Range humidity, int lightHoursPerDay, Range temperature)
    {
        WateringIntervalDays = wateringIntervalDays;
        Humidity = humidity;
        LightHoursPerDay = lightHoursPerDay;
        Temperature = temperature;
    }
}
