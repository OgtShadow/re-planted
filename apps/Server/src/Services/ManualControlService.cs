using System.Net.Http.Json;
using RePlanted.Server.Contracts.Devices;

namespace RePlanted.Server.Services;

public sealed class ManualControlService(IHttpClientFactory httpClientFactory, IConfiguration configuration)
{
    public async Task<(bool Success, int StatusCode, string? Error)> StartPumpAsync(
        int userId,
        string deviceId,
        int durationMs,
        CancellationToken cancellationToken)
    {
        try
        {
            var client = httpClientFactory.CreateClient(nameof(ManualControlService));
            var baseUrl = (configuration["ClientServerBaseUrl"] ?? "http://localhost:8082").TrimEnd('/');
            using var response = await client.PostAsJsonAsync(
                $"{baseUrl}/api/client-server/controllers/{userId}/devices/{Uri.EscapeDataString(deviceId)}/pump",
                new ManualPumpRequest { DurationMs = durationMs },
                cancellationToken);
            return await ReadResultAsync(response, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return (false, StatusCodes.Status503ServiceUnavailable, "Kontroler IoT jest niedostępny.");
        }
    }

    public async Task<(bool Success, int StatusCode, string? Error)> StopPumpAsync(
        int userId,
        string deviceId,
        CancellationToken cancellationToken)
    {
        try
        {
            var client = httpClientFactory.CreateClient(nameof(ManualControlService));
            var baseUrl = (configuration["ClientServerBaseUrl"] ?? "http://localhost:8082").TrimEnd('/');
            using var response = await client.PostAsync(
                $"{baseUrl}/api/client-server/controllers/{userId}/devices/{Uri.EscapeDataString(deviceId)}/stop",
                null,
                cancellationToken);
            return await ReadResultAsync(response, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return (false, StatusCodes.Status503ServiceUnavailable, "Kontroler IoT jest niedostępny.");
        }
    }

    private static async Task<(bool Success, int StatusCode, string? Error)> ReadResultAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return (true, (int)response.StatusCode, null);
        }

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(cancellationToken: cancellationToken);
        return (false, (int)response.StatusCode, error?.Response ?? "Nie udało się wykonać komendy.");
    }

    private sealed class ErrorResponse
    {
        public string? Response { get; set; }
        public string? response { get; set; }
    }
}
