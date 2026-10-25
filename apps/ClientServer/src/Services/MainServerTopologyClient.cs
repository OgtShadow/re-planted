using ClientServer.Contracts;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace ClientServer.Services;

public interface IMainServerTopologyClient
{
    Task<ControllerTopologyDto?> GetTopologyAsync(int clientId, CancellationToken cancellationToken);
    Task<ControllerConfigurationSnapshot?> GetConfigurationAsync(int clientId, CancellationToken cancellationToken);
    Task ReportParameterRangeIssueAsync(int clientId, ParameterRangeIssue issue, CancellationToken cancellationToken);
    Task MarkPlantWateredAsync(int clientId, int plantId, CancellationToken cancellationToken);
}

public sealed class MainServerTopologyClient : IMainServerTopologyClient
{
    private readonly HttpClient _httpClient;
    private readonly MainServerApiOptions _options;
    private readonly IJwtTokenProvider _jwtTokenProvider;
    private readonly OfflineModeOptions _offlineOptions;
    private readonly ILogger<MainServerTopologyClient> _logger;

    public MainServerTopologyClient(
        HttpClient httpClient,
        IOptions<MainServerApiOptions> options,
        IOptions<OfflineModeOptions> offlineOptions,
        IJwtTokenProvider jwtTokenProvider,
        ILogger<MainServerTopologyClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _offlineOptions = offlineOptions.Value;
        _jwtTokenProvider = jwtTokenProvider;
        _logger = logger;
    }

    public async Task<ControllerConfigurationSnapshot?> GetConfigurationAsync(int clientId, CancellationToken cancellationToken)
    {
        var topology = await GetTopologyAsync(clientId, cancellationToken);
        if (topology is null)
        {
            return null;
        }

        var fetchedAtUtc = DateTime.UtcNow;
        var versionPayload = JsonSerializer.Serialize(topology);
        var version = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(versionPayload))).ToLowerInvariant();
        var validity = TimeSpan.FromMinutes(Math.Clamp(_offlineOptions.SnapshotValidityMinutes, 1, 10080));

        return new ControllerConfigurationSnapshot(
            clientId,
            version,
            fetchedAtUtc,
            fetchedAtUtc.Add(validity),
            topology with { SyncedAtUtc = fetchedAtUtc });
    }

    public async Task ReportParameterRangeIssueAsync(int clientId, ParameterRangeIssue issue, CancellationToken cancellationToken)
    {
        try
        {
            var path = _options.ParameterRangeAlertPath.Replace("{clientId}", clientId.ToString(), StringComparison.OrdinalIgnoreCase);
            using var request = new HttpRequestMessage(HttpMethod.Post, path)
            {
                Content = JsonContent.Create(new
                {
                    plantId = issue.PlantId,
                    parameter = issue.Parameter,
                    value = issue.Value,
                    min = issue.Min,
                    max = issue.Max,
                    sensorDeviceId = issue.SensorDeviceId
                })
            };
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _jwtTokenProvider.CreateClientToken(clientId));

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Nie udało się zgłosić parametru poza zakresem dla rośliny {PlantId}. Kod: {StatusCode}", issue.PlantId, response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Nie udało się zgłosić parametru poza zakresem dla rośliny {PlantId}.", issue.PlantId);
        }
    }

    public async Task MarkPlantWateredAsync(int clientId, int plantId, CancellationToken cancellationToken)
    {
        try
        {
            var path = _options.PlantWateredPath
                .Replace("{clientId}", clientId.ToString(), StringComparison.OrdinalIgnoreCase)
                .Replace("{plantId}", plantId.ToString(), StringComparison.OrdinalIgnoreCase);
            using var request = new HttpRequestMessage(HttpMethod.Post, path);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _jwtTokenProvider.CreateClientToken(clientId));

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Nie udało się zapisać podlania rośliny {PlantId}. Kod: {StatusCode}", plantId, response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Nie udało się zapisać podlania rośliny {PlantId}.", plantId);
        }
    }

    public async Task<ControllerTopologyDto?> GetTopologyAsync(int clientId, CancellationToken cancellationToken)
    {
        try
        {
            var path = _options.PlantsPath.Replace("{clientId}", clientId.ToString(), StringComparison.OrdinalIgnoreCase);
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _jwtTokenProvider.CreateClientToken(clientId));

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Synchronizacja topologii nie powiodła się dla klienta {ClientId}. Kod: {StatusCode}", clientId, response.StatusCode);
                return null;
            }

            var plants = await response.Content.ReadFromJsonAsync<List<PlantDto>>(cancellationToken);
            if (plants is null)
            {
                return null;
            }

            return new ControllerTopologyDto(
                clientId,
                DateTime.UtcNow,
                plants.Select(MapPlant).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Nie udało się zsynchronizować topologii z głównym serwerem dla klienta {ClientId}.", clientId);
            return null;
        }
    }

    private static ControllerPlantDto MapPlant(PlantDto plant)
    {
        return new ControllerPlantDto(
            plant.Id,
            string.IsNullOrWhiteSpace(plant.Name) ? "Bez nazwy" : plant.Name,
            string.IsNullOrWhiteSpace(plant.Species) ? "Unknown" : plant.Species,
            new ControllerPlantParametersDto(
                plant.Parameters?.WateringIntervalDays ?? 0,
                plant.Parameters?.Humidity?.Min ?? 0,
                plant.Parameters?.Humidity?.Max ?? 100,
                plant.Parameters?.LightHoursPerDay ?? 0,
                plant.Parameters?.Temperature?.Min ?? 0,
                plant.Parameters?.Temperature?.Max ?? 100,
                plant.Parameters?.SoilMoisture?.Min ?? 300,
                plant.Parameters?.SoilMoisture?.Max ?? 700,
                plant.Parameters?.LightScheduleStart,
                plant.Parameters?.LightScheduleEnd),
            (plant.Devices ?? []).Select(device => new ControllerDeviceDto(
                device.Id,
                string.IsNullOrWhiteSpace(device.Name) ? $"Urządzenie {device.Id}" : device.Name,
                string.IsNullOrWhiteSpace(device.DeviceKind) ? "actuator" : device.DeviceKind,
                device.TargetParameter ?? string.Empty,
                device.SensorFields ?? [],
                device.ExternalDeviceId ?? string.Empty,
                device.EffectType ?? string.Empty,
                device.EffectStrength,
                device.IsEnabled)).ToList(),
            plant.LastWatered);
    }

    private sealed record PlantDto(
        int Id,
        string Name,
        string Species,
        PlantParametersDto? Parameters,
        List<DeviceDto>? Devices,
        DateTime? LastWatered);

    private sealed record PlantParametersDto(
        int WateringIntervalDays,
        RangeDto? Humidity,
        int LightHoursPerDay,
        RangeDto? Temperature,
        RangeDto? SoilMoisture,
        TimeSpan? LightScheduleStart,
        TimeSpan? LightScheduleEnd);

    private sealed record RangeDto(int Min, int Max);

    private sealed record DeviceDto(
        int Id,
        string Name,
        string? DeviceKind,
        string? TargetParameter,
        List<string>? SensorFields,
        string? ExternalDeviceId,
        string? EffectType,
        double EffectStrength,
        bool IsEnabled);

}