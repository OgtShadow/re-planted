public class Plant
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Species { get; set; }
    public DateTime PlantedDate { get; set; }
    public string HealthStatus { get; set; }
    public DateTime LastWatered { get; set; }

    public Plant()
    {
        Name = "placeholder plant name";
        Species = "placeholder species";
        HealthStatus = "Healthy";
    }

    public Plant(string name, string species, string location)
    {
        Name = name;
        Species = species;
        PlantedDate = DateTime.Now;
        HealthStatus = "Healthy";
    }

    public void Water()
    {
        LastWatered = DateTime.Now;
    }

    public bool NeedsWater(int daysBetweenWatering = 3)
    {
        return (DateTime.Now - LastWatered).TotalDays >= daysBetweenWatering;
    }
}