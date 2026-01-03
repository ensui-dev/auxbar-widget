# Auxbar Desktop Client

A Windows desktop client for [Auxbar](https://auxbar.me) - a real-time music streaming widget that displays your currently playing track on your stream overlay.

![Auxbar Client Screenshot](docs/screenshot.png)

## Overview

Auxbar is a full-stack application that enables streamers to display their currently playing music as a customizable widget on their stream. This repository contains the open-source Windows desktop client that:

- Detects music playing from any Windows media source (Spotify, Apple Music, YouTube Music, etc.)
- Streams real-time track information to the Auxbar backend via WebSocket
- Supports album art, track progress, and play/pause state

## Architecture

```
+-------------------+       WebSocket        +------------------+       +------------------+
|  Desktop Client   | --------------------> |  Auxbar Backend  | <---- |  OBS Browser     |
|  (This Repo)      |   Track Data + Auth   |  (Node.js/AWS)   |       |  Source Widget   |
+-------------------+                        +------------------+       +------------------+
        |                                            |
        v                                            v
+-------------------+                        +------------------+
|  Windows Media    |                        |  SQLite + JWT    |
|  Session API      |                        |  Authentication  |
+-------------------+                        +------------------+
```

### Tech Stack

**Desktop Client (This Repository)**
- **Framework**: .NET 8, Windows Forms
- **Media Detection**: Windows.Media.Control API (GlobalSystemMediaTransportControlsSession)
- **Real-time Communication**: WebSocket (Websocket.Client)
- **Authentication**: JWT with automatic token refresh

**Backend (Closed Source)**
- Node.js/Express API hosted on AWS EC2
- SQLite database with Knex.js
- WebSocket server for real-time updates
- AWS SES for transactional emails
- AWS CDK for infrastructure

**Frontend**
- React + TypeScript + Vite
- Tailwind CSS with retro pixel art theme

## Features

- **Universal Media Detection**: Captures track info from any app using Windows Media Session API
- **Real-time Sync**: WebSocket connection ensures instant updates on your stream widget
- **Album Art Support**: Extracts and transmits album artwork as base64
- **Automatic Reconnection**: Handles network interruptions gracefully
- **Token Auto-Refresh**: JWT tokens refresh automatically before expiration
- **System Tray**: Runs minimized in the system tray while streaming
- **Custom Pixel Art UI**: Retro-styled interface matching the Auxbar web theme

## Building from Source

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Windows 10/11 (required for Windows Media Session API)

### Build

```bash
# Clone the repository
git clone https://github.com/yourusername/auxbar-client.git
cd auxbar-client

# Restore dependencies
dotnet restore

# Build
dotnet build -c Release

# Publish self-contained executable
dotnet publish -c Release
```

The executable will be in `AuxbarClient/bin/Release/net8.0-windows10.0.19041.0/win-x64/publish/`

### Creating an Installer

The project includes an [Inno Setup](https://jrsoftware.org/isinfo.php) script for creating a Windows installer:

1. Install Inno Setup 6
2. Open `installer/setup.iss`
3. Compile (F9)

## Project Structure

```
client/
├── AuxbarClient/
│   ├── Services/
│   │   ├── ApiService.cs        # REST API client with JWT auth
│   │   ├── WebSocketService.cs  # Real-time track streaming
│   │   └── MediaSessionService.cs # Windows media detection
│   ├── Models/
│   │   └── TrackInfo.cs         # Data models
│   ├── Resources/
│   │   ├── app.ico              # Application icon
│   │   ├── primary.png          # Logo assets
│   │   └── PressStart2P-Regular.ttf # Pixel font
│   ├── MainForm.cs              # Main UI
│   ├── Program.cs               # Entry point
│   └── AuxbarClient.csproj      # Project configuration
└── installer/
    └── setup.iss                # Inno Setup installer script
```

## How It Works

### Media Detection

The client uses the Windows `GlobalSystemMediaTransportControlsSessionManager` API to detect currently playing media:

```csharp
_sessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
var session = _sessionManager.GetCurrentSession();
var mediaProperties = await session.TryGetMediaPropertiesAsync();
```

This API captures media from:
- Spotify
- Apple Music
- YouTube Music (in browser)
- Windows Media Player
- Any app that implements Windows media transport controls

### Real-time Updates

Track changes are streamed to the backend via WebSocket:

```csharp
var message = new WebSocketMessage
{
    Type = "track",
    Data = new TrackInfo
    {
        Title = "Song Title",
        Artist = "Artist Name",
        Album = "Album Name",
        AlbumArt = "data:image/png;base64,...",
        Playing = true,
        Progress = 45000,  // ms
        Duration = 180000  // ms
    }
};
_client.Send(JsonSerializer.Serialize(message));
```

### Authentication Flow

1. User logs in with email/password
2. Server returns JWT access token (15min) + refresh token (7 days)
3. Client stores tokens in `%APPDATA%/Auxbar/config.json`
4. Auto-refresh timer renews access token every 12 minutes
5. WebSocket reconnects automatically with new token

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

### Development Setup

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- [Press Start 2P](https://fonts.google.com/specimen/Press+Start+2P) font by CodeMan38
- [Websocket.Client](https://github.com/Marfusios/websocket-client) for robust WebSocket handling

## Related

- [Auxbar Website](https://auxbar.me) - Create your account and customize your widget
- Widget URL: `https://auxbar.me/widget/{your-slug}`

---

Built with love for the streaming community
