# Changelog

All notable changes to this project will be documented in this file.

## [1.2.0]

### Windows - 2026-07-17

#### Added
- Native Windows 10/11 x64 client built with Avalonia, .NET 10, and NAudio.
- Separate music and SFX playlists, concurrent effects, ducking, shuffle history, repeat modes, and per-track volume.
- File/folder import, multi-selection drag-and-drop, local hotkeys, themes, profile backup/restore, missing-file cleanup, and local error logs.
- Self-contained portable Windows zip and Windows-specific CI/release workflows.

#### Changed
- Windows navigation, ducking semantics, popup contrast, splitter behavior, and compact player layout now match the tested desktop contract.
- Windows release tags use `windows-vX.Y.Z` so macOS and Windows can be released independently with matching product versions.

### macOS - 2026-07-10

#### Added
- Single- and multi-track drag-and-drop between same-role playlists, including Option-copy and direct Finder file/folder drops onto a playlist.
- Shuffle deck without repeats and playlist-aware playback history.
- Injectable audio, file-access, scheduler, defaults, and telemetry seams with app-level regression tests.
- App-level XCTest target wired into the shared Xcode scheme.
- One-command project verification for SwiftPM, Xcode, strict concurrency, localization, and release packaging.
- Universal Release validation for Apple Silicon and Intel architectures.

#### Changed
- Replaced the application icon with a simplified D20 speaker mark designed to remain recognizable at small sizes.
- Music and SFX lifecycles are independent; replacing a player is atomic and failed playback keeps the active player intact.
- Playback state, playlist migration, custom themes, and physical-key hotkeys now round-trip and reconcile consistently.
- New file references use persistent bookmarks; legacy security-scoped bookmarks remain readable.
- The app now owns one player model, one hotkey store, one local monitor, and one main window.
- Minimum supported system is now macOS 13.0.
- The app target now builds in Swift 6 language mode.
- CI uses macOS 15 with an explicitly selected Xcode 26.3 toolchain.
- Release artifact names and versions are derived from Xcode build settings.
- GitHub release documentation now points to the latest universal build instead of a hard-coded version.

#### Removed
- App Sandbox entitlement configuration and the obsolete entitlement plist resource for the unsandboxed Developer ID distribution.

## [1.1.0] - 2026-06-26

### Added
- Per-track and per-effect volume controls from `0%` to `200%`.
- Track and effect reorder handles for sorting items inside playlists.
- In-app track and effect renaming without touching the original audio files.
- Track/effect action menus with rename, volume, and hotkey controls.
- Additional tests for track volume, title normalization, and item reordering.

### Changed
- SFX cards no longer show the extra inline play icon; sounds still start by clicking the card.
- README now has fuller Russian documentation and updated `1.1.0` download instructions.
- App version is now `1.1.0`.

## [1.0.0] - 2026-06-22

### Added
- Separate music and SFX playlist systems with independent selection.
- Two-zone main layout (music + effects) with compact grid tiles.
- Runtime language switching (English/Russian) in Settings.
- SFX playback over active music with ducking support.
- Sidebar playlist section resize and unified playlist editor.
- Playlist and preferences persistence via `UserDefaults`.
- Custom app-only hotkeys for playback, SFX stop, volume control, tracks, and effects.
- Hotkey settings tab with reassignment, conflict replacement, clearing, and default restore.
- Visual settings with built-in presets, custom theme presets, background images, colors, and interface controls.
- Playlist rename/delete menus and drag-to-reorder playlist sorting.

### Improved
- Migration from legacy mixed playlists to separate music/SFX structures.
- Better stability around volume/preferences normalization and imports.
- Playback context now allows music hotkeys to start tracks from other playlists without changing the selected UI playlist.
