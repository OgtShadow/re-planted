using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RePlanted.Server.Contracts;
using RePlanted.Server.Contracts.Plants;
using RePlanted.Server.Data;
using RePlanted.Server.Models;
using Server.Hubs;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace RePlanted.Server.Endpoints;

public static class PlantEndpoints
{
    public static IEndpointRouteBuilder MapPlantEndpoints(this IEndpointRouteBuilder app)
    {
        var plants = app.MapGroup("/api/users/{userId:int}/plants")
            .WithTags("Plants")
            .RequireAuthorization();

        plants.MapGet("", async (int userId, ClaimsPrincipal principal, AppDbContext db) =>
        {
            if (!IsRequestUserAuthorized(principal, userId))
            {
                return Results.Forbid();
            }

            var plantsForUser = await db.Plants
                .Include(p => p.Parameters)
                .Include(p => p.Devices)
                .Where(p => p.UserId == userId)
                .ToListAsync();

            return Results.Ok(plantsForUser.Select(plant => plant.ToResponse()).ToList());
        })
            .WithSummary("Get all plants for user")
            .WithDescription("Returns all plants assigned to the specified user.")
            .Produces<List<PlantResponse>>(StatusCodes.Status200OK);

        plants.MapGet("/{id:int}", async (int userId, int id, ClaimsPrincipal principal, AppDbContext db) =>
        {
            if (!IsRequestUserAuthorized(principal, userId))
            {
                return Results.Forbid();
            }

            var plant = await db.Plants
                .Include(p => p.Parameters)
                .Include(p => p.Devices)
                .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);
            return plant is not null ? Results.Ok(plant.ToResponse()) : Results.NotFound();
        })
            .WithSummary("Get user plant by ID")
            .WithDescription("Returns a plant only when it belongs to the specified user.")
            .Produces<PlantResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        plants.MapPost("", async (int userId, UpsertPlantRequest request, ClaimsPrincipal principal, AppDbContext db, IHubContext<PlantHub> hubContext) =>
        {
            if (!IsRequestUserAuthorized(principal, userId))
            {
                return Results.Forbid();
            }

            var userExists = await db.Users.AnyAsync(u => u.Id == userId);
            if (!userExists)
            {
                return Results.NotFound(new { Response = $"Nie znaleziono użytkownika o id={userId}" });
            }

            var newPlant = MapToPlant(request);
            NormalizePlantDates(newPlant);
            newPlant.UserId = userId;
            db.Plants.Add(newPlant);
            await db.SaveChangesAsync();
            Console.WriteLine($"Dodano roślinę: {newPlant.Name}, {newPlant.Species}");

            await hubContext.Clients.All.SendAsync("PlantsUpdated");

            return Results.Ok(new
            {
                Response = $"Dodano roślinę: {newPlant.Name}, {newPlant.Species}",
                Id = newPlant.Id,
                UserId = newPlant.UserId
            });
        })
            .WithSummary("Create plant for user")
            .WithDescription("Creates a new plant for a specific user and broadcasts PlantsUpdated to SignalR clients.")
            .Accepts<UpsertPlantRequest>("application/json")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        plants.MapPut("/{id:int}", async (int userId, int id, UpsertPlantRequest request, ClaimsPrincipal principal, AppDbContext db, IHubContext<PlantHub> hubContext) =>
        {
            if (!IsRequestUserAuthorized(principal, userId))
            {
                return Results.Forbid();
            }

            var plant = await db.Plants
                .Include(p => p.Parameters)
                .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);
            if (plant is null) return Results.NotFound();

            var updatedPlant = MapToPlant(request);
            NormalizePlantDates(updatedPlant);

            plant.Name = updatedPlant.Name;
            plant.Species = updatedPlant.Species;
            plant.ImageUrl = updatedPlant.ImageUrl;
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

            return Results.Ok(new { Response = $"Zaktualizowano roślinę: {plant.Name}", Id = plant.Id, UserId = plant.UserId });
        })
            .WithSummary("Update user plant")
            .WithDescription("Updates plant data only when the plant belongs to the specified user.")
            .Accepts<UpsertPlantRequest>("application/json")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        plants.MapDelete("/{id:int}", async (int userId, int id, ClaimsPrincipal principal, AppDbContext db, IHubContext<PlantHub> hubContext) =>
        {
            if (!IsRequestUserAuthorized(principal, userId))
            {
                return Results.Forbid();
            }

            var plant = await db.Plants.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);
            if (plant is null) return Results.NotFound();

            db.Plants.Remove(plant);
            await db.SaveChangesAsync();
            await hubContext.Clients.All.SendAsync("PlantsUpdated");

            return Results.Ok(new { Response = $"Usunięto roślinę: {plant.Name}", Id = plant.Id, UserId = userId });
        })
            .WithSummary("Delete user plant")
            .WithDescription("Deletes a plant only when it belongs to the specified user and broadcasts PlantsUpdated.")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }

    private static bool IsRequestUserAuthorized(ClaimsPrincipal principal, int routeUserId)
    {
        var claimValue = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub);

        return int.TryParse(claimValue, out var claimUserId) && claimUserId == routeUserId;
    }

    private static Plant MapToPlant(UpsertPlantRequest request)
    {
        var species = string.IsNullOrWhiteSpace(request.Species) ? "Unknown species" : request.Species.Trim();

        return new Plant
        {
            Name = string.IsNullOrWhiteSpace(request.Name) ? "Unnamed plant" : request.Name.Trim(),
            Species = species,
            ImageUrl = string.IsNullOrWhiteSpace(request.ImageUrl) ? null : request.ImageUrl.Trim(),
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
