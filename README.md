# Dungeon Soundboard (macOS)

Desktop audio app for tabletop sessions: play background music continuously and trigger SFX on top without interrupting the main track.

## Features

- Separate **Music Playlists** and **SFX Playlists**.
- SFX playback over active music with configurable ducking.
- Compact two-zone UI (Music top, SFX bottom).
- Runtime UI language switch (**English / Russian**).
- Persistent playlists and player preferences.
- Drag-and-drop import from Finder to Music/SFX zones.
- Multi-select track management with `Cmd+Click` and `Delete`.
- Custom app hotkeys for playback, SFX stop, volume control, tracks, and effects.
- Theme presets, custom visual themes, and background image support.
- Playlist rename/delete menus and drag-to-reorder playlist sorting.

## Requirements

- macOS `26.2+` (as configured in the project).
- Xcode `26.3+` (or compatible).

## Run (Xcode)

1. Open `Dungeon Soundboard.xcodeproj`.
2. Select scheme `Dungeon_Soundboard`.
3. Build and run on **My Mac**.

## Run (CLI)

```bash
xcodebuild -project "Dungeon Soundboard.xcodeproj" \
  -scheme "Dungeon_Soundboard" \
  -configuration Debug \
  -sdk macosx \
  CODE_SIGNING_ALLOWED=NO CODE_SIGNING_REQUIRED=NO build
```

## Unit Tests (Core Logic)

```bash
swift test --disable-sandbox --scratch-path .build --cache-path .swiftpm-cache
```

## Release (Unsigned `.app`, no paid Apple Developer)

Build a distributable unsigned app:

```bash
xcodebuild -project "Dungeon Soundboard.xcodeproj" \
  -scheme "Dungeon_Soundboard" \
  -configuration Release \
  -sdk macosx \
  -derivedDataPath ".DerivedData" \
  CODE_SIGNING_ALLOWED=NO CODE_SIGNING_REQUIRED=NO build

mkdir -p Release
cp -R ".DerivedData/Build/Products/Release/Dungeon Soundboard.app" Release/
```

Optional zip for GitHub Releases:

```bash
ditto -c -k --sequesterRsrc --keepParent \
  "Release/Dungeon Soundboard.app" \
  "Release/Dungeon-Soundboard-1.0.0-macOS.zip"
```

## First Launch On User Mac (Unsigned App)

Because the app is unsigned, macOS Gatekeeper will block direct open by default.
Users should do one of the following:

1. Right-click app -> `Open` -> `Open` (recommended).
2. Or `System Settings` -> `Privacy & Security` -> `Open Anyway` after first blocked launch.

## Known Limitations

- Audio files are accessed via macOS security-scoped bookmarks. If access is revoked, re-add files/folders.
- The app is optimized for local file playback (no streaming providers).

## License

MIT — see [LICENSE](./LICENSE).

---

## Кратко по-русски

Приложение для настольных игр: музыка играет непрерывно, а эффекты (SFX) можно запускать поверх неё без переключения трека.  
Поддерживаются отдельные плейлисты музыки и эффектов, приглушение, и переключение языка интерфейса (EN/RU).
