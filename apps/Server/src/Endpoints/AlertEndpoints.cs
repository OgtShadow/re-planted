using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RePlanted.Server.Contracts.Alerts;
using RePlanted.Server.Data;
using RePlanted.Server.Models;
using RePlanted.Server.Services;
using Server.Hubs;
using System.Security.Claims;

namespace RePlanted.Server.Endpoints;

public static class AlertEndpoints
{
    public static IEndpointRouteBuilder MapAlertEndpoints(this IEndpointRouteBuilder app)
    {
        var alerts = app.MapGroup("/api/users/{userId:int}/alerts")
            .WithTags("Alerts")
            .RequireAuthorization();

        alerts.MapPost("/parameter-range", async (
            int userId,
            ParameterRangeAlertRequest request,
            ClaimsPrincipal principal,
            AppDbContext db,
            IAlertService alertService,
            CancellationToken cancellationToken) =>
        {
            if (!IsAuthorized(principal, userId)) return Results.Forbid();

            var parameter = request.Parameter?.Trim();
            if (request.PlantId <= 0 ||
                parameter is null ||
                !(string.Equals(parameter, "temperature", StringComparison.OrdinalIgnoreCase) ||
                  string.Equals(parameter, "humidity", StringComparison.OrdinalIgnoreCase)) ||
                !double.IsFinite(request.Value) ||
                !double.IsFinite(request.Min) ||
                !double.IsFinite(request.Max) ||
                request.Min > request.Max ||
                request.Value >= request.Min && request.Value <= request.Max)
            {
                return Results.BadRequest(new { Response = "Nieprawidłowe dane alertu zakresu." });
            }

            var plant = await db.Plants.FirstOrDefaultAsync(plant => plant.Id == request.PlantId && plant.UserId == userId, cancellationToken);
            if (plant is null) return Results.NotFound();

            var normalizedParameter = parameter.ToLowerInvariant();
            var sourceKey = $"plant-range:{plant.Id}:{normalizedParameter}";
            var valueLabel = normalizedParameter == "temperature" ? "°C" : "%";
            var sensorLabel = string.IsNullOrWhiteSpace(request.SensorDeviceId) ? string.Empty : $" Czujnik: {request.SensorDeviceId}.";

            await alertService.CreateIfActiveMissingAsync(
                db,
                userId,
                AlertTypes.ThresholdExceeded,
                AlertSeverities.Warning,
                $"Parametr poza zakresem: {plant.Name}",
                $"{parameter}: {request.Value:0.0}{valueLabel}; dozwolony zakres: {request.Min:0.0}-{request.Max:0.0}{valueLabel}. Brak kompatybilnego, włączonego aktuatora.{sensorLabel}",
                sourceKey,
                cancellationToken);

            return Results.Accepted();
        })
            .WithSummary("Report a plant parameter outside its configured range without a compatible actuator")
            .Produces(StatusCodes.Status202Accepted)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        alerts.MapGet("", async (int userId, bool? activeOnly, ClaimsPrincipal principal, AppDbContext db) =>
        {
            if (!IsAuthorized(principal, userId)) return Results.Forbid();

            var query = db.Alerts.Where(alert => alert.UserId == userId);
            if (activeOnly == true) query = query.Where(alert => alert.AcknowledgedAtUtc == null);

            var result = await query.OrderByDescending(alert => alert.CreatedAtUtc)
                .Take(100)
                .Select(alert => new AlertResponse
                {
                    Id = alert.Id,
                    Type = alert.Type,
                    Severity = alert.Severity,
                    Title = alert.Title,
                    Message = alert.Message,
                    CreatedAtUtc = alert.CreatedAtUtc,
                    AcknowledgedAtUtc = alert.AcknowledgedAtUtc
                })
                .ToListAsync();

            return Results.Ok(result);
        }).Produces<List<AlertResponse>>();

        alerts.MapPost("/{id:int}/acknowledge", async (int userId, int id, ClaimsPrincipal principal, AppDbContext db, IHubContext<AlertHub> hub) =>
        {
            if (!IsAuthorized(principal, userId)) return Results.Forbid();

            var alert = await db.Alerts.FirstOrDefaultAsync(item => item.Id == id && item.UserId == userId);
            if (alert is null) return Results.NotFound();

            alert.AcknowledgedAtUtc ??= DateTime.UtcNow;
            await db.SaveChangesAsync();
            await hub.Clients.User(userId.ToString()).SendAsync("AlertAcknowledged", id);
            return Results.NoContent();
        }).Produces(StatusCodes.Status204NoContent);

        return app;
    }

    private static bool IsAuthorized(ClaimsPrincipal principal, int userId)
    {
        var claim = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("sub");
        return int.TryParse(claim, out var claimedUserId) && claimedUserId == userId;
    }
}
