using DotNetEnv;
using Server.Hubs;
using RePlanted.Server.Extensions;

var builder = WebApplication.CreateBuilder(args);


if (File.Exists(".env"))
{
    Env.Load();
}

builder.Configuration.AddEnvironmentVariables();
builder.Services.AddServerServices(builder.Configuration);

var app = builder.Build();

app.UseServerSwagger();
app.UseExceptionHandler(exceptionApp => exceptionApp.Run(async context =>
{
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    context.Response.ContentType = "application/json";
    await context.Response.WriteAsJsonAsync(new { response = "Wewnętrzny błąd serwera." });
}));
app.UseCors(ServiceCollectionExtensions.AllowClientPolicy);
app.UseAuthentication();
app.UseAuthorization();
app.MapHub<PlantHub>("/plantHub");
app.MapHub<UserHub>("/userHub");
app.MapHub<TelemetryHub>("/telemetryHub");
app.MapHub<AlertHub>("/alertsHub");
app.MapServerEndpoints();
if (!app.Environment.IsEnvironment("Testing"))
{
    app.ApplyDatabaseMigrations();
}

app.Run();

public partial class Program;
