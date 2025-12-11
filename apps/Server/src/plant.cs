using RePlanted.Server.Models;

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
        HealthStatus = "Healthy";
        Parameters = new Parameters();
    }

    public Plant(string Name, string Species)
    {
        this.Name = Name;
        this.Species = Species;
        this.PlantedDate = PlantedDate;
        HealthStatus = "Healthy";
        Parameters = new Parameters(species);
    }

    public Plant(string Name, Parameters Parameters)
    {
        this.Name = Name;
        Species = "custom species";
        this.PlantedDate = DateTime.Now;
        HealthStatus = "Healthy";
        this.Parameters = Parameters;
    }

    public void Water()
    {
        LastWatered = DateTime.Now;
    }

    public bool NeedsWater()
    {
        return (DateTime.Now - LastWatered).TotalDays >= Parameters.WateringIntervalDays;
    }
}