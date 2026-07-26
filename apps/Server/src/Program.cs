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
app.UseCors(ServiceCollectionExtensions.AllowClientPolicy);
app.MapHub<PlantHub>("/plantHub");
app.MapServerEndpoints();
app.ApplyDatabaseMigrations();

app.Run();
