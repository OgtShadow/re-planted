using RePlanted.Server.Models;

namespace RePlanted.Server;

public class Plant
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Species { get; set; }
    public DateTime PlantedDate { get; set; }
    public string HealthStatus { get; set; }
    public DateTime LastWatered { get; set; }
    public Parameters Parameters { get; set; }

    public Plant()
    {
        Name = "placeholder plant name";
        Species = "placeholder species";
        PlantedDate = DateTime.UtcNow;
        HealthStatus = "Healthy";
        Parameters = new Parameters();
        LastWatered = DateTime.UtcNow;
    }

    public Plant(string Name, string Species)
    {
        this.Name = Name;
        this.Species = Species;
        PlantedDate = DateTime.UtcNow;
        HealthStatus = "Healthy";
        Parameters = new Parameters(Species);
        LastWatered = DateTime.UtcNow;
    }

    public Plant(string Name, Parameters Parameters)
    {
        this.Name = Name;
        Species = "custom species";
        this.PlantedDate = DateTime.UtcNow;
        HealthStatus = "Healthy";
        this.Parameters = Parameters;
        LastWatered = DateTime.UtcNow;
    }

    public void Water()
    {
        LastWatered = DateTime.UtcNow;
    }

    public bool NeedsWater()
    {
        return (DateTime.Now - LastWatered).TotalDays >= Parameters.WateringIntervalDays;
    }
} 