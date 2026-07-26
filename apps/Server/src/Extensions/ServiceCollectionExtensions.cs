using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using RePlanted.Server.Data;
using System.Text.Json.Serialization;

namespace RePlanted.Server.Extensions;

public static class ServiceCollectionExtensions
{
    public const string AllowClientPolicy = "AllowClient";

    public static IServiceCollection AddServerServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JsonOptions>(options =>
        {
            options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        });

        services.AddSignalR();

        services.AddCors(options =>
        {
            options.AddPolicy(AllowClientPolicy, policy =>
            {
                policy.SetIsOriginAllowed(_ => true)
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials();
            });
        });

        services.AddHttpClient<RePlanted.Server.Services.ConnectionManager>();

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Re-Planted Server API",
                Version = "v1",
                Description = "HTTP API for plant management, diagnostics, and real-time updates."
            });
        });

        return services;
    }
}
