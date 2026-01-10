var builder = WebApplication.CreateBuilder(args);

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

var app = builder.Build();

app.UseCors("AllowAll");

var receivedMessages = new List<string>();
var plantsList = new List<RePlanted.Server.Plant>();

app.MapGet("/", () => { 
    return $"Server in fact działa!\n\nOtrzymane wiadomości:\n{string.Join("\n", receivedMessages)}\n\n" +
     $"Dodane obecnie rośliny:\n{string.Join("\n", plantsList.Select(p => $"{p.Name}, {p.Species}, {p.PlantedDate}, {p.HealthStatus}"))}";
    });

app.MapGet("/communication-test", () => "Communication with Client works!");

app.MapGet("/api/post", () => "Endpoint /api/post obsługuje POST. Użyj klienta do wysłania danych.");

app.MapPost("/api/post", (ExampleData data) => {
    var message = $"Otrzymano wiadomość: {data.Message}";
    receivedMessages.Add(message);
    return Results.Ok(new { Response = message });
});

app.MapGet("/api/plants", () => plantsList);

app.MapPost("/api/plants", (RePlanted.Server.Plant newPlant) => {
    plantsList.Add(newPlant);
    Console.WriteLine($"Dodano roślinę: {newPlant.Name}, {newPlant.Species}");
    return Results.Ok(new { Response = $"Dodano roślinę: {newPlant.Name}, {newPlant.Species}" });
});


app.Run();

public record ExampleData(string Message);