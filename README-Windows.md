# Dungeon Soundboard for Windows

This folder contains a separate Windows desktop client for Dungeon Soundboard. It ports the behavior of the macOS SwiftUI app without trying to compile SwiftUI/AppKit on Windows.

## Requirements

- Windows 10 22H2 or Windows 11, x64.
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
cd windows
dotnet run --project .\src\DungeonSoundboard.App\DungeonSoundboard.App.csproj
```

## Publish a Windows Zip

Official GitHub releases are built from Windows-specific tags by
`.github/workflows/windows-release.yml`. A tag like `windows-vX.Y.Z` publishes
the Windows asset `Dungeon-Soundboard-X.Y.Z-Windows-win-x64.zip` without
triggering the separate macOS release workflow.
PR/debug builds upload a `Dungeon-Soundboard-Windows-ci` artifact for smoke testing.

For a local Windows zip with the same layout as CI/release artifacts:

```powershell
cd windows
.\scripts\publish-windows.ps1
```

The helper script runs restore and tests before publishing, copies
`README-Windows.md`, `README.md`, and `LICENSE` into the package root, and writes
the zip under `windows\publish\`.

For a raw publish folder without packaging docs:

```powershell
dotnet publish .\src\DungeonSoundboard.App\DungeonSoundboard.App.csproj -c Release -r win-x64 --self-contained true -o .\publish\win-x64
```

If local PowerShell policy blocks script execution, run the same helper through
an explicit bypass for this process:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-windows.ps1
```

If tests have already passed in the current job, use `-SkipTests` to only publish
and package the zip. Use `-SkipRestore` only when the selected runtime was
already restored.

To create a local zip with the same naming convention as a tagged GitHub
Release and matching Windows assembly metadata, pass the release version:

```powershell
.\scripts\publish-windows.ps1 -Version X.Y.Z
```

## Manual Smoke Test

Before shipping a Windows zip, verify this flow on Windows:

- Extract the zip and launch `DungeonSoundboard.Windows.exe`.
- Add music files and play, pause/resume, previous, next, shuffle, and repeat.
- Enable fade on pause in Settings and confirm pausing fades music before resume.
- Add SFX files and confirm they play over music without interrupting it.
- While SFX is active, confirm music ducking applies and restores after SFX stops.
- Import files by drag-and-drop into music and SFX decks.
- Rename, reorder, and delete playlists and tracks.
- Confirm the main deck stays compact: search is implemented in the ViewModel
  but intentionally hidden from the macOS-close main layout for now.
- Bind local hotkeys for playback and selected tracks while the app is focused;
  confirm assigned track hotkeys appear in Settings and as badges on track tiles.
- Confirm Settings -> System language switches the UI at runtime. On first
  launch, Russian Windows culture starts in Russian; other cultures start in
  English.
- Adjust Settings -> Interface controls: theme preset, density, 2/3/4 deck
  columns, alpha-aware palette colors, background image, and theme reset.
- Use Settings -> Hotkeys -> Restore Defaults and confirm system bindings return
  while per-track bindings are cleared.
- Use Settings -> System -> Export Profile and Restore Profile with a temporary
  zip; confirm playlists, preferences, hotkeys, and theme restore correctly and a
  backup folder is created before replacement.
- Use Settings -> System -> Remove Missing Music / Remove Missing SFX on a test
  playlist with one missing file; confirm only missing entries are removed and
  normal audio files stay in place.
- Restart the app and confirm playlists, settings, hotkeys, theme, and background
  image path persist.
- Try a playlist item whose source file is missing and confirm the app keeps it in
  the playlist and shows a clear playback/open-location error.

## Demo Screenshot State

Normal users do not receive demo data. For screenshots and local UI checks only,
run:

```powershell
cd windows
.\scripts\seed-demo-state.ps1 -Force
```

The script writes demo JSON and silent local WAV files under
`%APPDATA%\DungeonSoundboard`, backs up existing JSON first, and only overwrites
when `-Force` is passed.

## Interface and Hotkeys

