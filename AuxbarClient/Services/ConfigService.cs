using System.Text.Json;
using System.Text.Json.Serialization;

namespace AuxbarClient.Services;

public class DiscordSettings
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("showAlbumName")]
    public bool ShowAlbumName { get; set; } = true;

    [JsonPropertyName("showPlaybackProgress")]
    public bool ShowPlaybackProgress { get; set; } = true;

    [JsonPropertyName("showButton")]
    public bool ShowButton { get; set; } = true;
}

public class AppConfig
{
    [JsonPropertyName("accessToken")]
    public string? AccessToken { get; set; }

    [JsonPropertyName("refreshToken")]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("widgetSlug")]
    public string? WidgetSlug { get; set; }

    [JsonPropertyName("discord")]
    public DiscordSettings Discord { get; set; } = new();
}

public static class ConfigService
{
    private static AppConfig? _config;
    private static readonly object _lock = new();

    public static string ConfigPath
    {
        get
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "Auxbar", "config.json");
        }
    }

    public static AppConfig Load()
    {
        lock (_lock)
        {
            if (_config != null)
                return _config;

            try
            {
                if (File.Exists(ConfigPath))
                {
                    var json = File.ReadAllText(ConfigPath);

                    // Try to load new format first
                    _config = JsonSerializer.Deserialize<AppConfig>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    // Handle migration from old format (simple dictionary)
                    if (_config == null)
                    {
                        var oldConfig = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                        if (oldConfig != null)
                        {
                            _config = new AppConfig
                            {
                                AccessToken = oldConfig.GetValueOrDefault("accessToken"),
                                RefreshToken = oldConfig.GetValueOrDefault("refreshToken")
                            };
                            Save(); // Save in new format
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load config: {ex.Message}");
            }

            _config ??= new AppConfig();
            return _config;
        }
    }

    public static void Save()
    {
        lock (_lock)
        {
            try
            {
                _config ??= new AppConfig();

                var directory = Path.GetDirectoryName(ConfigPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(_config, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(ConfigPath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to save config: {ex.Message}");
            }
        }
    }

    public static void UpdateTokens(string? accessToken, string? refreshToken)
    {
        var config = Load();
        config.AccessToken = accessToken;
        config.RefreshToken = refreshToken;
        Save();
    }

    public static void ClearTokens()
    {
        var config = Load();
        config.AccessToken = null;
        config.RefreshToken = null;
        config.WidgetSlug = null;
        Save();
    }

    public static void UpdateWidgetSlug(string? widgetSlug)
    {
        var config = Load();
        config.WidgetSlug = widgetSlug;
        Save();
    }

    public static void UpdateDiscordSettings(DiscordSettings settings)
    {
        var config = Load();
        config.Discord = settings;
        Save();
    }

    public static void Reset()
    {
        lock (_lock)
        {
            _config = null;
            if (File.Exists(ConfigPath))
            {
                File.Delete(ConfigPath);
            }
        }
    }
}
