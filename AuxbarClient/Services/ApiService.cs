using System.Net.Http.Json;
using System.Text.Json;
using AuxbarClient.Models;

namespace AuxbarClient.Services;

public class ApiService : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private string? _accessToken;
    private string? _refreshToken;
    private System.Threading.Timer? _refreshTimer;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    public string? AccessToken => _accessToken;
    public bool IsAuthenticated => !string.IsNullOrEmpty(_accessToken);

    public event Action? TokenRefreshed;
    public event Action? TokenRefreshFailed;

    public ApiService(string baseUrl = "https://auxbar.me")
    {
        _baseUrl = baseUrl;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseUrl)
        };
    }

    public async Task<AuthResponse> LoginAsync(string email, string password)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/auth/login", new { email, password });
            var result = await response.Content.ReadFromJsonAsync<AuthResponse>();

            if (result?.AccessToken != null)
            {
                _accessToken = result.AccessToken;
                _refreshToken = result.RefreshToken;
                SaveTokens();
                StartAutoRefresh();
            }

            return result ?? new AuthResponse { Error = "Unknown error" };
        }
        catch (Exception ex)
        {
            return new AuthResponse { Error = ex.Message };
        }
    }

    public async Task<bool> RefreshTokenAsync()
    {
        if (string.IsNullOrEmpty(_refreshToken)) return false;

        // Use semaphore to prevent multiple simultaneous refresh attempts
        await _refreshLock.WaitAsync();
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = _refreshToken });

            if (!response.IsSuccessStatusCode)
            {
                TokenRefreshFailed?.Invoke();
                ClearTokens();
                return false;
            }

            var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
            if (result?.AccessToken != null)
            {
                _accessToken = result.AccessToken;
                _refreshToken = result.RefreshToken;
                SaveTokens();
                StartAutoRefresh(); // Restart timer with new token
                TokenRefreshed?.Invoke();
                return true;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Token refresh error: {ex.Message}");
            TokenRefreshFailed?.Invoke();
        }
        finally
        {
            _refreshLock.Release();
        }

        ClearTokens();
        return false;
    }

    /// <summary>
    /// Starts automatic token refresh timer.
    /// Refreshes token every 12 minutes (token expires in 15 minutes).
    /// </summary>
    private void StartAutoRefresh()
    {
        StopAutoRefresh();

        // Refresh every 12 minutes (720000ms) - before 15-minute expiration
        var refreshInterval = TimeSpan.FromMinutes(12);

        _refreshTimer = new System.Threading.Timer(
            async _ => await RefreshTokenAsync(),
            null,
            refreshInterval,
            refreshInterval
        );

        Console.WriteLine("Auto-refresh timer started (12-minute interval)");
    }

    private void StopAutoRefresh()
    {
        _refreshTimer?.Dispose();
        _refreshTimer = null;
    }

    public void LoadTokens()
    {
        var configPath = GetConfigPath();
        if (File.Exists(configPath))
        {
            try
            {
                var json = File.ReadAllText(configPath);
                var config = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                if (config != null)
                {
                    config.TryGetValue("accessToken", out _accessToken);
                    config.TryGetValue("refreshToken", out _refreshToken);
                }
            }
            catch
            {
                // Config file corrupted
            }
        }
    }

    private void SaveTokens()
    {
        var configPath = GetConfigPath();
        var config = new Dictionary<string, string?>
        {
            ["accessToken"] = _accessToken,
            ["refreshToken"] = _refreshToken
        };

        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
        File.WriteAllText(configPath, JsonSerializer.Serialize(config));
    }

    public void ClearTokens()
    {
        StopAutoRefresh();
        _accessToken = null;
        _refreshToken = null;

        var configPath = GetConfigPath();
        if (File.Exists(configPath))
        {
            File.Delete(configPath);
        }
    }

    private static string GetConfigPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "Auxbar", "config.json");
    }

    public string GetWebSocketUrl()
    {
        var wsUrl = _baseUrl.Replace("http://", "ws://").Replace("https://", "wss://");
        return $"{wsUrl}/ws?type=client&token={_accessToken}";
    }

    public void Dispose()
    {
        StopAutoRefresh();
        _refreshLock?.Dispose();
        _httpClient?.Dispose();
    }
}
