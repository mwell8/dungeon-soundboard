# Dungeon Soundboard

Desktop audio app for tabletop sessions: play background music continuously and trigger SFX on top without interrupting the main track.

The repository contains two desktop clients with the same core soundboard behavior:

- **macOS 13+** — SwiftUI/AppKit, universal Apple Silicon and Intel build. Release tags use `vX.Y.Z`.
- **Windows 10/11 x64** — Avalonia/.NET/NAudio self-contained build. Release tags use `windows-vX.Y.Z`; build and usage details are in [README-Windows.md](./README-Windows.md).

Both clients are distributed as unsigned archives and use app-local hotkeys.

## macOS

## Features

- Separate **Music Playlists** and **SFX Playlists**.
- SFX playback over active music with configurable ducking.
- Compact two-zone UI (Music top, SFX bottom).
- Runtime UI language switch (**English / Russian**).
- Persistent playlists and player preferences.
- Single- and multi-track drag-and-drop within and between same-role playlists; hold `Option` to copy.
- Direct Finder file/folder drops onto a specific Music/SFX playlist.
- Multi-select track management with `Cmd+Click` and `Delete`.
- Custom app hotkeys for playback, SFX stop, volume control, tracks, and effects.
- Per-track and per-effect volume from `0%` to `200%`.
- Theme presets, custom visual themes, and background image support.
- Playlist rename/delete menus and drag-to-reorder playlist sorting.
- Track/effect rename menus and drag-to-reorder sorting inside playlists.

## Requirements

- macOS `13.0+`.
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

## Unit Tests

```bash
swift test --disable-sandbox --scratch-path .build --cache-path .swiftpm-cache
```

## Full Project Check

Run the same SwiftPM, Xcode, strict-concurrency, localization, and universal Release checks used by CI:

```bash
scripts/check_project.sh
```

## Release (Unsigned `.app`, no paid Apple Developer)

Build a distributable unsigned app:

```bash
xcodebuild -project "Dungeon Soundboard.xcodeproj" \
  -scheme "Dungeon_Soundboard" \
  -configuration Release \
  -sdk macosx \
  -derivedDataPath ".DerivedData" \
  CODE_SIGNING_ALLOWED=NO CODE_SIGNING_REQUIRED=NO \
  ARCHS="arm64 x86_64" ONLY_ACTIVE_ARCH=NO build

mkdir -p Release
cp -R ".DerivedData/Build/Products/Release/Dungeon Soundboard.app" Release/
```

Optional zip for GitHub Releases:

```bash
ditto -c -k --sequesterRsrc --keepParent \
  "Release/Dungeon Soundboard.app" \
  "Release/Dungeon-Soundboard-<version>-macOS-universal-unsigned.zip"
```

## Download The Latest Release

Open the [GitHub Releases page](https://github.com/mwell8/dungeon-soundboard/releases), choose the latest release whose tag is `vX.Y.Z` (not `windows-vX.Y.Z`), download its macOS universal zip, unpack it, and move `Dungeon Soundboard.app` to `Applications` if desired.

## First Launch On User Mac (Unsigned App)

Because the app is unsigned, macOS Gatekeeper will block direct open by default.
Users should do one of the following:

1. Right-click app -> `Open` -> `Open` (recommended).
2. Or `System Settings` -> `Privacy & Security` -> `Open Anyway` after first blocked launch.

## Known Limitations

- Audio file locations are stored as persistent macOS bookmarks with a saved-path fallback. Legacy security-scoped bookmarks remain supported.
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

Релизная сборка универсальная: она нативно работает на Mac с Apple Silicon и Intel.

Приложение сделано как отдельный пульт звука для мастера: без аккаунтов, браузера, сервера и настройки виртуального стола. Оно подходит для офлайн-сессий по D&D и другим НРИ, когда нужно быстро включать атмосферу, бой, город, таверну, тревогу, окружение или короткие эффекты.

### Возможности

- Отдельные плейлисты для **музыки** и **SFX**.
- Одновременное воспроизведение фоновой музыки и эффектов.
- Настраиваемое приглушение музыки во время SFX.
- Перенос одиночных и выбранных групп треков/SFX внутри и между плейлистами; `Option` копирует элементы.
- Прямой импорт файлов и папок Finder в конкретный плейлист.
- Создание, переименование, удаление и сортировка плейлистов мышкой.
- Сортировка треков и эффектов внутри плейлиста через отдельную drag-иконку.
- Переименование треков и эффектов внутри приложения без переименования файла на диске.
- Индивидуальная громкость каждого трека и эффекта от `0%` до `200%`.
- Мультивыбор треков через `Cmd+Click`.
- Настраиваемые хоткеи для музыки, эффектов, громкости и отдельных треков.
- `Delete` по умолчанию останавливает только SFX.
- `Space` ставит музыку на паузу или снимает с паузы.
- `+` / `-` регулируют громкость музыки.
- `Shift +` / `Shift -` регулируют громкость SFX.
- Темы, готовые пресеты, собственные визуальные пресеты и фоновые изображения.
- Переключение языка интерфейса: английский / русский.
- Сохранение плейлистов, настроек, темы и хоткеев через `UserDefaults`.

### Что сохраняется

Плейлисты, названия треков внутри приложения, порядок элементов, индивидуальная громкость, темы, пресеты, язык и хоткеи сохраняются локально через `UserDefaults`. Замена приложения на новую версию не должна удалять эти данные.

Аудиофайлы остаются там, где вы их храните. Dungeon Soundboard не копирует музыку в свой проект и не переименовывает исходные файлы.

### Скачать и запустить

1. Откройте [страницу Releases](https://github.com/mwell8/dungeon-soundboard/releases).
2. Выберите последний релиз с тегом `vX.Y.Z` (не `windows-vX.Y.Z`) и скачайте приложенный macOS universal zip.
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

- macOS `13.0+`.
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

Полная проверка проекта тем же набором команд, который используется в CI:

```bash
scripts/check_project.sh
```

### Ограничения

- Приложение работает с локальными аудиофайлами, стриминговые сервисы не поддерживаются.
- Пути к файлам хранятся в persistent bookmarks macOS с резервным сохранённым путём; старые security-scoped bookmarks также поддерживаются.
- Хоткеи работают только когда активно окно приложения. Это не глобальные системные хоткеи macOS.

### Бесплатный релиз и вклад в проект

Dungeon Soundboard можно свободно скачивать, использовать для своих игр, отправлять другим ведущим и улучшать под лицензией MIT.

Если вы хотите помочь проекту, можно создавать issue с багами и идеями, предлагать улучшения интерфейса, править документацию, добавлять переводы или отправлять pull request.

Если приложение оказалось полезным и вы хотите поддержать будущие эксперименты, можно отправить добровольный донат:

- [Поддержать MWell на Boosty](https://boosty.to/mwell/donate)
