# Dungeon Soundboard for Windows

This folder contains a separate Windows desktop client for Dungeon Soundboard. It ports the behavior of the macOS SwiftUI app without trying to compile SwiftUI/AppKit on Windows.

## Requirements

- Windows 10 or newer.
- .NET 10 SDK for building from source.
- Audio files stored locally on disk.

## Build and Test

```powershell
cd windows
dotnet restore .\DungeonSoundboard.Windows.sln
dotnet build .\DungeonSoundboard.Windows.sln -c Release
dotnet test .\DungeonSoundboard.Windows.sln -c Release
```

## Run

```powershell
dotnet run --project .\src\DungeonSoundboard.App\DungeonSoundboard.App.csproj
```

## Publish a Windows Zip

```powershell
cd windows
dotnet publish .\src\DungeonSoundboard.App\DungeonSoundboard.App.csproj -c Release -r win-x64 --self-contained true -o .\publish\win-x64
Compress-Archive -Path .\publish\win-x64\* -DestinationPath .\publish\Dungeon-Soundboard-Windows-win-x64.zip -Force
```

Or use the helper script:

```powershell
.\scripts\publish-windows.ps1
```

## Data Location

The Windows app stores JSON data in:

```text
%APPDATA%\DungeonSoundboard\
  playlists.json
  preferences.json
  hotkeys.json
  theme.json
```

The app stores file paths. It does not use the Windows Registry and does not import macOS security-scoped bookmarks.

## Supported Audio Extensions

The importer accepts:

```text
mp3, wav, aiff, aif, m4a, aac, caf, mp4
```

Actual playback support depends on NAudio and codecs available on the Windows machine. If playback fails, the track stays in the playlist and the app shows an error.

## MVP Feature Coverage

Implemented:

- Music and SFX playlists.
- Music playback with next, previous, shuffle, repeat, stop, pause/resume.
- Concurrent SFX playback over music.
- Ducking while one or more SFX are active.
- Per-track and per-effect volume multipliers from 0% to 200%.
- File/folder import and drag-and-drop import.
- App-local hotkeys while the window is focused.
- Custom hotkey binding for selected music/effect tracks.
- Theme presets and background image path persistence.
- JSON persistence across restarts.

Not included in the MVP:

- Global system-wide hotkeys.
- Streaming providers.
- Installer/MSIX.
- Full visual parity with the macOS UI.
- Import of macOS security-scoped bookmark access.
