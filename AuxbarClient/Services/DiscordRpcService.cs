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

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            _isEnabled = value;
            if (!_isEnabled)
            {
                ClearPresence();
            }
            else if (_currentTrack != null)
            {
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
            return;

        _currentTrack = track;

        try
        {
            // Use album art URL if available, otherwise fall back to auxbar_logo asset
            var largeImageKey = !string.IsNullOrEmpty(track.AlbumArt) ? track.AlbumArt : "auxbar_logo";

            var presence = new RichPresence
            {
                Details = TruncateString(track.Title, 128),
                State = TruncateString($"by {track.Artist}", 128),
                Assets = new Assets
                {
                    LargeImageKey = largeImageKey,
                    LargeImageText = string.IsNullOrEmpty(track.Album) ? "Auxbar" : TruncateString(track.Album, 128),
                    SmallImageKey = track.Playing ? "playing" : "paused",
                    SmallImageText = track.Playing ? "Playing" : "Paused"
                }
            };

            // Add timestamps for elapsed time if playing and we have progress info
            if (track.Playing && track.Progress.HasValue && track.Duration.HasValue && track.Duration.Value > 0)
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

            // Add button to open Auxbar website
            presence.Buttons = new DiscordRPC.Button[]
            {
                new DiscordRPC.Button
                {
                    Label = "Get Auxbar",
                    Url = "https://auxbar.me"
                }
            };

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

    private static string TruncateString(string? str, int maxLength)
    {
        if (string.IsNullOrEmpty(str))
            return string.Empty;

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
