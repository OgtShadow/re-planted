using Microsoft.EntityFrameworkCore;
using RePlanted.Server.Data;
using RePlanted.Server;
using DotNetEnv;

var builder = WebApplication.CreateBuilder(args);

// Load env vars from .env
Env.Load();

// Configuration builder will automatically load Environment Variables
// We just need to make sure DotNetEnv loads them into the process first (which Env.Load() does)
builder.Configuration.AddEnvironmentVariables();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddHttpClient<RePlanted.Server.Services.ConnectionManager>();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseOracle(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

app.UseCors("AllowAll");

var receivedMessages = new List<string>();

app.MapGet("/", async (AppDbContext db) => { 
    var plants = await db.Plants.ToListAsync();
    return $"Server in fact działa!\n\nOtrzymane wiadomości:\n{string.Join("\n", receivedMessages)}\n\n" +
     $"Dodane obecnie rośliny:\n{string.Join("\n", plants.Select(p => $"{p.Name}, {p.Species}, {p.PlantedDate}, {p.HealthStatus}"))}";
    });

app.MapGet("/communication-test", () => "Communication with Client works!");

app.MapGet("/api/post", () => "Endpoint /api/post obsługuje POST. Użyj klienta do wysłania danych.");

app.MapPost("/api/post", (ExampleData data) => {
    var message = $"Otrzymano wiadomość: {data.Message}";
    receivedMessages.Add(message);
    return Results.Ok(new { Response = message });
});

app.MapGet("/api/plants", async (AppDbContext db) => await db.Plants.Include(p => p.Parameters).ToListAsync());

app.MapPost("/api/plants", async (Plant newPlant, AppDbContext db) => {
    db.Plants.Add(newPlant);
    await db.SaveChangesAsync();
    Console.WriteLine($"Dodano roślinę: {newPlant.Name}, {newPlant.Species}");
    return Results.Ok(new { Response = $"Dodano roślinę: {newPlant.Name}, {newPlant.Species}" });
});

app.Run();

public record ExampleData(string Message);