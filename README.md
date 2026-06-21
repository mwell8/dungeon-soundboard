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

## Free Release and Contributions

Dungeon Soundboard is free to use. You can download it, run it for your own games, share it with other tabletop players, and improve the project under the MIT license.

Contributions are welcome: bug reports, feature ideas, translations, documentation fixes, and pull requests all help make the app better.

If you enjoy the app and want to support future experiments, you can send a voluntary donation:

- [Support MWell on Boosty](https://boosty.to/mwell/donate)

## License

MIT — see [LICENSE](./LICENSE).

---

## Описание на русском

**Dungeon Soundboard** — macOS-приложение для ведущих настольных игр. Оно помогает держать фоновую музыку включенной и быстро запускать звуковые эффекты поверх нее, не сбивая текущий музыкальный трек.

### Возможности

- Отдельные плейлисты для **музыки** и **SFX**.
- Одновременное воспроизведение фоновой музыки и эффектов.
- Настраиваемое приглушение музыки во время SFX.
- Drag-and-drop импорт аудиофайлов из Finder.
- Создание, переименование, удаление и сортировка плейлистов мышкой.
- Мультивыбор треков через `Cmd+Click`.
- Настраиваемые хоткеи для музыки, эффектов, громкости и отдельных треков.
- `Delete` по умолчанию останавливает только SFX.
- `Space` ставит музыку на паузу или снимает с паузы.
- `+` / `-` регулируют громкость музыки.
- `Shift +` / `Shift -` регулируют громкость SFX.
- Темы, готовые пресеты, собственные визуальные пресеты и фоновые изображения.
- Переключение языка интерфейса: английский / русский.
- Сохранение плейлистов, настроек, темы и хоткеев через `UserDefaults`.

### Скачать и запустить

1. Откройте страницу [Releases](https://github.com/mwell8/dungeon-soundboard/releases).
2. Скачайте `Dungeon-Soundboard-1.0.0-macOS.zip`.
3. Распакуйте архив.
4. Перетащите `Dungeon Soundboard.app` в `Applications` или запустите из распакованной папки.

Сборка не notarized, поэтому macOS может заблокировать первый запуск. Это нормально для неподписанного публичным Developer ID приложения.

Первый запуск:

1. Нажмите правой кнопкой по `Dungeon Soundboard.app`.
2. Выберите `Open`.
3. Подтвердите запуск кнопкой `Open`.

После первого разрешения приложение обычно запускается обычным двойным кликом.

### Сборка из исходников

Требования:

- macOS `26.2+`.
- Xcode `26.3+` или совместимая версия.

В Xcode:

1. Откройте `Dungeon Soundboard.xcodeproj`.
2. Выберите схему `Dungeon_Soundboard`.
3. Выберите destination `My Mac`.
4. Нажмите `Cmd+B` для сборки или `Cmd+R` для запуска.

Через CLI:

```bash
xcodebuild -project "Dungeon Soundboard.xcodeproj" \
  -scheme "Dungeon_Soundboard" \
  -configuration Debug \
  -sdk macosx \
  CODE_SIGNING_ALLOWED=NO CODE_SIGNING_REQUIRED=NO build
```

### Ограничения

- Приложение работает с локальными аудиофайлами, стриминговые сервисы не поддерживаются.
- Доступ к файлам хранится через security-scoped bookmarks macOS. Если доступ к файлам был отозван или файлы перемещены, их нужно добавить заново.
- Хоткеи работают только когда активно окно приложения. Это не глобальные системные хоткеи macOS.

### Бесплатный релиз и вклад в проект

Dungeon Soundboard можно свободно скачивать, использовать для своих игр, отправлять другим ведущим и улучшать под лицензией MIT.

Если вы хотите помочь проекту, можно создавать issue с багами и идеями, предлагать улучшения интерфейса, править документацию, добавлять переводы или отправлять pull request.

Если приложение оказалось полезным и вы хотите поддержать будущие эксперименты, можно отправить добровольный донат:

- [Поддержать MWell на Boosty](https://boosty.to/mwell/donate)
