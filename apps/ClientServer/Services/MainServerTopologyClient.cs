using ClientServer.Contracts;
using Microsoft.Extensions.Options;

namespace ClientServer.Services;

public interface IMainServerTopologyClient
{
    Task<ControllerTopologyDto?> GetTopologyAsync(CancellationToken cancellationToken);
}

public sealed class MainServerTopologyClient : IMainServerTopologyClient
{
    private readonly HttpClient _httpClient;
    private readonly MainServerApiOptions _options;
    private readonly IoTControllerOptions _controllerOptions;
    private readonly ILogger<MainServerTopologyClient> _logger;

    public MainServerTopologyClient(
        HttpClient httpClient,
        IOptions<MainServerApiOptions> options,
        IOptions<IoTControllerOptions> controllerOptions,
        ILogger<MainServerTopologyClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _controllerOptions = controllerOptions.Value;
        _logger = logger;
    }

    public async Task<ControllerTopologyDto?> GetTopologyAsync(CancellationToken cancellationToken)
    {
        try
        {
            var path = _options.PlantsPath.Replace("{clientId}", _controllerOptions.ClientId.ToString(), StringComparison.OrdinalIgnoreCase);
            var plants = await _httpClient.GetFromJsonAsync<List<PlantDto>>(path, cancellationToken);
            if (plants is null)
            {
                return null;
            }

            return new ControllerTopologyDto(
                _controllerOptions.ClientId,
                DateTime.UtcNow,
                plants.Select(MapPlant).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Nie udało się zsynchronizować topologii z głównym serwerem.");
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
                device.TargetParameter ?? string.Empty,
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
        string? TargetParameter,
        string? EffectType,
        double EffectStrength,
        bool IsEnabled);
}