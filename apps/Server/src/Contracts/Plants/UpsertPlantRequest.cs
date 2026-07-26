using RePlanted.Server.Models;

namespace RePlanted.Server.Contracts.Plants;

public record UpsertPlantRequest(
    string Name,
    string Species,
    DateTime PlantedDate,
    string HealthStatus,
    DateTime LastWatered,
    Parameters Parameters
);
