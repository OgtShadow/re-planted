using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using RePlanted.Server.Contracts.Telemetry;
using RePlanted.Server.Data;
using RePlanted.Server.Models;
using Server.Hubs;

namespace RePlanted.Server.Services;

public sealed class TelemetryCollectorBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory scopeFactory;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly ILogger<TelemetryCollectorBackgroundService> logger;
    private readonly IHubContext<TelemetryHub> telemetryHub;
    private readonly TelemetryCollectorOptions options;

    public TelemetryCollectorBackgroundService(
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpClientFactory,
        IHubContext<TelemetryHub> telemetryHub,
        IOptions<TelemetryCollectorOptions> options,
        ILogger<TelemetryCollectorBackgroundService> logger)
    {
        this.scopeFactory = scopeFactory;
        this.httpClientFactory = httpClientFactory;
        this.telemetryHub = telemetryHub;
        this.logger = logger;
        this.options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var client = httpClientFactory.CreateClient(nameof(TelemetryCollectorBackgroundService));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollAndStoreSnapshotAsync(client, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Telemetry collector cycle failed.");
            }

            var intervalSeconds = Math.Clamp(options.PollingIntervalSeconds, 5, 300);
            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
        }
    }

    private async Task PollAndStoreSnapshotAsync(HttpClient client, CancellationToken cancellationToken)
    {
        var endpoint = BuildSensorsUri();
        var snapshots = await client.GetFromJsonAsync<List<SensorTelemetrySnapshot>>(endpoint, cancellationToken);
        if (snapshots is null || snapshots.Count == 0)
        {
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        foreach (var snapshot in snapshots)
        {
            await UpsertSnapshotAsync(db, snapshot, cancellationToken);
        }

        var retentionDays = Math.Clamp(options.RetentionDays, 1, 365);
        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
        var staleBuckets = db.TelemetryBuckets.Where(x => x.BucketStartUtc < cutoff);
        db.TelemetryBuckets.RemoveRange(staleBuckets);

        await db.SaveChangesAsync(cancellationToken);
        await telemetryHub.Clients.All.SendAsync("TelemetryUpdated", snapshots, cancellationToken);
    }

    private static async Task UpsertSnapshotAsync(AppDbContext db, SensorTelemetrySnapshot snapshot, CancellationToken cancellationToken)
    {
        var bucketStartUtc = TruncateToMinute(snapshot.Timestamp == default ? DateTime.UtcNow : snapshot.Timestamp.ToUniversalTime());
        var deviceId = string.IsNullOrWhiteSpace(snapshot.DeviceId) ? "unknown-device" : snapshot.DeviceId.Trim();

        var bucket = await db.TelemetryBuckets
            .FirstOrDefaultAsync(x => x.DeviceId == deviceId && x.BucketStartUtc == bucketStartUtc, cancellationToken);

        if (bucket is null)
        {
            bucket = new TelemetryBucket
            {
                DeviceId = deviceId,
                BucketStartUtc = bucketStartUtc,
                SampleCount = 1,
                TemperatureSum = snapshot.Temperature,
                TemperatureMin = snapshot.Temperature,
                TemperatureMax = snapshot.Temperature,
                HumiditySum = snapshot.Humidity,
                HumidityMin = snapshot.Humidity,
                HumidityMax = snapshot.Humidity,
                SoilMoistureSum = snapshot.SoilMoistureAnalog,
                SoilMoistureMin = snapshot.SoilMoistureAnalog,
                SoilMoistureMax = snapshot.SoilMoistureAnalog,
                WaterLevelSum = snapshot.WaterLevelCm,
                WaterLevelMin = snapshot.WaterLevelCm,
                WaterLevelMax = snapshot.WaterLevelCm,
                LastPumpState = snapshot.PumpState,
                LastLampState = snapshot.LampState
            };

            db.TelemetryBuckets.Add(bucket);
        }
        else
        {
            bucket.SampleCount += 1;
            bucket.TemperatureSum += snapshot.Temperature;
            bucket.TemperatureMin = Math.Min(bucket.TemperatureMin, snapshot.Temperature);
            bucket.TemperatureMax = Math.Max(bucket.TemperatureMax, snapshot.Temperature);

            bucket.HumiditySum += snapshot.Humidity;
            bucket.HumidityMin = Math.Min(bucket.HumidityMin, snapshot.Humidity);
            bucket.HumidityMax = Math.Max(bucket.HumidityMax, snapshot.Humidity);

            bucket.SoilMoistureSum += snapshot.SoilMoistureAnalog;
            bucket.SoilMoistureMin = Math.Min(bucket.SoilMoistureMin, snapshot.SoilMoistureAnalog);
            bucket.SoilMoistureMax = Math.Max(bucket.SoilMoistureMax, snapshot.SoilMoistureAnalog);

            bucket.WaterLevelSum += snapshot.WaterLevelCm;
            bucket.WaterLevelMin = Math.Min(bucket.WaterLevelMin, snapshot.WaterLevelCm);
            bucket.WaterLevelMax = Math.Max(bucket.WaterLevelMax, snapshot.WaterLevelCm);

            bucket.LastPumpState = snapshot.PumpState;
            bucket.LastLampState = snapshot.LampState;
        }
    }

    private Uri BuildSensorsUri()
    {
        var trimmedBaseUrl = options.BaseUrl.TrimEnd('/');
        var trimmedPath = options.SensorsPath.StartsWith('/') ? options.SensorsPath : "/" + options.SensorsPath;
        return new Uri($"{trimmedBaseUrl}{trimmedPath}");
    }

    private static DateTime TruncateToMinute(DateTime valueUtc)
    {
        return new DateTime(valueUtc.Year, valueUtc.Month, valueUtc.Day, valueUtc.Hour, valueUtc.Minute, 0, DateTimeKind.Utc);
    }
}