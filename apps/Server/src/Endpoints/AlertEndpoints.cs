using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RePlanted.Server.Contracts.Alerts;
using RePlanted.Server.Data;
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
