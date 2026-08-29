using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using RePlanted.Server.Contracts.Telemetry;
using RePlanted.Server.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace RePlanted.Server.Endpoints;

public static class TelemetryEndpoints
{
    public static IEndpointRouteBuilder MapTelemetryEndpoints(this IEndpointRouteBuilder app)
    {
        var telemetry = app.MapGroup("/api/users/{userId:int}/telemetry")
            .WithTags("Telemetry")
            .RequireAuthorization();

        telemetry.MapGet("/trends", [Authorize] async (
            int userId,
            ClaimsPrincipal principal,
            AppDbContext db,
            string? deviceId,
            int? plantId,
            string? sensorField,
            int hours = 24,
            int maxPoints = 240) =>
        {
            if (!IsRequestUserAuthorized(principal, userId))
            {
                return Results.Forbid();
            }

            var normalizedHours = Math.Clamp(hours, 1, 24 * 30);
            var normalizedMaxPoints = Math.Clamp(maxPoints, 30, 1000);

            var toUtc = DateTime.UtcNow;
            var fromUtc = toUtc.AddHours(-normalizedHours);

            var normalizedSensorField = NormalizeSensorField(sensorField);
            var normalizedDeviceId = NormalizeIdentifier(deviceId);

            var contexts = await BuildTelemetryContextsAsync(db, userId, plantId, normalizedSensorField);
            if (contexts.Count == 0)
            {
                return Results.Ok(new TelemetryTrendResponse
                {
                    DeviceId = string.Empty,
                    DeviceName = string.Empty,
                    ExternalDeviceId = string.Empty,
                    PlantId = plantId,
                    SensorField = normalizedSensorField,
                    FromUtc = fromUtc,
                    ToUtc = toUtc,
                    IntervalMinutes = 1,
                    Points = []
                });
            }

            var selectedContext = string.IsNullOrWhiteSpace(normalizedDeviceId)
                ? contexts[0]
                : contexts.FirstOrDefault(ctx => ctx.MatchesRequestedDevice(normalizedDeviceId));

            if (selectedContext is null)
            {
                return Results.Ok(new TelemetryTrendResponse
                {
                    DeviceId = string.Empty,
                    DeviceName = string.Empty,
                    ExternalDeviceId = deviceId?.Trim() ?? string.Empty,
                    PlantId = plantId,
                    SensorField = normalizedSensorField,
                    FromUtc = fromUtc,
                    ToUtc = toUtc,
                    IntervalMinutes = 1,
                    Points = []
                });
            }

            var rows = await db.TelemetryBuckets
                .AsNoTracking()
                .Where(x => x.BucketStartUtc >= fromUtc && x.BucketStartUtc <= toUtc)
                .OrderBy(x => x.BucketStartUtc)
                .ToListAsync();

            var matchingRows = rows
                .Where(selectedContext.MatchesBucketDevice)
                .OrderBy(x => x.BucketStartUtc)
                .ToList();

            return Results.Ok(BuildTrendResponse(
                selectedContext,
                matchingRows,
                normalizedMaxPoints,
                plantId,
                normalizedSensorField,
                fromUtc,
                toUtc));
        })
        .WithSummary("Get telemetry trend points")
        .WithDescription("Returns compact historical telemetry for charts. Data is stored per minute and downsampled when needed.")
        .Produces<TelemetryTrendResponse>(StatusCodes.Status200OK);

        telemetry.MapGet("/trends/all", [Authorize] async (
            int userId,
            ClaimsPrincipal principal,
            AppDbContext db,
            int? plantId,
            string? sensorField,
            int hours = 24,
            int maxPoints = 240) =>
        {
            if (!IsRequestUserAuthorized(principal, userId))
            {
                return Results.Forbid();
            }

            var normalizedHours = Math.Clamp(hours, 1, 24 * 30);
            var normalizedMaxPoints = Math.Clamp(maxPoints, 30, 1000);
            var normalizedSensorField = NormalizeSensorField(sensorField);

            var toUtc = DateTime.UtcNow;
            var fromUtc = toUtc.AddHours(-normalizedHours);

            var contexts = await BuildTelemetryContextsAsync(db, userId, plantId, normalizedSensorField);
            if (contexts.Count == 0)
            {
                return Results.Ok(Array.Empty<TelemetryTrendResponse>());
            }

            var rows = await db.TelemetryBuckets
                .AsNoTracking()
                .Where(x => x.BucketStartUtc >= fromUtc && x.BucketStartUtc <= toUtc)
                .OrderBy(x => x.BucketStartUtc)
                .ToListAsync();

            var result = contexts
                .Select(context =>
                {
                    var matchingRows = rows
                        .Where(context.MatchesBucketDevice)
                        .OrderBy(x => x.BucketStartUtc)
                        .ToList();

                    return BuildTrendResponse(
                        context,
                        matchingRows,
                        normalizedMaxPoints,
                        plantId,
                        normalizedSensorField,
                        fromUtc,
                        toUtc);
                })
                .ToList();

            return Results.Ok(result);
        })
        .WithSummary("Get telemetry trends for all sensor devices")
        .WithDescription("Returns one trend series per sensor device, including assigned plants, to build meaningful per-device charts.")
        .Produces<List<TelemetryTrendResponse>>(StatusCodes.Status200OK);
//====================================================================
// Dane testowe telemetryczne do testowania wykresów potem usunąć :>
//====================================================================
        telemetry.MapPost("/seed-test-data", [Authorize] async (
            int userId,
            ClaimsPrincipal principal,
            AppDbContext db,
            string? deviceId,
            int hours = 72,
            int stepMinutes = 5,
            bool replaceExisting = true) =>
        {
            if (!IsRequestUserAuthorized(principal, userId))
            {
                return Results.Forbid();
            }

            var normalizedHours = Math.Clamp(hours, 1, 24 * 30);
            var normalizedStepMinutes = Math.Clamp(stepMinutes, 1, 60);
            var normalizedDeviceId = string.IsNullOrWhiteSpace(deviceId) ? "esp32-test-node-01" : deviceId.Trim();

            var toUtc = TruncateToMinute(DateTime.UtcNow);
            var fromUtc = toUtc.AddHours(-normalizedHours);

            if (replaceExisting)
            {
                var existingBuckets = db.TelemetryBuckets.Where(x =>
                    x.DeviceId == normalizedDeviceId &&
                    x.BucketStartUtc >= fromUtc &&
                    x.BucketStartUtc <= toUtc);
                db.TelemetryBuckets.RemoveRange(existingBuckets);
                await db.SaveChangesAsync();
            }

            var random = new Random(42);
            var buckets = new List<Models.TelemetryBucket>();

            var totalMinutes = (int)(toUtc - fromUtc).TotalMinutes;
            for (var minute = 0; minute <= totalMinutes; minute += normalizedStepMinutes)
            {
                var bucketStart = fromUtc.AddMinutes(minute);
                var cycle = minute / 60.0;

                var temperatureAvg = 230 + 18 * Math.Sin(cycle / 3.0) + random.Next(-4, 5);
                var humidityAvg = 560 + 80 * Math.Cos(cycle / 4.0) + random.Next(-12, 13);
                var soilAvg = 620 + 130 * Math.Sin(cycle / 2.0) + random.Next(-20, 21);
                var waterAvg = 14 + 3.0 * Math.Sin(cycle / 6.0) + random.NextDouble() * 1.5;

                var sampleCount = random.Next(2, 5);
                var temperatureBase = (int)Math.Round(temperatureAvg);
                var humidityBase = (int)Math.Round(humidityAvg);
                var soilBase = (int)Math.Round(soilAvg);
                var waterBase = (int)Math.Round(waterAvg);

                buckets.Add(new Models.TelemetryBucket
                {
                    DeviceId = normalizedDeviceId,
                    BucketStartUtc = bucketStart,
                    SampleCount = sampleCount,
                    TemperatureSum = temperatureBase * sampleCount,
                    TemperatureMin = temperatureBase - random.Next(1, 4),
                    TemperatureMax = temperatureBase + random.Next(1, 4),
                    HumiditySum = humidityBase * sampleCount,
                    HumidityMin = humidityBase - random.Next(3, 12),
                    HumidityMax = humidityBase + random.Next(3, 12),
                    SoilMoistureSum = soilBase * sampleCount,
                    SoilMoistureMin = Math.Max(0, soilBase - random.Next(8, 40)),
                    SoilMoistureMax = soilBase + random.Next(8, 40),
                    WaterLevelSum = waterBase * sampleCount,
                    WaterLevelMin = Math.Max(0, waterBase - random.Next(1, 3)),
                    WaterLevelMax = waterBase + random.Next(1, 3),
                    LastPumpState = random.NextDouble() > 0.75,
                    LastLampState = random.NextDouble() > 0.55
                });
            }

            db.TelemetryBuckets.AddRange(buckets);
            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                response = "Wygenerowano dane testowe telemetryczne.",
                deviceId = normalizedDeviceId,
                hours = normalizedHours,
                stepMinutes = normalizedStepMinutes,
                insertedBuckets = buckets.Count,
                fromUtc,
                toUtc
            });
        })
        .WithSummary("Seed synthetic telemetry history")
        .WithDescription("Generates synthetic telemetry history for chart testing.")
        .Produces(StatusCodes.Status200OK);

        return app;
    }
