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

app.MapGet("/", () => $"Server in fact działa!\n\nOtrzymane wiadomości:\n{string.Join("\n", receivedMessages)}");

app.MapGet("/communication-test", () => "Communication with Client works!");

app.MapGet("/api/post", () => "Endpoint /api/post obsługuje POST. Użyj klienta do wysłania danych.");

app.MapPost("/api/post", (ExampleData data) => {
    var message = $"Otrzymano wiadomość: {data.Message}";
    receivedMessages.Add(message);
    return Results.Ok(new { Response = message });
});

app.Run();

public record ExampleData(string Message);