using DiscordRPC;
using DiscordRPC.Logging;
using AuxbarClient.Models;

namespace AuxbarClient.Services;

public class DiscordRpcService : IDisposable
{
    // Discord Application ID
    // Create your application at: https://discord.com/developers/applications
    // 1. Click "New Application" and name it "Auxbar"
    // 2. Copy the Application ID and paste it here
    // 3. Go to "Rich Presence" > "Art Assets" and upload images:
    //    - "auxbar_logo" - Your main logo (large image)
    //    - "playing" - Playing icon (small image)
    //    - "paused" - Paused icon (small image)
    private const string ApplicationId = "1457077045090717717";

    private DiscordRpcClient? _client;
    private TrackInfo? _currentTrack;
    private bool _isEnabled = true;
    private bool _isInitialized = false;
    private readonly object _lock = new();

    public event Action? Connected;
    public event Action? Disconnected;
    public event Action<string>? Error;

    // Settings properties
    public bool ShowAlbumName { get; set; } = true;
    public bool ShowPlaybackProgress { get; set; } = true;
    public bool ShowButton { get; set; } = true;

    // Widget slug for album art URL (set after login)
    public string? WidgetSlug { get; set; }
    private const string BaseUrl = "https://auxbar.me";

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled == value) return;

            _isEnabled = value;
            Console.WriteLine($"Discord RPC IsEnabled changed to: {_isEnabled}");

            if (!_isEnabled)
            {
                ClearPresence();
            }
            else if (_currentTrack != null)
            {
                // Re-initialize if needed and update presence
                if (!_isInitialized)
                {
                    Initialize();
                }
                UpdatePresence(_currentTrack);
            }
        }
    }

    public bool IsConnected => _client?.IsInitialized ?? false;

    public void Initialize()
    {
        lock (_lock)
        {
            if (_isInitialized) return;

            try
            {
                _client = new DiscordRpcClient(ApplicationId)
                {
                    Logger = new ConsoleLogger { Level = LogLevel.Warning }
                };

                _client.OnReady += (sender, e) =>
                {
                    Console.WriteLine($"Discord RPC connected as {e.User.Username}");
                    Connected?.Invoke();
                };

                _client.OnClose += (sender, e) =>
                {
                    Console.WriteLine($"Discord RPC disconnected: {e.Reason}");
                    Disconnected?.Invoke();
                };

                _client.OnError += (sender, e) =>
                {
                    Console.WriteLine($"Discord RPC error: {e.Message}");
                    Error?.Invoke(e.Message);
                };

                _client.OnConnectionFailed += (sender, e) =>
                {
                    Console.WriteLine($"Discord RPC connection failed: {e.FailedPipe}");
                    Error?.Invoke($"Connection failed on pipe {e.FailedPipe}");
                };

                _client.Initialize();
                _isInitialized = true;

                // Set initial idle presence
                SetIdlePresence();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to initialize Discord RPC: {ex.Message}");
                Error?.Invoke(ex.Message);
            }
        }
    }

    public void UpdatePresence(TrackInfo track)
    {
        if (!_isEnabled || _client == null || !_client.IsInitialized)
        {
            Console.WriteLine($"Discord RPC UpdatePresence skipped: enabled={_isEnabled}, client={_client != null}, initialized={_client?.IsInitialized}");
            return;
        }

        _currentTrack = track;

        try
        {
            // Prepare display strings with fallbacks
            var title = TruncateString(track.Title, 128, "Unknown Track");
            var artist = track.Artist;
            var artistDisplay = string.IsNullOrWhiteSpace(artist) || artist.Length < 2
                ? "Unknown Artist"
                : $"by {artist}";

            // Add progress to artist display if enabled (format: "by Artist • 2:34 / 4:12")
            if (ShowPlaybackProgress && track.Progress.HasValue && track.Duration.HasValue && track.Duration.Value > 0)
            {
                var currentTime = FormatTime(track.Progress.Value);
                var totalTime = FormatTime(track.Duration.Value);
                artistDisplay = $"{artistDisplay} • {currentTime} / {totalTime}";
            }

            artistDisplay = TruncateString(artistDisplay, 128, "Unknown Artist");

            Console.WriteLine($"Discord RPC UpdatePresence: Title='{title}', Artist='{artistDisplay}', Playing={track.Playing}");

            // Build large image text based on settings
            string largeImageText = "Auxbar";
            if (ShowAlbumName && !string.IsNullOrEmpty(track.Album))
            {
                largeImageText = TruncateString(track.Album, 128, "Auxbar");
            }

            // Determine large image: use external album art URL if available, else fallback to asset
            // LargeImageKey supports direct URLs - Discord will handle proxying them
            string largeImageKey = "auxbar_logo"; // Default fallback to uploaded asset
            if (!string.IsNullOrEmpty(WidgetSlug) && !string.IsNullOrEmpty(track.AlbumArt))
            {
                // Use path-based cache busting instead of query params
                // Discord may reject URLs with query parameters, but path segments work
                var trackHash = Math.Abs($"{track.Title}-{track.Artist}".GetHashCode());
                largeImageKey = $"{BaseUrl}/api/widget/album-art/{WidgetSlug}/{trackHash}";
                Console.WriteLine($"Discord RPC using album art URL: {largeImageKey}");
            }
            else if (string.IsNullOrEmpty(WidgetSlug))
            {
                Console.WriteLine("Discord RPC: No widget slug set, using default asset");
            }
            else
            {
                Console.WriteLine("Discord RPC: No album art available, using default asset");
            }

            // Build assets - LargeImageKey accepts both asset names and external URLs
            var assets = new Assets
            {
                LargeImageKey = largeImageKey,
                LargeImageText = largeImageText,
                SmallImageKey = track.Playing ? "playing" : "paused",
                SmallImageText = track.Playing ? "Playing" : "Paused"
            };

            var presence = new RichPresence
            {
                Details = title,
                State = artistDisplay,
                Assets = assets
            };

            // Note: We're showing progress in the State field instead of using Discord timestamps
            // Discord timestamps auto-increment which causes issues when pausing/resuming

            // Add button to open Auxbar website if enabled
            if (ShowButton)
            {
                presence.Buttons = new DiscordRPC.Button[]
                {
                    new DiscordRPC.Button
                    {
                        Label = "Get Auxbar",
                        Url = "https://auxbar.me"
                    }
                };
            }

            _client.SetPresence(presence);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to update Discord presence: {ex.Message}");
        }
    }

    public void SetIdlePresence()
    {
        if (!_isEnabled || _client == null || !_client.IsInitialized)
            return;

        _currentTrack = null;

        try
        {
            var presence = new RichPresence
            {
                Details = "Not playing anything",
                State = "Idle",
                Assets = new Assets
                {
                    LargeImageKey = "auxbar_logo",
                    LargeImageText = "Auxbar - Music Widget for Streamers"
                },
                Buttons = new DiscordRPC.Button[]
                {
                    new DiscordRPC.Button
                    {
                        Label = "Get Auxbar",
                        Url = "https://auxbar.me"
                    }
                }
            };

            _client.SetPresence(presence);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to set idle presence: {ex.Message}");
        }
    }

    public void ClearPresence()
    {
        if (_client == null || !_client.IsInitialized)
            return;

        try
        {
            _client.ClearPresence();
            _currentTrack = null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to clear Discord presence: {ex.Message}");
        }
    }

    private static string TruncateString(string? str, int maxLength, string fallback = "Unknown")
    {
        // Discord RPC requires at least 2 characters for Details/State
        if (string.IsNullOrWhiteSpace(str) || str.Length < 2)
            return fallback;

        if (str.Length <= maxLength)
            return str;

        return str[..(maxLength - 3)] + "...";
    }

    private static string FormatTime(long milliseconds)
    {
        var time = TimeSpan.FromMilliseconds(milliseconds);
        if (time.TotalHours >= 1)
        {
            return $"{(int)time.TotalHours}:{time.Minutes:D2}:{time.Seconds:D2}";
        }
        return $"{time.Minutes}:{time.Seconds:D2}";
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_client != null)
            {
                try
                {
                    _client.ClearPresence();
                    _client.Dispose();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error disposing Discord RPC client: {ex.Message}");
                }
                finally
                {
                    _client = null;
                    _isInitialized = false;
                }
            }
        }
    }
}
