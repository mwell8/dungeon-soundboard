# DnD Music Player (macOS)

Desktop audio app for tabletop sessions: play background music continuously and trigger SFX on top without interrupting the main track.

## Features

- Separate **Music Playlists** and **SFX Playlists**.
- SFX playback over active music with configurable ducking.
- Compact two-zone UI (Music top, SFX bottom).
- Runtime UI language switch (**English / Russian**).
- Persistent playlists and player preferences.

## Requirements

- macOS `26.2+` (as configured in the project).
- Xcode `26.3+` (or compatible).

## Run (Xcode)

1. Open `DnD Music Player.xcodeproj`.
2. Select scheme `DnD_Music_Player`.
3. Build and run on **My Mac**.

## Run (CLI)

```bash
xcodebuild -project "DnD Music Player.xcodeproj" \
  -scheme "DnD_Music_Player" \
  -configuration Debug \
  -sdk macosx \
  CODE_SIGNING_ALLOWED=NO CODE_SIGNING_REQUIRED=NO build
```

## Release (Unsigned `.app`, no paid Apple Developer)

Build a distributable unsigned app:

```bash
xcodebuild -project "DnD Music Player.xcodeproj" \
  -scheme "DnD_Music_Player" \
  -configuration Release \
  -sdk macosx \
  -derivedDataPath ".DerivedData" \
  CODE_SIGNING_ALLOWED=NO CODE_SIGNING_REQUIRED=NO build

mkdir -p Release
cp -R ".DerivedData/Build/Products/Release/DnD_Music_Player.app" Release/
```

Optional zip for GitHub Releases:

```bash
ditto -c -k --sequesterRsrc --keepParent \
  "Release/DnD_Music_Player.app" \
  "Release/DnD_Music_Player-unsigned-macOS.zip"
```

## First Launch On User Mac (Unsigned App)

Because the app is unsigned, macOS Gatekeeper will block direct open by default.
Users should do one of the following:

1. Right-click app -> `Open` -> `Open` (recommended).
2. Or `System Settings` -> `Privacy & Security` -> `Open Anyway` after first blocked launch.

## Optional: Signed + Notarized Release (Paid Apple Developer)

1. Install a valid **Developer ID Application** certificate into your login keychain.
2. Configure one-time notarization credentials:

```bash
xcrun notarytool store-credentials "AC_NOTARY" \
  --apple-id "YOUR_APPLE_ID" \
  --team-id "YOUR_TEAM_ID" \
  --password "YOUR_APP_SPECIFIC_PASSWORD"
```

3. Run release pipeline from project root:

```bash
DEVELOPER_ID_APPLICATION="Developer ID Application: Your Name (TEAMID)" \
NOTARY_PROFILE="AC_NOTARY" \
./scripts/release-macos.sh
```

Output artifacts will be placed in `dist/`, including notarized zip:
`dist/DnD_Music_Player-macOS-notarized.zip`.

## Screenshots

- Add screenshots here before publishing releases.
- Suggested: main screen, settings, language switch.

## Known Limitations

- Audio files are accessed via macOS security-scoped bookmarks. If access is revoked, re-add files/folders.
- The app is optimized for local file playback (no streaming providers).

## License

MIT — see [LICENSE](./LICENSE).

---

## Кратко по-русски

Приложение для DnD/настольных игр: музыка играет непрерывно, а эффекты (SFX) можно запускать поверх неё без переключения трека.  
Поддерживаются отдельные плейлисты музыки и эффектов, ducking, и переключение языка интерфейса (EN/RU).
