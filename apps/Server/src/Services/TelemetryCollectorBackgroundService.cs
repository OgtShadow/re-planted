using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using RePlanted.Server.Contracts.Telemetry;
using RePlanted.Server.Data;
using RePlanted.Server.Models;
using Server.Hubs;

namespace RePlanted.Server.Services;

public interface ITelemetryRefreshService
{
    Task<IReadOnlyList<SensorTelemetrySnapshot>> RefreshAsync(CancellationToken cancellationToken);
}

public sealed class TelemetryCollectorBackgroundService : BackgroundService, ITelemetryRefreshService
{
    private readonly IServiceScopeFactory scopeFactory;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly ILogger<TelemetryCollectorBackgroundService> logger;
    private readonly IHubContext<TelemetryHub> telemetryHub;
    private readonly TelemetryCollectorOptions options;
    private readonly Dictionary<string, int> missedTelemetryCycles = new(StringComparer.OrdinalIgnoreCase);

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
                await RefreshAsync(stoppingToken, client);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Telemetry collector cycle failed.");
            }

            var intervalSeconds = Math.Clamp(options.PollingIntervalSeconds, 5, 300);
            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
        }
    }

    public Task<IReadOnlyList<SensorTelemetrySnapshot>> RefreshAsync(CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(nameof(TelemetryCollectorBackgroundService));
        return RefreshAsync(cancellationToken, client);
    }

    private async Task<IReadOnlyList<SensorTelemetrySnapshot>> RefreshAsync(CancellationToken cancellationToken, HttpClient client)
    {
        var endpoint = BuildSensorsUri();
        var snapshots = new List<SensorTelemetrySnapshot>();
        var primarySnapshots = await client.GetFromJsonAsync<List<SensorTelemetrySnapshot>>(endpoint, cancellationToken);
        if (primarySnapshots is not null)
        {
            snapshots.AddRange(primarySnapshots);
        }

        foreach (var additionalUrl in options.AdditionalSensorUrls)
        {
            if (string.IsNullOrWhiteSpace(additionalUrl))
            {
                continue;
            }

            try
            {
                var additionalSnapshot = await client.GetFromJsonAsync<SensorTelemetrySnapshot>(additionalUrl, cancellationToken);
                if (additionalSnapshot is not null)
                {
                    snapshots.Add(additionalSnapshot);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to poll additional sensor endpoint {Url}.", additionalUrl);
            }
        }

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var alertService = scope.ServiceProvider.GetRequiredService<IAlertService>();

        if (snapshots.Count == 0)
        {
            var users = await db.Users.Select(user => user.Id).ToListAsync(cancellationToken);
            foreach (var userId in users)
            {
                await alertService.CreateIfActiveMissingAsync(db, userId, AlertTypes.MissingTelemetry,
                    AlertSeverities.Warning, "Brak telemetrii", "Nie odebrano danych telemetrycznych z kontrolera.",
                    "telemetry:missing", cancellationToken);
            }
            return snapshots;
        }

        foreach (var snapshot in snapshots)
        {
            await UpsertSnapshotAsync(db, snapshot, cancellationToken);

            var devices = await db.ActuatorDevices
                .Where(device => device.ExternalDeviceId == snapshot.DeviceId && device.UserId > 0)
                .Select(device => new { device.UserId, device.Name })
                .ToListAsync(cancellationToken);
            foreach (var device in devices)
            {
                if (snapshot.WaterLevelCm <= 5)
                {
                    await alertService.CreateIfActiveMissingAsync(db, device.UserId, AlertTypes.LowWater,
                        AlertSeverities.Critical, "Niski poziom wody", $"Urządzenie {device.Name} zgłasza poziom wody {snapshot.WaterLevelCm} cm.",
                        $"water:{snapshot.DeviceId}", cancellationToken);
                }
            }
        }

        var receivedDeviceIds = snapshots
            .Select(snapshot => snapshot.DeviceId)
            .Where(deviceId => !string.IsNullOrWhiteSpace(deviceId))
            .Select(NormalizeIdentifier)
            .ToList();
        var configuredDevices = await db.ActuatorDevices
            .Where(device => device.IsEnabled && device.DeviceKind.ToLower() == "sensor")
            .Select(device => new { device.UserId, device.Name, device.ExternalDeviceId })
            .ToListAsync(cancellationToken);
        foreach (var device in configuredDevices)
        {
            var deviceKey = NormalizeIdentifier(device.ExternalDeviceId);
            var received = receivedDeviceIds.Any(receivedId => IsMatchingDeviceId(deviceKey, receivedId));
            if (received)
            {
                missedTelemetryCycles.Remove(deviceKey);
                continue;
            }

            missedTelemetryCycles[deviceKey] = missedTelemetryCycles.TryGetValue(deviceKey, out var missed)
                ? missed + 1
                : 1;
            if (missedTelemetryCycles[deviceKey] < 2)
            {
                continue;
            }

            await alertService.CreateIfActiveMissingAsync(db, device.UserId, AlertTypes.DeviceDisconnected,
                AlertSeverities.Warning, "Urządzenie odłączone", $"Nie odebrano telemetrii z urządzenia {device.Name}.",
                $"device:{deviceKey}", cancellationToken);
        }

        var retentionDays = Math.Clamp(options.RetentionDays, 1, 365);
        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
        var staleBuckets = db.TelemetryBuckets.Where(x => x.BucketStartUtc < cutoff);
        db.TelemetryBuckets.RemoveRange(staleBuckets);

        await db.SaveChangesAsync(cancellationToken);
        await telemetryHub.Clients.All.SendAsync("TelemetryUpdated", snapshots, cancellationToken);
        return snapshots;
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

    private static string NormalizeIdentifier(string value)
    {
        return value.Trim().ToLowerInvariant();
    }

    private static bool IsMatchingDeviceId(string configuredId, string receivedId)
    {
        return configuredId.Length > 0 && (configuredId == receivedId
            || receivedId.StartsWith(configuredId + "-", StringComparison.Ordinal)
            || configuredId.StartsWith(receivedId + "-", StringComparison.Ordinal));
    }
}