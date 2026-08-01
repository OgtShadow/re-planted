using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using RePlanted.Server.Contracts.Devices;
using RePlanted.Server.Data;
using RePlanted.Server.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace RePlanted.Server.Endpoints;

public static class ActuatorDeviceEndpoints
{
    private static readonly HashSet<string> SupportedGoCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "pump",
        "light"
    };

    private static readonly HashSet<string> SupportedTargetParameters = new(StringComparer.OrdinalIgnoreCase)
    {
        "soilMoistureAnalog",
        "lightIsDark",
        "temperature",
        "humidity",
        "waterLevelCm"
    };

    public static IEndpointRouteBuilder MapActuatorDeviceEndpoints(this IEndpointRouteBuilder app)
    {
        var devices = app.MapGroup("/api/users/{userId:int}/devices")
            .WithTags("ActuatorDevices")
            .RequireAuthorization();

        devices.MapGet("/catalog", (int userId, ClaimsPrincipal principal) =>
        {
            if (!IsRequestUserAuthorized(principal, userId))
            {
                return Results.Forbid();
            }

            return Results.Ok(new
            {
                commands = new[]
                {
                    new
                    {
                        key = "pump",
                        commandPath = "/command/pump",
                        stateField = "pumpState",
                        defaultTargetParameter = "soilMoistureAnalog",
                        suggestedEffectType = "increase",
                        supportedTargetParameters = new[] { "soilMoistureAnalog", "waterLevelCm", "humidity" }
                    },
                    new
                    {
                        key = "light",
                        commandPath = "/command/light",
                        stateField = "lampState",
                        defaultTargetParameter = "lightIsDark",
                        suggestedEffectType = "set",
                        supportedTargetParameters = new[] { "lightIsDark" }
                    }
                },
                sensorFields = SupportedTargetParameters.ToArray(),
                supportedEffectTypes = new[] { "increase", "decrease", "set" }
            });
        })
            .WithSummary("Get Go device catalog")
            .WithDescription("Returns supported actuator commands and sensor fields based on the Go mock service.")
            .Produces(StatusCodes.Status200OK);

        devices.MapGet("", async (int userId, ClaimsPrincipal principal, AppDbContext db) =>
        {
            if (!IsRequestUserAuthorized(principal, userId))
            {
                return Results.Forbid();
            }

            var result = await db.ActuatorDevices
                .Include(d => d.Plants)
                .Where(d => d.UserId == userId)
                .ToListAsync();

            return Results.Ok(result);
        })
            .WithSummary("Get all actuator devices for user")
            .WithDescription("Returns all actuator devices owned by the user and assigned plants.")
            .Produces<List<ActuatorDevice>>(StatusCodes.Status200OK);

        devices.MapGet("/{id:int}", async (int userId, int id, ClaimsPrincipal principal, AppDbContext db) =>
        {
            if (!IsRequestUserAuthorized(principal, userId))
            {
                return Results.Forbid();
            }

            var device = await db.ActuatorDevices
                .Include(d => d.Plants)
                .FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId);

            return device is not null ? Results.Ok(device) : Results.NotFound();
        })
            .WithSummary("Get actuator device by ID")
            .WithDescription("Returns a single actuator device only when it belongs to the selected user.")
            .Produces<ActuatorDevice>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        devices.MapPost("", async (int userId, UpsertActuatorDeviceRequest request, ClaimsPrincipal principal, AppDbContext db) =>
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

            var device = MapToDevice(request);
            device.UserId = userId;

            db.ActuatorDevices.Add(device);
            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                Response = $"Dodano urządzenie: {device.Name}",
                Id = device.Id,
                UserId = device.UserId
            });
        })
            .WithSummary("Create actuator device")
            .WithDescription("Creates a new standalone actuator device for user.")
            .Accepts<UpsertActuatorDeviceRequest>("application/json")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        devices.MapPut("/{id:int}", async (int userId, int id, UpsertActuatorDeviceRequest request, ClaimsPrincipal principal, AppDbContext db) =>
        {
            if (!IsRequestUserAuthorized(principal, userId))
            {
                return Results.Forbid();
            }

            var device = await db.ActuatorDevices
                .FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId);

            if (device is null)
            {
                return Results.NotFound();
            }

            var mapped = MapToDevice(request);
            device.Name = mapped.Name;
            device.TargetParameter = mapped.TargetParameter;
            device.EffectType = mapped.EffectType;
            device.EffectStrength = mapped.EffectStrength;
            device.IsEnabled = mapped.IsEnabled;

            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                Response = $"Zaktualizowano urządzenie: {device.Name}",
                Id = device.Id,
                UserId = device.UserId
            });
        })
            .WithSummary("Update actuator device")
            .WithDescription("Updates an existing standalone actuator device.")
            .Accepts<UpsertActuatorDeviceRequest>("application/json")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        devices.MapDelete("/{id:int}", async (int userId, int id, ClaimsPrincipal principal, AppDbContext db) =>
        {
            if (!IsRequestUserAuthorized(principal, userId))
            {
                return Results.Forbid();
            }

            var device = await db.ActuatorDevices.FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId);

            if (device is null)
            {
                return Results.NotFound();
            }

            db.ActuatorDevices.Remove(device);
            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                Response = $"Usunięto urządzenie: {device.Name}",
                Id = device.Id,
                UserId = userId
            });
        })
            .WithSummary("Delete actuator device")
            .WithDescription("Deletes an actuator device.")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        devices.MapPut("/{deviceId:int}/plants/{plantId:int}", async (int userId, int deviceId, int plantId, ClaimsPrincipal principal, AppDbContext db) =>
        {
            if (!IsRequestUserAuthorized(principal, userId))
            {
                return Results.Forbid();
            }

            var device = await db.ActuatorDevices
                .Include(d => d.Plants)
                .FirstOrDefaultAsync(d => d.Id == deviceId && d.UserId == userId);
            if (device is null)
            {
                return Results.NotFound(new { Response = $"Nie znaleziono urządzenia o id={deviceId}" });
            }

            var plant = await db.Plants.FirstOrDefaultAsync(p => p.Id == plantId && p.UserId == userId);
            if (plant is null)
            {
                return Results.NotFound(new { Response = $"Nie znaleziono rośliny o id={plantId}" });
            }

            if (device.Plants.All(p => p.Id != plantId))
            {
                device.Plants.Add(plant);
                await db.SaveChangesAsync();
            }

            return Results.Ok(new { Response = "Przypisano urządzenie do rośliny", DeviceId = deviceId, PlantId = plantId });
        })
            .WithSummary("Assign device to plant")
            .WithDescription("Assigns an existing device to a plant (many-to-many).")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        devices.MapDelete("/{deviceId:int}/plants/{plantId:int}", async (int userId, int deviceId, int plantId, ClaimsPrincipal principal, AppDbContext db) =>
        {
            if (!IsRequestUserAuthorized(principal, userId))
            {
                return Results.Forbid();
            }

            var device = await db.ActuatorDevices
                .Include(d => d.Plants)
                .FirstOrDefaultAsync(d => d.Id == deviceId && d.UserId == userId);
            if (device is null)
            {
                return Results.NotFound(new { Response = $"Nie znaleziono urządzenia o id={deviceId}" });
            }

            var plant = device.Plants.FirstOrDefault(p => p.Id == plantId && p.UserId == userId);
            if (plant is null)
            {
                return Results.NotFound(new { Response = $"Urządzenie nie jest przypisane do rośliny o id={plantId}" });
            }

            device.Plants.Remove(plant);
            await db.SaveChangesAsync();

            return Results.Ok(new { Response = "Odpięto urządzenie od rośliny", DeviceId = deviceId, PlantId = plantId });
        })
            .WithSummary("Unassign device from plant")
            .WithDescription("Removes relation between device and plant (many-to-many).")
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

    private static ActuatorDevice MapToDevice(UpsertActuatorDeviceRequest request)
    {
        var goCommand = NormalizeGoCommand(request.GoCommand);
        var defaultTargetParameter = GetDefaultTargetParameter(goCommand);

        return new ActuatorDevice
        {
            Name = string.IsNullOrWhiteSpace(request.Name) ? "Unnamed device" : request.Name.Trim(),
            GoCommand = goCommand,
            GoCommandPath = $"/command/{goCommand}",
            GoStateField = GetStateField(goCommand),
            TargetParameter = NormalizeTargetParameter(request.TargetParameter, defaultTargetParameter),
            EffectType = NormalizeEffectType(request.EffectType, goCommand),
            EffectStrength = request.EffectStrength is null or 0 ? 1 : request.EffectStrength.Value,
            IsEnabled = request.IsEnabled ?? true
        };
    }

    private static string NormalizeGoCommand(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "pump";
        }

        var normalized = value.Trim().ToLowerInvariant();
        return SupportedGoCommands.Contains(normalized) ? normalized : "pump";
    }

    private static string NormalizeTargetParameter(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var normalized = value.Trim();
        return SupportedTargetParameters.Contains(normalized) ? normalized : fallback;
    }

    private static string NormalizeEffectType(string? value, string goCommand)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return goCommand == "light" ? "set" : "increase";
        }

        var normalized = value.Trim().ToLowerInvariant();
        return normalized switch
        {
            "increase" or "decrease" or "set" => normalized,
            _ => goCommand == "light" ? "set" : "increase"
        };
    }

    private static string GetDefaultTargetParameter(string goCommand)
    {
        return goCommand switch
        {
            "light" => "lightIsDark",
            _ => "soilMoistureAnalog"
        };
    }

    private static string GetStateField(string goCommand)
    {
        return goCommand switch
        {
            "light" => "lampState",
            _ => "pumpState"
        };
    }
}
