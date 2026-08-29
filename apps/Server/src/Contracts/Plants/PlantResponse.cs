using RePlanted.Server.Contracts.Devices;
using RePlanted.Server.Models;

namespace RePlanted.Server.Contracts.Plants;

public sealed class PlantResponse
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Species { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public DateTime PlantedDate { get; set; }
    public string HealthStatus { get; set; } = string.Empty;
    public DateTime LastWatered { get; set; }
    public Parameters Parameters { get; set; } = new();
    public List<DeviceResponse> Devices { get; set; } = [];
}