//====================================================================
//--------------------------------------------------------------------
//====================================================================


    private static bool IsRequestUserAuthorized(ClaimsPrincipal principal, int routeUserId)
    {
        var claimValue = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub);

        return int.TryParse(claimValue, out var claimUserId) && claimUserId == routeUserId;
    }

    private static bool DeviceSupportsSensorField(Models.ActuatorDevice device, string sensorField)
    {
        if (device.SensorFields.Any(field => string.Equals(field, sensorField, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return MapTargetParameterToSensorField(device.TargetParameter) == sensorField;
    }

    private static async Task<List<TelemetryDeviceContext>> BuildTelemetryContextsAsync(
        AppDbContext db,
        int userId,
        int? plantId,
        string sensorField)
    {
        var devices = await db.ActuatorDevices
            .AsNoTracking()
            .Include(d => d.Plants)
            .Where(d => d.UserId == userId)
            .Where(d => d.DeviceKind == "sensor")
            .OrderBy(d => d.Name)
            .ThenBy(d => d.Id)
            .ToListAsync();

        var filtered = devices
            .Where(d => DeviceSupportsSensorField(d, sensorField))
            .Where(d => !plantId.HasValue || d.Plants.Any(p => p.Id == plantId.Value))
            .ToList();

        return filtered
            .Select(device => new TelemetryDeviceContext(
                device.Name,
                ResolveTelemetryDeviceId(device),
                device.Plants.Select(p => p.Id).Distinct().ToList(),
                device.Plants.Select(p => p.Name).Distinct().ToList()))
            .ToList();
    }

    private static TelemetryTrendResponse BuildTrendResponse(
        TelemetryDeviceContext context,
        List<Models.TelemetryBucket> rows,
        int maxPoints,
        int? selectedPlantId,
        string sensorField,
        DateTime fromUtc,
        DateTime toUtc)
    {
        if (rows.Count == 0)
        {
            return new TelemetryTrendResponse
            {
                DeviceId = context.ExternalDeviceId,
                DeviceName = context.DeviceName,
                ExternalDeviceId = context.ExternalDeviceId,
                PlantId = selectedPlantId,
                PlantIds = context.PlantIds,
                PlantNames = context.PlantNames,
                SensorField = sensorField,
                FromUtc = fromUtc,
                ToUtc = toUtc,
                IntervalMinutes = 1,
                Points = []
            };
        }

        var grouped = Downsample(rows, maxPoints);
        return new TelemetryTrendResponse
        {
            DeviceId = grouped.DeviceId,
            DeviceName = context.DeviceName,
            ExternalDeviceId = context.ExternalDeviceId,
            PlantId = selectedPlantId,
            PlantIds = context.PlantIds,
            PlantNames = context.PlantNames,
            SensorField = sensorField,
            FromUtc = fromUtc,
            ToUtc = toUtc,
            IntervalMinutes = grouped.IntervalMinutes,
            Points = grouped.Points
        };
    }

    private static string NormalizeIdentifier(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();
    }

    private static string ResolveTelemetryDeviceId(Models.ActuatorDevice? device)
    {
        if (device is null)
        {
            return string.Empty;
        }

        return string.IsNullOrWhiteSpace(device.ExternalDeviceId)
            ? string.Empty
            : device.ExternalDeviceId.Trim();
    }

    private static string NormalizeSensorField(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "soilMoistureAnalog";
        }

        var normalized = value.Trim();

        return normalized.ToLowerInvariant() switch
        {
            "soilmoisture" or "soilmoistureanalog" => "soilMoistureAnalog",
            "light" or "lightisdark" => "lightIsDark",
            "temperature" => "temperature",
            "humidity" => "humidity",
            "waterlevel" or "waterlevelcm" => "waterLevelCm",
            _ => "soilMoistureAnalog"
        };
    }

    private static string MapTargetParameterToSensorField(string? targetParameter)
    {
        if (string.IsNullOrWhiteSpace(targetParameter))
        {
            return "soilMoistureAnalog";
        }

        return targetParameter.Trim().ToLowerInvariant() switch
        {
            "soilmoisture" => "soilMoistureAnalog",
            "light" => "lightIsDark",
            "temperature" => "temperature",
            "humidity" => "humidity",
            "waterlevel" => "waterLevelCm",
            _ => "soilMoistureAnalog"
        };
    }

    private static (int IntervalMinutes, IReadOnlyList<TelemetryTrendPoint> Points, string DeviceId) Downsample(
        IReadOnlyList<Models.TelemetryBucket> rows,
        int maxPoints)
    {
        var baseBucketMinutes = ResolveBaseBucketMinutes(rows);
        var groupSize = Math.Max(1, (int)Math.Ceiling(rows.Count / (double)maxPoints));
        var points = new List<TelemetryTrendPoint>(Math.Min(maxPoints, rows.Count));

        for (var i = 0; i < rows.Count; i += groupSize)
        {
            var chunk = rows.Skip(i).Take(groupSize).ToList();
            var sampleCount = chunk.Sum(x => x.SampleCount);
            if (sampleCount == 0)
            {
                continue;
            }

            points.Add(new TelemetryTrendPoint
            {
                BucketStartUtc = chunk[0].BucketStartUtc,
                TemperatureAvg = chunk.Sum(x => x.TemperatureSum) / sampleCount,
                TemperatureMin = chunk.Min(x => x.TemperatureMin),
                TemperatureMax = chunk.Max(x => x.TemperatureMax),
                HumidityAvg = chunk.Sum(x => x.HumiditySum) / sampleCount,
                HumidityMin = chunk.Min(x => x.HumidityMin),
                HumidityMax = chunk.Max(x => x.HumidityMax),
                SoilMoistureAvg = chunk.Sum(x => x.SoilMoistureSum) / sampleCount,
                SoilMoistureMin = chunk.Min(x => x.SoilMoistureMin),
                SoilMoistureMax = chunk.Max(x => x.SoilMoistureMax),
                WaterLevelAvg = chunk.Sum(x => x.WaterLevelSum) / sampleCount,
                WaterLevelMin = chunk.Min(x => x.WaterLevelMin),
                WaterLevelMax = chunk.Max(x => x.WaterLevelMax),
                LightOnMinutes = chunk.Count(x => x.LastLampState) * baseBucketMinutes,
                LightOffMinutes = chunk.Count(x => !x.LastLampState) * baseBucketMinutes,
                LightOnPercent = (chunk.Count(x => x.LastLampState) * 100.0) / Math.Max(1, chunk.Count),
                SampleCount = sampleCount
            });
        }

        return (groupSize, points, rows[0].DeviceId);
    }

    private static DateTime TruncateToMinute(DateTime valueUtc)
    {
        return new DateTime(valueUtc.Year, valueUtc.Month, valueUtc.Day, valueUtc.Hour, valueUtc.Minute, 0, DateTimeKind.Utc);
    }

    private static int ResolveBaseBucketMinutes(IReadOnlyList<Models.TelemetryBucket> rows)
    {
        if (rows.Count < 2)
        {
            return 1;
        }

        var totalMinutes = (rows[^1].BucketStartUtc - rows[0].BucketStartUtc).TotalMinutes;
        if (totalMinutes <= 0)
        {
            return 1;
        }

        var approximate = (int)Math.Round(totalMinutes / (rows.Count - 1));
        return Math.Clamp(approximate, 1, 60);
    }

    private sealed record TelemetryDeviceContext(
        string DeviceName,
        string ExternalDeviceId,
        IReadOnlyList<int> PlantIds,
        IReadOnlyList<string> PlantNames)
    {
        private readonly string normalizedExternalDeviceId = NormalizeIdentifier(ExternalDeviceId);

        public bool MatchesBucketDevice(Models.TelemetryBucket bucket)
        {
            if (string.IsNullOrWhiteSpace(normalizedExternalDeviceId))
            {
                return false;
            }

            var normalizedBucketDeviceId = NormalizeIdentifier(bucket.DeviceId);
            if (string.IsNullOrWhiteSpace(normalizedBucketDeviceId))
            {
                return false;
            }

            return normalizedBucketDeviceId == normalizedExternalDeviceId
                || normalizedBucketDeviceId.StartsWith(normalizedExternalDeviceId + "-", StringComparison.Ordinal);
        }

        public bool MatchesRequestedDevice(string normalizedRequestedDevice)
        {
            if (string.IsNullOrWhiteSpace(normalizedRequestedDevice))
            {
                return true;
            }

            return normalizedRequestedDevice == normalizedExternalDeviceId
                || normalizedRequestedDevice.StartsWith(normalizedExternalDeviceId + "-", StringComparison.Ordinal)
                || normalizedExternalDeviceId.StartsWith(normalizedRequestedDevice + "-", StringComparison.Ordinal);
        }
    }
}