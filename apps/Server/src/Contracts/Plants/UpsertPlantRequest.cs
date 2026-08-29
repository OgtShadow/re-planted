using RePlanted.Server.Models;

namespace RePlanted.Server.Contracts.Plants;

public class UpsertPlantRequest
{
    public string? Name { get; set; }
    public string? Species { get; set; }
    public string? ImageUrl { get; set; }
    public DateTime? PlantedDate { get; set; }
    public string? HealthStatus { get; set; }
    public DateTime? LastWatered { get; set; }
    public Parameters? Parameters { get; set; }
}
