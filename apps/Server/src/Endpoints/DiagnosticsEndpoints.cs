using Microsoft.EntityFrameworkCore;
using RePlanted.Server.Contracts;
using RePlanted.Server.Data;

namespace RePlanted.Server.Endpoints;

public static class DiagnosticsEndpoints
{
    private static readonly List<string> ReceivedMessages = new();

    public static IEndpointRouteBuilder MapDiagnosticsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/", async (AppDbContext db) =>
        {
            var plants = await db.Plants.ToListAsync();
            return $"Server in fact działa!\n\nOtrzymane wiadomości:\n{string.Join("\n", ReceivedMessages)}\n\n" +
                   $"Dodane obecnie rośliny:\n{string.Join("\n", plants.Select(p => $"{p.Name}, {p.Species}, {p.PlantedDate}, {p.HealthStatus}"))}";
        });

        app.MapGet("/communication-test", () => "Communication with Client works!");

        app.MapGet("/api/post", () => "Endpoint /api/post obsługuje POST. Użyj klienta do wysłania danych.");

        app.MapPost("/api/post", (ExampleData data) =>
        {
            var message = $"Otrzymano wiadomość: {data.Message}";
            ReceivedMessages.Add(message);
            return Results.Ok(new { Response = message });
        });

        return app;
    }
}
