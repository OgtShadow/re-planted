using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
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

        return services;
    }
}
