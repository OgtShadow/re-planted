using Microsoft.EntityFrameworkCore;
using RePlanted.Server.Data;
using RePlanted.Server;
using DotNetEnv;
using Server.Hubs;
using Microsoft.AspNetCore.SignalR;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Json;

var builder = WebApplication.CreateBuilder(args);

// Load env vars from .env
Env.Load();

// Configuration builder will automatically load Environment Variables
// We just need to make sure DotNetEnv loads them into the process first (which Env.Load() does)
builder.Configuration.AddEnvironmentVariables();

builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
});

builder.Services.AddSignalR();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowClient", policy =>
    {
        policy.SetIsOriginAllowed(origin => true) 
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

builder.Services.AddHttpClient<RePlanted.Server.Services.ConnectionManager>();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseOracle(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

app.UseCors("AllowClient");
app.MapHub<PlantHub>("/plantHub");

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

app.MapGet("/api/plants/{id}", async (int id, AppDbContext db) => 
{
    var plant = await db.Plants.Include(p => p.Parameters).FirstOrDefaultAsync(p => p.Id == id);
    return plant is not null ? Results.Ok(plant) : Results.NotFound();
});

app.MapPost("/api/plants", async (Plant newPlant, AppDbContext db, IHubContext<PlantHub> hubContext) => {
    db.Plants.Add(newPlant);
    await db.SaveChangesAsync();
    Console.WriteLine($"Dodano roślinę: {newPlant.Name}, {newPlant.Species}");
    
    // Powiadom wszystkich klientów o zmianie
    await hubContext.Clients.All.SendAsync("PlantsUpdated");
    
    return Results.Ok(new { Response = $"Dodano roślinę: {newPlant.Name}, {newPlant.Species}" });
});

app.MapPut("/api/plants/{id}", async (int id, Plant updatedPlant, AppDbContext db, IHubContext<PlantHub> hubContext) => {
    var plant = await db.Plants.Include(p => p.Parameters).FirstOrDefaultAsync(p => p.Id == id);
    if (plant is null) return Results.NotFound();

    plant.Name = updatedPlant.Name;
    plant.Species = updatedPlant.Species;
    
    if (plant.Parameters != null && updatedPlant.Parameters != null)
    {
        plant.Parameters.Temperature = updatedPlant.Parameters.Temperature;
        plant.Parameters.Humidity = updatedPlant.Parameters.Humidity;
        plant.Parameters.WateringIntervalDays = updatedPlant.Parameters.WateringIntervalDays;
        plant.Parameters.LightHoursPerDay = updatedPlant.Parameters.LightHoursPerDay;
    }
    else if (updatedPlant.Parameters != null)
    {
        plant.Parameters = updatedPlant.Parameters;
    }

    await db.SaveChangesAsync();

    await hubContext.Clients.All.SendAsync("PlantsUpdated");

    return Results.Ok(new { Response = $"Zaktualizowano roślinę: {plant.Name}" });
});

app.MapDelete("/api/plants/{id}", async (int id, AppDbContext db, IHubContext<PlantHub> hubContext) => {
    var plant = await db.Plants.FindAsync(id);
    if (plant is null) return Results.NotFound();

    db.Plants.Remove(plant);
    await db.SaveChangesAsync();

    await hubContext.Clients.All.SendAsync("PlantsUpdated");

    return Results.Ok(new { Response = $"Usunięto roślinę: {plant.Name}" });
});

app.Run();

public record ExampleData(string Message);