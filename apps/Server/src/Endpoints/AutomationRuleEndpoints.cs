using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RePlanted.Server.Contracts;
using RePlanted.Server.Contracts.AutomationRules;
using RePlanted.Server.Data;
using RePlanted.Server.Models;
using Server.Hubs;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace RePlanted.Server.Endpoints;

public static class AutomationRuleEndpoints
{
    public static IEndpointRouteBuilder MapAutomationRuleEndpoints(this IEndpointRouteBuilder app)
    {
        var rules = app.MapGroup("/api/users/{userId:int}/automation-rules")
            .WithTags("AutomationRules")
            .RequireAuthorization();

        rules.MapGet("", async (int userId, ClaimsPrincipal principal, AppDbContext db) =>
        {
            if (!IsRequestUserAuthorized(principal, userId))
            {
                return Results.Forbid();
            }

            var result = await db.AutomationRules
                .Include(r => r.Plant)
                .Include(r => r.SensorDevice)
                .Include(r => r.ActuatorDevice)
                .Where(r => r.UserId == userId)
                .ToListAsync();

            return Results.Ok(result.Select(rule => rule.ToResponse()).ToList());
        })
            .WithSummary("Get all automation rules for user")
            .Produces<List<AutomationRuleResponse>>(StatusCodes.Status200OK);

        rules.MapGet("/{id:int}", async (int userId, int id, ClaimsPrincipal principal, AppDbContext db) =>
        {
            if (!IsRequestUserAuthorized(principal, userId))
            {
                return Results.Forbid();
            }

            var rule = await db.AutomationRules
                .Include(r => r.Plant)
                .Include(r => r.SensorDevice)
                .Include(r => r.ActuatorDevice)
                .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId);

            return rule is not null ? Results.Ok(rule.ToResponse()) : Results.NotFound();
        })
            .WithSummary("Get automation rule by ID")
            .Produces<AutomationRuleResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        rules.MapPost("", async (int userId, UpsertAutomationRuleRequest request, ClaimsPrincipal principal, AppDbContext db, IHubContext<PlantHub> hubContext) =>
        {
            if (!IsRequestUserAuthorized(principal, userId))
            {
                return Results.Forbid();
            }

            var rule = new AutomationRule { UserId = userId };
            var validationError = await ApplyRequestAsync(rule, request, userId, db);
            if (validationError is not null)
            {
                return Results.BadRequest(new { Response = validationError });
            }

            db.AutomationRules.Add(rule);
            await db.SaveChangesAsync();
            await hubContext.Clients.All.SendAsync("AutomationRulesUpdated");

            return Results.Ok(new { Response = $"Dodano regułę automatyzacji dla rośliny {rule.PlantId}", Id = rule.Id, UserId = rule.UserId });
        })
            .WithSummary("Create automation rule")
            .Accepts<UpsertAutomationRuleRequest>("application/json")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);

        rules.MapPut("/{id:int}", async (int userId, int id, UpsertAutomationRuleRequest request, ClaimsPrincipal principal, AppDbContext db, IHubContext<PlantHub> hubContext) =>
        {
            if (!IsRequestUserAuthorized(principal, userId))
            {
                return Results.Forbid();
            }

            var rule = await db.AutomationRules.FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId);
            if (rule is null) return Results.NotFound();

            var validationError = await ApplyRequestAsync(rule, request, userId, db);
            if (validationError is not null)
            {
                return Results.BadRequest(new { Response = validationError });
            }

            rule.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync();
            await hubContext.Clients.All.SendAsync("AutomationRulesUpdated");

            return Results.Ok(new { Response = $"Zaktualizowano regułę automatyzacji {rule.Id}", Id = rule.Id, UserId = rule.UserId });
        })
            .WithSummary("Update automation rule")
            .Accepts<UpsertAutomationRuleRequest>("application/json")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        rules.MapDelete("/{id:int}", async (int userId, int id, ClaimsPrincipal principal, AppDbContext db, IHubContext<PlantHub> hubContext) =>
        {
            if (!IsRequestUserAuthorized(principal, userId))
            {
                return Results.Forbid();
            }

            var rule = await db.AutomationRules.FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId);
            if (rule is null) return Results.NotFound();

            db.AutomationRules.Remove(rule);
            await db.SaveChangesAsync();
            await hubContext.Clients.All.SendAsync("AutomationRulesUpdated");

            return Results.Ok(new { Response = $"Usunięto regułę automatyzacji {rule.Id}", Id = rule.Id, UserId = userId });
        })
            .WithSummary("Delete automation rule")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        // Called by ClientServer's Rule Engine right after it successfully executes an action, so the
        // cooldown survives ClientServer restarts and stays visible to the user.
        rules.MapPost("/{id:int}/trigger", async (int userId, int id, ClaimsPrincipal principal, AppDbContext db) =>
        {
            if (!IsRequestUserAuthorized(principal, userId))
            {
                return Results.Forbid();
            }

            var rule = await db.AutomationRules.FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId);
            if (rule is null) return Results.NotFound();

            rule.LastTriggeredUtc = DateTime.UtcNow;
            await db.SaveChangesAsync();

            return Results.Ok(new { Response = "Zarejestrowano wykonanie reguły", rule.LastTriggeredUtc });
        })
            .WithSummary("Record that an automation rule just fired")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<string?> ApplyRequestAsync(AutomationRule rule, UpsertAutomationRuleRequest request, int userId, AppDbContext db)
    {
        if (request.PlantId is not { } plantId || !await db.Plants.AnyAsync(p => p.Id == plantId && p.UserId == userId))
        {
            return "Nieprawidłowy identyfikator rośliny.";
        }

        if (request.SensorDeviceId is not { } sensorDeviceId || !await db.ActuatorDevices.AnyAsync(d => d.Id == sensorDeviceId && d.UserId == userId))
        {
            return "Nieprawidłowy identyfikator urządzenia czujnika.";
        }

        if (string.IsNullOrWhiteSpace(request.SensorField))
        {
            return "Pole SensorField jest wymagane.";
        }

        if (request.Condition is null || !AutomationConditions.All.Contains(request.Condition))
        {
            return $"Pole Condition musi być jedną z wartości: {string.Join(", ", AutomationConditions.All)}.";
        }

        if (request.ActuatorDeviceId is not { } actuatorDeviceId)
        {
            return "Nieprawidłowy identyfikator urządzenia wykonawczego.";
        }

        var actuatorDevice = await db.ActuatorDevices.FirstOrDefaultAsync(d => d.Id == actuatorDeviceId && d.UserId == userId);
        if (actuatorDevice is null)
        {
            return "Nieprawidłowy identyfikator urządzenia wykonawczego.";
        }

        if (request.Action is null || !AutomationActions.All.Contains(request.Action))
        {
            return $"Pole Action musi być jedną z wartości: {string.Join(", ", AutomationActions.All)}.";
        }

        if (request.DurationSeconds is not { } durationSeconds || durationSeconds <= 0)
        {
            return "Pole DurationSeconds musi być większe od zera.";
        }

        var status = request.Status ?? AutomationRuleStatuses.Enabled;
        if (!AutomationRuleStatuses.All.Contains(status))
        {
            return $"Pole Status musi być jedną z wartości: {string.Join(", ", AutomationRuleStatuses.All)}.";
        }

        rule.PlantId = plantId;
        rule.SensorDeviceId = sensorDeviceId;
        rule.SensorField = request.SensorField.Trim();
        rule.Condition = request.Condition;
        rule.Threshold = request.Threshold ?? 0;
        rule.ActuatorDeviceId = actuatorDeviceId;
        rule.Action = request.Action;
        rule.DurationSeconds = durationSeconds;
        rule.Priority = request.Priority ?? 100;
        rule.CooldownMinutes = Math.Max(0, request.CooldownMinutes ?? 30);
        rule.Status = status;

        // The allowed hours for light are one setting per plant (Parameters), not per rule, so it
        // can't disturb e.g. sleep no matter which rule/device ends up controlling the light.
        if (string.Equals(actuatorDevice.TargetParameter, "light", StringComparison.OrdinalIgnoreCase))
        {
            var plant = await db.Plants.Include(p => p.Parameters).FirstOrDefaultAsync(p => p.Id == plantId);
            rule.ScheduleStartTime = plant?.Parameters?.LightScheduleStart;
            rule.ScheduleEndTime = plant?.Parameters?.LightScheduleEnd;
        }
        else
        {
            rule.ScheduleStartTime = request.ScheduleStartTime;
            rule.ScheduleEndTime = request.ScheduleEndTime;
        }

        return null;
    }

    private static bool IsRequestUserAuthorized(ClaimsPrincipal principal, int routeUserId)
    {
        var claimValue = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub);

        return int.TryParse(claimValue, out var claimUserId) && claimUserId == routeUserId;
    }
}
