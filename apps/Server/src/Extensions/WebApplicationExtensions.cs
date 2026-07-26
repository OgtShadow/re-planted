using Microsoft.EntityFrameworkCore;
using RePlanted.Server.Data;
using RePlanted.Server.Endpoints;

namespace RePlanted.Server.Extensions;

public static class WebApplicationExtensions
{
    public static WebApplication MapServerEndpoints(this WebApplication app)
    {
        app.MapDiagnosticsEndpoints();
        app.MapPlantEndpoints();
        return app;
    }

    public static WebApplication ApplyDatabaseMigrations(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        dbContext.Database.Migrate();
        return app;
    }
}
