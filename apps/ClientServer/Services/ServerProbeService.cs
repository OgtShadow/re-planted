using ClientServer.Contracts;
using Microsoft.Extensions.Options;

namespace ClientServer.Services;

public sealed class ServerProbeService : IServerProbeService
{
    private readonly HttpClient _httpClient;
    private readonly ServerApiOptions _options;
    private readonly ILogger<ServerProbeService> _logger;

    public ServerProbeService(
        HttpClient httpClient,
        IOptions<ServerApiOptions> options,
        ILogger<ServerProbeService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ServerCheckResponse> CheckAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.GetAsync(_options.CommunicationPath, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            return new ServerCheckResponse(
                Reachable: response.IsSuccessStatusCode,
                StatusCode: (int)response.StatusCode,
                Source: _httpClient.BaseAddress?.ToString() ?? _options.BaseUrl,
                Message: response.IsSuccessStatusCode
                    ? "Polaczenie z glownym serwerem dziala"
                    : "Glowny serwer odpowiedzial, ale zwrocil blad",
                ResponseBody: body,
                UtcTime: DateTime.UtcNow.ToString("O")
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Nie udalo sie nawiazac polaczenia z glownym serwerem");

            return new ServerCheckResponse(
                Reachable: false,
                StatusCode: 0,
                Source: _httpClient.BaseAddress?.ToString() ?? _options.BaseUrl,
                Message: "Nie udalo sie polaczyc z glownym serwerem",
                ResponseBody: ex.Message,
                UtcTime: DateTime.UtcNow.ToString("O")
            );
        }
    }
}