The Windows UI is built with Avalonia and uses a Windows-friendly layout rather
than native SwiftUI/AppKit views. The main window keeps the same operating model
as the macOS app: music controls stay in the upper workspace and SFX controls
stay in the lower workspace.

Settings include:

- Interface density for compact, comfortable, or spacious layouts.
- Runtime interface language preference for English and Russian on the main
  workflow and Settings surfaces. The first launch follows Windows UI culture:
  `ru-*` starts in Russian, and other cultures start in English.
- Theme presets and alpha-aware Avalonia color pickers for background, panel,
  card, accent, text, and danger colors.
- Custom theme presets that can save, reapply, and delete the current visual
  setup.
- Optional background image without an emulated macOS material/blur control.
- Theme reset that keeps the selected background, and restore defaults that also
  clears the background image.
- App-local hotkeys for playback, volume, stop actions, and individual tracks or
  effects.
- A restore-defaults action for system hotkeys.
- Profile export/restore for playlists, preferences, hotkeys, and theme. Audio
  files are not embedded in profile zips.
- Missing-file cleanup for the selected music/SFX playlists.

Hotkeys are local to the app window. They are intentionally not global
system-wide shortcuts in the MVP. Supported local bindings include letters,
number-row keys, numpad digits, F1-F12, Space, Enter, Delete, arrow keys, plus,
and minus, with plain, Shift, or Ctrl modifiers. Alt, Win, Tab, Shift+Ctrl and
Escape are reserved and cannot be assigned; Escape cancels key capture.

## Data Location

The Windows app stores JSON data in:

```text
%APPDATA%\DungeonSoundboard\
  playlists.json
  preferences.json
  hotkeys.json
  theme.json
  Backups\
  Recovery\
  Logs\errors.log
```

The app stores file paths. It does not use the Windows Registry and does not import macOS security-scoped bookmarks.

Profile export creates a zip containing `manifest.json` plus the four JSON state
files. Profile restore validates the zip, creates a timestamped backup in
`Backups\`, replaces the JSON files, and reloads the app state immediately.

## Supported Audio Extensions

The importer accepts:

```text
mp3, wav, aiff, aif, m4a, aac, caf, mp4
```

Actual playback support depends on NAudio and codecs available on the Windows
machine. If playback fails, the track stays in the playlist. Missing files,
unsupported codecs/decode failures, and audio output device failures are reported
with separate user-facing errors.

## MVP Feature Coverage

Implemented:

- Music and SFX playlists.
- Music playback with next, previous, shuffle, repeat, stop, pause/resume, and optional fade on pause.
- Concurrent SFX playback over music.
- Ducking while one or more SFX are active.
- Per-track and per-effect volume multipliers from 0% to 200%.
- Async file/folder import and drag-and-drop import.
- Ordered multi-track drag-and-drop inside and across playlists; cross-playlist
  drag moves by default and `Alt+Drag` copies with new IDs and no copied hotkeys.
- Import summaries for added, duplicate, unsupported, and unavailable/skipped
  paths.
- App-local hotkeys while the window is focused.
- F1-F12 local hotkey bindings for fast soundboard-style track/effect triggers.
- Custom hotkey binding for selected music/effect tracks.
- Assigned hotkey badges on track tiles and assigned-hotkey management in Settings.
- English/Russian interface language preference stored in `preferences.json`.
- Playlist and track rename/delete/reorder flows.
- Per-deck search logic for music and SFX tracks by title or file path. The
  search controls are hidden from the main screen in the current macOS-close
  layout pass.
- Profile export/restore with pre-restore backups.
- Missing-file cleanup commands for selected music/SFX playlists.
- Theme presets, alpha-aware palette editing, custom theme presets, background
  image path persistence, density, real 2/3/4 columns, reset, and
  restore-default controls.
- Windows app icon, product metadata, and DPI-aware application manifest.
- JSON persistence across restarts.
- Missing audio files remain in playlists and show playback/open-location errors.

Not included in the MVP:

- Global system-wide hotkeys.
- Streaming providers.
- Installer/MSIX.
- Code signing, auto-update, and output-device selection.
- Pixel-perfect native macOS visual parity.
- Import of macOS security-scoped bookmark access.
