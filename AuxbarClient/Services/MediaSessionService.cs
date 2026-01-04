using Windows.Media.Control;
using AuxbarClient.Models;

namespace AuxbarClient.Services;

public class MediaSessionService : IDisposable
{
    private GlobalSystemMediaTransportControlsSessionManager? _sessionManager;
    private GlobalSystemMediaTransportControlsSession? _currentSession;
    private readonly System.Timers.Timer _pollTimer;

    public event Action<TrackInfo?>? TrackChanged;

    private TrackInfo? _lastTrack;
    private string? _lastTrackId;

    // Expose current track for initial sync
    public TrackInfo? CurrentTrack => _lastTrack;

    public MediaSessionService()
    {
        _pollTimer = new System.Timers.Timer(1000); // Poll every second
        _pollTimer.Elapsed += async (s, e) => await PollCurrentTrack();
    }

    public async Task InitializeAsync()
    {
        try
        {
            _sessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            _sessionManager.CurrentSessionChanged += OnCurrentSessionChanged;
            UpdateCurrentSession();
            _pollTimer.Start();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to initialize media session: {ex.Message}");
        }
    }

    private void OnCurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args)
    {
        UpdateCurrentSession();
    }

    private void UpdateCurrentSession()
    {
        _currentSession = _sessionManager?.GetCurrentSession();
    }

    private async Task PollCurrentTrack()
    {
        try
        {
            if (_currentSession == null)
            {
                if (_lastTrack != null)
                {
                    _lastTrack = null;
                    _lastTrackId = null;
                    TrackChanged?.Invoke(null);
                }
                return;
            }

            var mediaProperties = await _currentSession.TryGetMediaPropertiesAsync();
            var playbackInfo = _currentSession.GetPlaybackInfo();
            var timelineProperties = _currentSession.GetTimelineProperties();

            if (mediaProperties == null)
            {
                if (_lastTrack != null)
                {
                    _lastTrack = null;
                    _lastTrackId = null;
                    TrackChanged?.Invoke(null);
                }
                return;
            }

            var trackId = $"{mediaProperties.Title}-{mediaProperties.Artist}";
            var isPlaying = playbackInfo?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

            // Get album art as base64 if available
            string? albumArtBase64 = null;
            try
            {
                var thumbnail = mediaProperties.Thumbnail;
                if (thumbnail != null)
                {
                    using var stream = await thumbnail.OpenReadAsync();
                    using var memoryStream = new MemoryStream();
                    await stream.AsStreamForRead().CopyToAsync(memoryStream);
                    albumArtBase64 = $"data:image/png;base64,{Convert.ToBase64String(memoryStream.ToArray())}";
                }
            }
            catch
            {
                // Album art not available
            }

            var track = new TrackInfo
            {
                Title = mediaProperties.Title ?? "Unknown",
                Artist = mediaProperties.Artist ?? "Unknown",
                Album = mediaProperties.AlbumTitle,
                AlbumArt = albumArtBase64,
                Playing = isPlaying,
                Progress = (long?)timelineProperties?.Position.TotalMilliseconds,
                Duration = (long?)timelineProperties?.EndTime.TotalMilliseconds
            };

            // Only fire event if something changed
            bool hasChanged = _lastTrackId != trackId ||
                             _lastTrack?.Playing != track.Playing ||
                             HasSignificantProgressChange(_lastTrack?.Progress, track.Progress, track.Playing);

            if (hasChanged)
            {
                _lastTrackId = trackId;
                _lastTrack = track;
                TrackChanged?.Invoke(track);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error polling track: {ex.Message}");
        }
    }

    private bool HasSignificantProgressChange(long? lastProgress, long? currentProgress, bool isPlaying)
    {
        if (lastProgress == null || currentProgress == null) return true;
        if (!isPlaying) return false;

        // Expected progress after 1 second if playing
        var expectedProgress = lastProgress.Value + 1500; // 1.5s tolerance
        var drift = Math.Abs(currentProgress.Value - expectedProgress);

        // User seeked if drift > 2.5 seconds
        return drift > 2500;
    }

    public void Dispose()
    {
        _pollTimer.Stop();
        _pollTimer.Dispose();
    }
}
