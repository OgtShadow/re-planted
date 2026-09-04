using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RePlanted.Server.Data;
using RePlanted.Server.Models;
using Server.Hubs;

namespace RePlanted.Server.Services;

public interface IAlertService
{
    Task<Alert?> CreateIfActiveMissingAsync(AppDbContext db, int userId, string type, string severity, string title, string message, string sourceKey, CancellationToken cancellationToken);
}

public sealed class AlertService(IHubContext<AlertHub> alertHub) : IAlertService
{
    public async Task<Alert?> CreateIfActiveMissingAsync(
        AppDbContext db,
        int userId,
        string type,
        string severity,
        string title,
        string message,
        string sourceKey,
        CancellationToken cancellationToken)
    {
        var exists = await db.Alerts.AnyAsync(alert =>
            alert.UserId == userId && alert.SourceKey == sourceKey && alert.AcknowledgedAtUtc == null,
            cancellationToken);
        if (exists) return null;

        var alert = new Alert
        {
            UserId = userId,
            Type = type,
            Severity = severity,
            Title = title,
            Message = message,
            SourceKey = sourceKey,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.Alerts.Add(alert);
        await db.SaveChangesAsync(cancellationToken);
        await alertHub.Clients.User(userId.ToString()).SendAsync("AlertCreated", new
        {
            alert.Id,
            alert.Type,
            alert.Severity,
            alert.Title,
            alert.Message,
            alert.CreatedAtUtc,
            alert.AcknowledgedAtUtc
        }, cancellationToken);
        return alert;
    }
}
