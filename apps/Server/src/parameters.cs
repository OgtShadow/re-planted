namespace RePlanted.Server.Models;

public record Range(int Min, int Max);

public class Parameters
{
    public int Id { get; set; } // Primary Key
    public int WateringIntervalDays { get; set; }
    public Range Humidity { get; set; }
    public int LightHoursPerDay { get; set; }
    public Range Temperature { get; set; }

    public Parameters()
    {
        WateringIntervalDays = 0;
        Humidity = new Range(0, 100);
        LightHoursPerDay = 0;
        Temperature = new Range(0, 100);
    }

    public Parameters(string species)
    {
        // Placeholder: In a real application, parameters would be fetched based on species
        WateringIntervalDays = 3;
        Humidity = new Range(30, 70);
        LightHoursPerDay = 6;
        Temperature = new Range(15, 25);
    }

    public Parameters(int WateringIntervalDays, Range Humidity, int LightHoursPerDay, Range Temperature)
    {
        this.WateringIntervalDays = WateringIntervalDays;
        this.Humidity = Humidity;
        this.LightHoursPerDay = LightHoursPerDay;
        this.Temperature = Temperature;
    }
}
