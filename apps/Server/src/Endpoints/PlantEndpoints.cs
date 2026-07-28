using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RePlanted.Server.Contracts.Plants;
using RePlanted.Server.Data;
using RePlanted.Server.Models;
using Server.Hubs;

namespace RePlanted.Server.Endpoints;

public static class PlantEndpoints
{
    public static IEndpointRouteBuilder MapPlantEndpoints(this IEndpointRouteBuilder app)
    {
        var plants = app.MapGroup("/api/plants").WithTags("Plants");

        plants.MapGet("", async (AppDbContext db) =>
            await db.Plants.Include(p => p.Parameters).ToListAsync())
            .WithSummary("Get all plants")
            .WithDescription("Returns all plants with their parameter settings.")
            .Produces<List<Plant>>(StatusCodes.Status200OK);

        plants.MapGet("/{id:int}", async (int id, AppDbContext db) =>
        {
            var plant = await db.Plants.Include(p => p.Parameters).FirstOrDefaultAsync(p => p.Id == id);
            return plant is not null ? Results.Ok(plant) : Results.NotFound();
        })
            .WithSummary("Get plant by ID")
            .WithDescription("Returns a single plant when it exists.")
            .Produces<Plant>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        plants.MapPost("", async (UpsertPlantRequest request, AppDbContext db, IHubContext<PlantHub> hubContext) =>
        {
            var newPlant = MapToPlant(request);
            NormalizePlantDates(newPlant);
            db.Plants.Add(newPlant);
            await db.SaveChangesAsync();
            Console.WriteLine($"Dodano roślinę: {newPlant.Name}, {newPlant.Species}");

            await hubContext.Clients.All.SendAsync("PlantsUpdated");

            return Results.Ok(new { Response = $"Dodano roślinę: {newPlant.Name}, {newPlant.Species}" });
        })
            .WithSummary("Create plant")
            .WithDescription("Creates a new plant and broadcasts PlantsUpdated to SignalR clients.")
            .Accepts<UpsertPlantRequest>("application/json")
            .Produces(StatusCodes.Status200OK);

        plants.MapPut("/{id:int}", async (int id, UpsertPlantRequest request, AppDbContext db, IHubContext<PlantHub> hubContext) =>
        {
            var plant = await db.Plants.Include(p => p.Parameters).FirstOrDefaultAsync(p => p.Id == id);
            if (plant is null) return Results.NotFound();

            var updatedPlant = MapToPlant(request);
            NormalizePlantDates(updatedPlant);

            plant.Name = updatedPlant.Name;
            plant.Species = updatedPlant.Species;
            plant.PlantedDate = updatedPlant.PlantedDate;
            plant.LastWatered = updatedPlant.LastWatered;

            if (plant.Parameters != null && updatedPlant.Parameters != null)
            {
                plant.Parameters.Temperature = updatedPlant.Parameters.Temperature;
                plant.Parameters.Humidity = updatedPlant.Parameters.Humidity;
                plant.Parameters.WateringIntervalDays = updatedPlant.Parameters.WateringIntervalDays;
                plant.Parameters.LightHoursPerDay = updatedPlant.Parameters.LightHoursPerDay;
            }
            else if (updatedPlant.Parameters != null)
            {
                plant.Parameters = updatedPlant.Parameters;
            }

            await db.SaveChangesAsync();
            await hubContext.Clients.All.SendAsync("PlantsUpdated");

            return Results.Ok(new { Response = $"Zaktualizowano roślinę: {plant.Name}" });
        })
            .WithSummary("Update plant")
            .WithDescription("Updates existing plant data and broadcasts PlantsUpdated.")
            .Accepts<UpsertPlantRequest>("application/json")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        plants.MapDelete("/{id:int}", async (int id, AppDbContext db, IHubContext<PlantHub> hubContext) =>
        {
            var plant = await db.Plants.FindAsync(id);
            if (plant is null) return Results.NotFound();

            db.Plants.Remove(plant);
            await db.SaveChangesAsync();
            await hubContext.Clients.All.SendAsync("PlantsUpdated");

            return Results.Ok(new { Response = $"Usunięto roślinę: {plant.Name}", Id = plant.Id });
        })
            .WithSummary("Delete plant")
            .WithDescription("Deletes a plant and broadcasts PlantsUpdated.")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }

    private static Plant MapToPlant(UpsertPlantRequest request)
    {
        var species = string.IsNullOrWhiteSpace(request.Species) ? "Unknown species" : request.Species.Trim();

        return new Plant
        {
            Name = string.IsNullOrWhiteSpace(request.Name) ? "Unnamed plant" : request.Name.Trim(),
            Species = species,
            PlantedDate = request.PlantedDate ?? DateTime.UtcNow,
            HealthStatus = string.IsNullOrWhiteSpace(request.HealthStatus) ? "Healthy" : request.HealthStatus.Trim(),
            LastWatered = request.LastWatered ?? DateTime.UtcNow,
            Parameters = request.Parameters ?? new Parameters(species)
        };
    }

    private static void NormalizePlantDates(Plant plant)
    {
        plant.PlantedDate = EnsureUtc(plant.PlantedDate);
        plant.LastWatered = EnsureUtc(plant.LastWatered);
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }
}
