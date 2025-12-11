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

    public Plant(string name, string species)
    {
        Name = Name;
        Species = Species;
        this.PlantedDate = PlantedDate;
        HealthStatus = "Healthy";
        Parameters = new Parameters(species);
    }

    public Plant(string name, Parameters parameters)
    {
        Name = name;
        Species = "custom species";
        PlantedDate = DateTime.Now;
        HealthStatus = "Healthy";
        Parameters = parameters;
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