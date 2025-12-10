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

app.MapGet("/", () => "Server in fact działa!");

app.MapGet("/communication-test", () => "Communication with Client works!");

app.Run();