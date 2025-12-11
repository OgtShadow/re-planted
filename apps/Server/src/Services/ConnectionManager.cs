using System.Net.Http.Json;

namespace RePlanted.Server.Services;

public class ConnectionManager
{
    private readonly HttpClient httpClient;
    private readonly string clientBaseUrl;
    private readonly ILogger<ConnectionManager> logger;

    public ConnectionManager(HttpClient httpClient, IConfiguration configuration, ILogger<ConnectionManager> logger)
    {
        this.httpClient = httpClient;
        this.clientBaseUrl = configuration["ClientBaseUrl"] ?? "http://localhost:5173";
        this.logger = logger;
    }

    public async Task<T?> GetAsync<T>(string endpoint)
    {
        try
        {
            var response = await httpClient.GetAsync($"{clientBaseUrl}{endpoint}");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<T>();
        }
        catch (Exception ex)
        {
            logger.LogError($"GET request failed: {ex.Message}");
            throw;
        }
    }

    public async Task<string> GetTextAsync(string endpoint)
    {
        try
        {
            var response = await httpClient.GetAsync($"{clientBaseUrl}{endpoint}");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            logger.LogError($"GET text request failed: {ex.Message}");
            throw;
        }
    }

    public async Task<T?> PostAsync<T>(string endpoint, object data)
    {
        try
        {
            var response = await httpClient.PostAsJsonAsync($"{clientBaseUrl}{endpoint}", data);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<T>();
        }
        catch (Exception ex)
        {
            logger.LogError($"POST request failed: {ex.Message}");
            throw;
        }
    }

    public async Task<T?> PutAsync<T>(string endpoint, object data)
    {
        try
        {
            var response = await httpClient.PutAsJsonAsync($"{clientBaseUrl}{endpoint}", data);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<T>();
        }
        catch (Exception ex)
        {
            logger.LogError($"PUT request failed: {ex.Message}");
            throw;
        }
    }

    public async Task<T?> DeleteAsync<T>(string endpoint)
    {
        try
        {
            var response = await httpClient.DeleteAsync($"{clientBaseUrl}{endpoint}");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<T>();
        }
        catch (Exception ex)
        {
            logger.LogError($"DELETE request failed: {ex.Message}");
            throw;
        }
    }
}
