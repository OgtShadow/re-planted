using Microsoft.EntityFrameworkCore;
using RePlanted.Server.Data;
using RePlanted.Server.Endpoints;

namespace RePlanted.Server.Extensions;

public static class WebApplicationExtensions
{
    public static WebApplication UseServerSwagger(this WebApplication app)
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "Re-Planted Server API v1");
            options.RoutePrefix = "swagger";
            options.DocumentTitle = "Re-Planted API Docs";
        });

        return app;
    }

    public static WebApplication MapServerEndpoints(this WebApplication app)
    {
        app.MapDiagnosticsEndpoints();
        app.MapPlantEndpoints();
        app.MapActuatorDeviceEndpoints();
        app.MapUserEndpoints();
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
