using ClientServer.Contracts;
using Microsoft.Extensions.Options;

namespace ClientServer.Services;

public interface IMainServerTopologyClient
{
    Task<ControllerTopologyDto?> GetTopologyAsync(int clientId, CancellationToken cancellationToken);
}

public sealed class MainServerTopologyClient : IMainServerTopologyClient
{
    private readonly HttpClient _httpClient;
    private readonly MainServerApiOptions _options;
    private readonly IJwtTokenProvider _jwtTokenProvider;
    private readonly ILogger<MainServerTopologyClient> _logger;

    public MainServerTopologyClient(
        HttpClient httpClient,
        IOptions<MainServerApiOptions> options,
        IJwtTokenProvider jwtTokenProvider,
        ILogger<MainServerTopologyClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _jwtTokenProvider = jwtTokenProvider;
        _logger = logger;
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
                plant.Parameters?.Temperature?.Max ?? 100),
            (plant.Devices ?? []).Select(device => new ControllerDeviceDto(
                device.Id,
                string.IsNullOrWhiteSpace(device.Name) ? $"Urządzenie {device.Id}" : device.Name,
                string.IsNullOrWhiteSpace(device.DeviceKind) ? "actuator" : device.DeviceKind,
                device.TargetParameter ?? string.Empty,
                device.SensorFields ?? [],
                device.ExternalDeviceId ?? string.Empty,
                device.EffectType ?? string.Empty,
                device.EffectStrength,
                device.IsEnabled)).ToList());
    }

    private sealed record PlantDto(
        int Id,
        string Name,
        string Species,
        PlantParametersDto? Parameters,
        List<DeviceDto>? Devices);

    private sealed record PlantParametersDto(
        int WateringIntervalDays,
        RangeDto? Humidity,
        int LightHoursPerDay,
        RangeDto? Temperature);

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