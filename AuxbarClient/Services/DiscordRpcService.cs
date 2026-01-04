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
            artistDisplay = TruncateString(artistDisplay, 128, "Unknown Artist");

            Console.WriteLine($"Discord RPC UpdatePresence: Title='{title}', Artist='{artistDisplay}', Playing={track.Playing}");

            // Use album art URL from server if we have a widget slug
            // The server serves the album art that was sent by this client
            var largeImageKey = "auxbar_logo";
            if (!string.IsNullOrEmpty(WidgetSlug) && !string.IsNullOrEmpty(track.AlbumArt))
            {
                // Discord RPC supports external HTTPS URLs for images
                // Use the server endpoint that serves the album art we sent
                largeImageKey = $"{BaseUrl}/api/widget/album-art/{WidgetSlug}";
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

            // Build large image text based on settings
            string largeImageText = "Auxbar";
            if (ShowAlbumName && !string.IsNullOrEmpty(track.Album))
            {
                largeImageText = TruncateString(track.Album, 128, "Auxbar");
            }

            var presence = new RichPresence
            {
                Details = title,
                State = artistDisplay,
                Assets = new Assets
                {
                    LargeImageKey = largeImageKey,
                    LargeImageText = largeImageText,
                    SmallImageKey = track.Playing ? "playing" : "paused",
                    SmallImageText = track.Playing ? "Playing" : "Paused"
                }
            };

            // Add timestamps for elapsed time if playing and we have progress info
            if (ShowPlaybackProgress && track.Playing && track.Progress.HasValue && track.Duration.HasValue && track.Duration.Value > 0)
            {
                var now = DateTime.UtcNow;
                var elapsed = TimeSpan.FromMilliseconds(track.Progress.Value);
                var remaining = TimeSpan.FromMilliseconds(track.Duration.Value - track.Progress.Value);

                // Show time elapsed (start time in the past)
                presence.Timestamps = new Timestamps
                {
                    Start = now - elapsed,
                    End = now + remaining
                };
            }

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
