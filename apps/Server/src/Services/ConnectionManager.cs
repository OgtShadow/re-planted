using System.Net.Http.Json;

namespace RePlanted.Server.Services;

public class ConnectionManager
{
    private readonly HttpClient _httpClient;
    private readonly string _clientBaseUrl;
    private readonly ILogger<ConnectionManager> _logger;

    public ConnectionManager(HttpClient httpClient, IConfiguration configuration, ILogger<ConnectionManager> logger)
    {
        _httpClient = httpClient;
        _clientBaseUrl = configuration["ClientBaseUrl"] ?? "http://localhost:5173";
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string endpoint)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_clientBaseUrl}{endpoint}");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<T>();
        }
        catch (Exception ex)
        {
            _logger.LogError($"GET request failed: {ex.Message}");
            throw;
        }
    }

    public async Task<string> GetTextAsync(string endpoint)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_clientBaseUrl}{endpoint}");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError($"GET text request failed: {ex.Message}");
            throw;
        }
    }

    public async Task<T?> PostAsync<T>(string endpoint, object data)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"{_clientBaseUrl}{endpoint}", data);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<T>();
        }
        catch (Exception ex)
        {
            _logger.LogError($"POST request failed: {ex.Message}");
            throw;
        }
    }

    public async Task<T?> PutAsync<T>(string endpoint, object data)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"{_clientBaseUrl}{endpoint}", data);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<T>();
        }
        catch (Exception ex)
        {
            _logger.LogError($"PUT request failed: {ex.Message}");
            throw;
        }
    }

    public async Task<T?> DeleteAsync<T>(string endpoint)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"{_clientBaseUrl}{endpoint}");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<T>();
        }
        catch (Exception ex)
        {
            _logger.LogError($"DELETE request failed: {ex.Message}");
            throw;
        }
    }
}
