using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RePlanted.Server;
using RePlanted.Server.Data;
using Server.Hubs;

namespace RePlanted.Server.Endpoints;

public static class PlantEndpoints
{
    public static IEndpointRouteBuilder MapPlantEndpoints(this IEndpointRouteBuilder app)
    {
        var plants = app.MapGroup("/api/plants");

        plants.MapGet("", async (AppDbContext db) =>
            await db.Plants.Include(p => p.Parameters).ToListAsync());

        plants.MapGet("/{id:int}", async (int id, AppDbContext db) =>
        {
            var plant = await db.Plants.Include(p => p.Parameters).FirstOrDefaultAsync(p => p.Id == id);
            return plant is not null ? Results.Ok(plant) : Results.NotFound();
        });

        plants.MapPost("", async (Plant newPlant, AppDbContext db, IHubContext<PlantHub> hubContext) =>
        {
            NormalizePlantDates(newPlant);
            db.Plants.Add(newPlant);
            await db.SaveChangesAsync();
            Console.WriteLine($"Dodano roślinę: {newPlant.Name}, {newPlant.Species}");

            await hubContext.Clients.All.SendAsync("PlantsUpdated");

            return Results.Ok(new { Response = $"Dodano roślinę: {newPlant.Name}, {newPlant.Species}" });
        });

        plants.MapPut("/{id:int}", async (int id, Plant updatedPlant, AppDbContext db, IHubContext<PlantHub> hubContext) =>
        {
            var plant = await db.Plants.Include(p => p.Parameters).FirstOrDefaultAsync(p => p.Id == id);
            if (plant is null) return Results.NotFound();

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
        });

        plants.MapDelete("/{id:int}", async (int id, AppDbContext db, IHubContext<PlantHub> hubContext) =>
        {
            var plant = await db.Plants.FindAsync(id);
            if (plant is null) return Results.NotFound();

            db.Plants.Remove(plant);
            await db.SaveChangesAsync();
            await hubContext.Clients.All.SendAsync("PlantsUpdated");

            return Results.Ok(new { Response = $"Usunięto roślinę: {plant.Name}" });
        });

        return app;
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
