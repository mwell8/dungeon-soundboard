# Changelog

All notable changes to this project will be documented in this file.

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
