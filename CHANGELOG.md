# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.1.0] - 2026-01-04

### Added
- Discord Rich Presence integration
- Dynamic album art display as Discord large image
- Playing/paused status icons for Discord
- Playback progress timestamps in Discord status
- "Get Auxbar" button on Discord presence
- Discord toggle in connected panel

### Changed
- Version now read from version.json at build time
- Improved Discord connection status indicators

## [1.0.0] - 2025-01-03

### Added
- Initial release
- Windows Media Session API integration for universal music detection
- Real-time WebSocket streaming to Auxbar backend
- Album art extraction and transmission
- JWT authentication with automatic token refresh
- System tray support for background operation
- Retro pixel art UI theme
- Inno Setup installer script

### Supported Media Sources
- Spotify
- Apple Music
- YouTube Music (browser)
- Windows Media Player
- Any app using Windows media transport controls
