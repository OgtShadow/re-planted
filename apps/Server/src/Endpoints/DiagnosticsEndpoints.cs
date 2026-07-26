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
        })
            .WithTags("Diagnostics")
            .WithSummary("Server status")
            .WithDescription("Returns a simple server status page with cached messages and current plants.")
            .Produces<string>(StatusCodes.Status200OK);

        app.MapGet("/communication-test", () => "Communication with Client works!")
            .WithTags("Diagnostics")
            .WithSummary("Client communication check")
            .Produces<string>(StatusCodes.Status200OK);

        app.MapGet("/api/post", () => "Endpoint /api/post obsługuje POST. Użyj klienta do wysłania danych.")
            .WithTags("Diagnostics")
            .WithSummary("POST usage info")
            .Produces<string>(StatusCodes.Status200OK);

        app.MapPost("/api/post", (ExampleData data) =>
        {
            var message = $"Otrzymano wiadomość: {data.Message}";
            ReceivedMessages.Add(message);
            return Results.Ok(new { Response = message });
        })
            .WithTags("Diagnostics")
            .WithSummary("Store test message")
            .WithDescription("Stores a message in memory for quick diagnostics.")
            .Accepts<ExampleData>("application/json")
            .Produces(StatusCodes.Status200OK);

        return app;
    }
}
