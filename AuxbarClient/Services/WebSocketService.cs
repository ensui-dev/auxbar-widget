using System.Text.Json;
using Websocket.Client;
using AuxbarClient.Models;

namespace AuxbarClient.Services;

public class WebSocketService : IDisposable
{
    private WebsocketClient? _client;
    private readonly ApiService _apiService;
    private bool _isConnected;

    public event Action? Connected;
    public event Action? Disconnected;
    public event Action<string>? Error;

    public bool IsConnected => _isConnected;

    public WebSocketService(ApiService apiService)
    {
        _apiService = apiService;
    }

    public async Task ConnectAsync()
    {
        if (!_apiService.IsAuthenticated)
        {
            Error?.Invoke("Not authenticated");
            return;
        }

        // Dispose existing client if any
        if (_client != null)
        {
            _client.Dispose();
            _client = null;
        }

        var wsUrl = _apiService.GetWebSocketUrl();

        _client = new WebsocketClient(new Uri(wsUrl))
        {
            ReconnectTimeout = TimeSpan.FromSeconds(30),
            ErrorReconnectTimeout = TimeSpan.FromSeconds(5)
        };

        _client.ReconnectionHappened.Subscribe(info =>
        {
            Console.WriteLine($"Reconnection happened, type: {info.Type}");
            _isConnected = true;
            Connected?.Invoke();
        });

        _client.DisconnectionHappened.Subscribe(info =>
        {
            Console.WriteLine($"Disconnection happened, type: {info.Type}");
            _isConnected = false;
            Disconnected?.Invoke();
        });

        _client.MessageReceived.Subscribe(msg =>
        {
            Console.WriteLine($"Message received: {msg}");
        });

        await _client.Start();
    }

    /// <summary>
    /// Reconnects the WebSocket with a new token after token refresh
    /// </summary>
    public async Task ReconnectAsync()
    {
        Console.WriteLine("Reconnecting WebSocket with new token...");
        await ConnectAsync();
    }

    public void SendTrackUpdate(TrackInfo track)
    {
        if (_client == null || !_isConnected) return;

        var message = new WebSocketMessage
        {
            Type = "track",
            Data = track
        };

        var json = JsonSerializer.Serialize(message, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        _client.Send(json);
    }

    public void SendIdle()
    {
        if (_client == null || !_isConnected) return;

        var message = new WebSocketMessage { Type = "idle" };
        var json = JsonSerializer.Serialize(message, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        _client.Send(json);
    }

    public void Disconnect()
    {
        _client?.Stop(System.Net.WebSockets.WebSocketCloseStatus.NormalClosure, "User disconnected");
        _isConnected = false;
    }

    public void Dispose()
    {
        _client?.Dispose();
    }
}
