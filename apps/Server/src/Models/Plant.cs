namespace RePlanted.Server.Models;

public class Plant
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public User? User { get; set; }
    public List<ActuatorDevice> Devices { get; set; }
    public string Name { get; set; }
    public string Species { get; set; }
    public DateTime PlantedDate { get; set; }
    public string HealthStatus { get; set; }
    public DateTime LastWatered { get; set; }
    public Parameters Parameters { get; set; }

    public Plant()
    {
        Devices = new List<ActuatorDevice>();
        Name = "placeholder plant name";
        Species = "placeholder species";
        PlantedDate = DateTime.UtcNow;
        HealthStatus = "Healthy";
        Parameters = new Parameters();
        LastWatered = DateTime.UtcNow;
    }

    public Plant(string name, string species)
    {
        Devices = new List<ActuatorDevice>();
        Name = name;
        Species = species;
        PlantedDate = DateTime.UtcNow;
        HealthStatus = "Healthy";
        Parameters = new Parameters(species);
        LastWatered = DateTime.UtcNow;
    }

    public Plant(string name, Parameters parameters)
    {
        Devices = new List<ActuatorDevice>();
        Name = name;
        Species = "custom species";
        PlantedDate = DateTime.UtcNow;
        HealthStatus = "Healthy";
        Parameters = parameters;
        LastWatered = DateTime.UtcNow;
    }

    public void Water()
    {
        LastWatered = DateTime.UtcNow;
    }

    public bool NeedsWater()
    {
        return (DateTime.UtcNow - LastWatered).TotalDays >= Parameters.WateringIntervalDays;
    }
}
