namespace AuxbarClient.Models;

public class TrackInfo
{
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string? Album { get; set; }
    public string? AlbumArt { get; set; }
    public bool Playing { get; set; }
    public long? Progress { get; set; }  // milliseconds
    public long? Duration { get; set; }  // milliseconds
}

public class AuthResponse
{
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public UserInfo? User { get; set; }
    public string? Error { get; set; }
}

public class UserInfo
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string WidgetSlug { get; set; } = string.Empty;
}

public class WebSocketMessage
{
    public string Type { get; set; } = string.Empty;
    public object? Data { get; set; }
}
