using DungeonSoundboard.Core.Models;

namespace DungeonSoundboard.App.ViewModels;

public sealed record AppLanguageOption(AppLanguage Language, string Name);

public sealed record InterfaceDensityOption(InterfaceDensity Value, string Name);

public sealed record RepeatModeOption(RepeatMode Value, string Name);

public sealed record BackgroundLayoutModeOption(BackgroundLayoutMode Value, string Name);

public sealed class AppUiStrings
{
    private AppUiStrings(AppLanguage language)
    {
        CurrentLanguage = language;
    }

    public AppLanguage CurrentLanguage { get; }

    public static AppUiStrings For(AppLanguage language) => new(language);

    private bool Ru => CurrentLanguage == AppLanguage.Russian;

    public string AppTitle => "Dungeon Soundboard";
    public string Settings => Ru ? "Настройки" : "Settings";
    public string Theme => Ru ? "Тема" : "Theme";
    public string ChooseBackgroundImage => Ru ? "Выбрать фоновое изображение" : "Choose background image";
    public string MusicPlaylists => Ru ? "Музыкальные плейлисты" : "Music Playlists";
    public string MusicPlaylistsHint => Ru ? "Выберите плейлист для музыкальной деки" : "Choose the playlist used by the music deck";
    public string SfxPlaylists => Ru ? "SFX-плейлисты" : "SFX Playlists";
    public string SfxPlaylistsHint => Ru ? "Одновременные эффекты и короткие акценты" : "Concurrent effects and stingers";
    public string CreateMusicPlaylist => Ru ? "Создать музыкальный плейлист" : "Create music playlist";
    public string CreateSfxPlaylist => Ru ? "Создать SFX-плейлист" : "Create SFX playlist";
    public string DeleteSelectedMusicPlaylist => Ru ? "Удалить выбранный музыкальный плейлист" : "Delete selected music playlist";
    public string DeleteSelectedSfxPlaylist => Ru ? "Удалить выбранный SFX-плейлист" : "Delete selected SFX playlist";
    public string MoveSelectedMusicPlaylistUp => Ru ? "Переместить выбранный музыкальный плейлист выше" : "Move selected music playlist up";
    public string MoveSelectedMusicPlaylistDown => Ru ? "Переместить выбранный музыкальный плейлист ниже" : "Move selected music playlist down";
    public string MoveSelectedSfxPlaylistUp => Ru ? "Переместить выбранный SFX-плейлист выше" : "Move selected SFX playlist up";
    public string MoveSelectedSfxPlaylistDown => Ru ? "Переместить выбранный SFX-плейлист ниже" : "Move selected SFX playlist down";
    public string ShufflePlayThisPlaylist => Ru ? "Запустить плейлист в случайном порядке" : "Shuffle play this playlist";
    public string MovePlaylistUp => Ru ? "Переместить плейлист выше" : "Move playlist up";
    public string MovePlaylistDown => Ru ? "Переместить плейлист ниже" : "Move playlist down";
    public string DeletePlaylist => Ru ? "Удалить плейлист" : "Delete playlist";
    public string ResizePlaylistSections => Ru ? "Изменить высоту секций плейлистов" : "Resize playlist sections";
    public string MusicPlaylistName => Ru ? "Название музыкального плейлиста" : "Music playlist name";
    public string SfxPlaylistName => Ru ? "Название SFX-плейлиста" : "SFX playlist name";
    public string MusicDeck => Ru ? "Музыкальная дека" : "Music deck";
    public string SfxDeck => Ru ? "SFX-дека" : "SFX deck";
    public string AddFiles => Ru ? "Добавить файлы" : "Add Files";
    public string AddFolder => Ru ? "Добавить папку" : "Add Folder";
    public string DeleteSelected => Ru ? "Удалить выбранные" : "Delete Selected";
    public string Play => Ru ? "Играть" : "Play";
    public string AddMusicFiles => Ru ? "Добавить музыкальные файлы" : "Add music files";
    public string AddMusicFolder => Ru ? "Добавить папку с музыкой" : "Add a music folder";
    public string AddSfxFiles => Ru ? "Добавить SFX-файлы" : "Add SFX files";
    public string AddSfxFolder => Ru ? "Добавить папку с SFX" : "Add an SFX folder";
    public string PlaySelectedMusicTrack => Ru ? "Запустить выбранный музыкальный трек" : "Play selected music track";
    public string DeleteSelectedTrack => Ru ? "Удалить выбранный трек" : "Delete selected track";
    public string DeleteSelectedSfx => Ru ? "Удалить выбранный SFX" : "Delete selected SFX";
    public string NoMusicTracks => Ru ? "Нет музыкальных треков" : "No music tracks";
    public string NoMusicTracksHint => Ru ? "Перетащите аудиофайлы сюда или добавьте их кнопкой" : "Drop audio files here or add files to start the music deck";
    public string NoSfxTracks => Ru ? "Нет SFX-треков" : "No SFX tracks";
    public string NoSfxTracksHint => Ru ? "Перетащите аудиофайлы сюда или добавьте их в SFX-деку" : "Drop audio files here or add files to build the SFX deck";
    public string SearchMusicTracks => Ru ? "Поиск музыки" : "Search music";
    public string SearchSfxTracks => Ru ? "Поиск SFX" : "Search SFX";
    public string ClearSearch => Ru ? "Очистить поиск" : "Clear search";
    public string NoSearchResults => Ru ? "Совпадений не найдено" : "No matches found";
    public string NoSearchResultsHint => Ru ? "Измените запрос или очистите поиск" : "Change the search text or clear the filter";
    public string DropMusicFiles => Ru ? "Отпустите музыкальные файлы" : "Drop music files";
    public string DropMusicFilesHint => Ru ? "Файлы будут добавлены в выбранный музыкальный плейлист" : "Files will be added to the selected music playlist";
    public string DropSfxFiles => Ru ? "Отпустите SFX-файлы" : "Drop SFX files";
    public string DropSfxFilesHint => Ru ? "Файлы будут добавлены в выбранный SFX-плейлист" : "Files will be added to the selected SFX playlist";
    public string NowPlaying => Ru ? "Сейчас играет" : "Now Playing";
    public string NothingIsPlaying => Ru ? "Ничего не играет" : "Nothing is playing";
    public string Playing => Ru ? "Играет" : "Playing";
    public string PausedStopped => Ru ? "Пауза / остановлено" : "Paused / stopped";
    public string Pause => Ru ? "Пауза" : "Pause";
    public string Resume => Ru ? "Продолжить" : "Resume";
    public string PreviousTrack => Ru ? "Предыдущий трек" : "Previous track";
    public string NextTrack => Ru ? "Следующий трек" : "Next track";
    public string Shuffle => Ru ? "Случайно" : "Shuffle";
    public string StopSfx => Ru ? "Стоп SFX" : "Stop SFX";
    public string StopAll => Ru ? "Стоп всё" : "Stop All";
    public string StopAllPlayback => Ru ? "Остановить всё воспроизведение" : "Stop all playback";
    public string StopAllActiveSfx => Ru ? "Остановить все активные SFX" : "Stop all currently playing SFX";

    public string Audio => Ru ? "Звук" : "Audio";
    public string Mixer => Ru ? "Микшер" : "Mixer";
    public string Music => Ru ? "Музыка" : "Music";
    public string Sfx => "SFX";
    public string Ducking => Ru ? "Приглушение" : "Ducking";
    public string Playback => Ru ? "Воспроизведение" : "Playback";
    public string FadeOnPause => Ru ? "Плавная пауза" : "Fade on pause";
    public string Interface => Ru ? "Интерфейс" : "Interface";
    public string Preset => Ru ? "Пресет" : "Preset";
    public string Density => Ru ? "Плотность" : "Density";
    public string Panels => Ru ? "Панели" : "Panels";
    public string Corners => Ru ? "Углы" : "Corners";
    public string Accent => Ru ? "Акцент" : "Accent";
    public string Background => Ru ? "Фон" : "Background";
    public string ChooseImage => Ru ? "Выбрать" : "Choose Image";
    public string Clear => Ru ? "Очистить" : "Clear";
    public string Layout => Ru ? "Режим" : "Layout";
    public string Opacity => Ru ? "Прозрачность" : "Opacity";
    public string Dim => Ru ? "Затемнение" : "Dim";
    public string Blur => Ru ? "Размытие" : "Blur";
    public string Palette => Ru ? "Палитра" : "Palette";
    public string PaletteHint => Ru
        ? "Настройте цвета и прозрачность сохранённой темы. Изменение цвета переводит текущий пресет в свою тему."
        : "Tune the saved theme colors and opacity. Changing a color turns the current preset into a custom theme.";
    public string CustomThemes => Ru ? "Свои темы" : "Custom Themes";
    public string CustomThemeName => Ru ? "Название своей темы" : "Custom theme name";
    public string Save => Ru ? "Сохранить" : "Save";
    public string ApplyCustomTheme => Ru ? "Применить свою тему" : "Apply custom theme";
    public string DeleteCustomTheme => Ru ? "Удалить свою тему" : "Delete custom theme";
    public string Advanced => Ru ? "Дополнительно" : "Advanced";
    public string ResetTheme => Ru ? "Сбросить тему" : "Reset Theme";
    public string ResetThemeTip => Ru ? "Сбросить цвета и настройки, сохранив текущий фон" : "Reset theme colors and controls while keeping the current background";
    public string RestoreDefaults => Ru ? "Вернуть по умолчанию" : "Restore Defaults";
    public string RestoreVisualDefaultsTip => Ru ? "Вернуть стандартную тему и очистить фоновое изображение" : "Restore the default visual theme and clear the background image";
    public string CurrentBackground => Ru ? "Текущий фон" : "Current Background";
    public string DeckLayout => Ru ? "Раскладка дек" : "Deck Layout";
    public string MusicColumns => Ru ? "Колонки музыки" : "Music columns";
    public string SfxColumns => Ru ? "Колонки SFX" : "SFX columns";
    public string Hotkeys => Ru ? "Клавиши" : "Hotkeys";
    public string SystemHotkeys => Ru ? "Системные клавиши" : "System Hotkeys";
    public string SelectedTracks => Ru ? "Выбранные треки" : "Selected Tracks";
    public string AssignedTrackHotkeys => Ru ? "Клавиши треков" : "Assigned Track Hotkeys";
    public string RestoreDefaultHotkeys => Ru ? "Вернуть клавиши по умолчанию" : "Restore Defaults";
    public string RestoreThisHotkeyDefault => Ru ? "Вернуть эту клавишу по умолчанию" : "Restore this hotkey default";
    public string AssignHotkey => Ru ? "Назначить клавишу" : "Assign hotkey";
    public string ClearHotkey => Ru ? "Очистить клавишу" : "Clear hotkey";
    public string CancelHotkeyCapture => Ru ? "Отменить назначение клавиши" : "Cancel hotkey capture";
    public string System => Ru ? "Система" : "System";
    public string LanguageSection => Ru ? "Язык" : "Language";
    public string Language => Ru ? "Язык интерфейса" : "Interface language";
    public string Storage => Ru ? "Локальные данные приложения" : "App Data";
    public string StorageDescription => Ru
        ? "Здесь хранятся плейлисты, настройки, клавиши и тема. Аудиофайлы остаются в исходных папках."
        : "Playlists, settings, hotkeys, and the theme are stored here. Audio files remain in their original folders.";
    public string OpenFolder => Ru ? "Открыть папку" : "Open Folder";
    public string ProfileBackup => Ru ? "Профиль" : "Profile Backup";
    public string ProfileHelp => Ru ? "Что входит в профиль" : "What is included in a profile";
    public string ProfileDescription => Ru
        ? "Профиль сохраняет плейлисты, настройки, горячие клавиши и тему. Аудиофайлы в ZIP не включаются и должны оставаться доступными по сохранённым путям."
        : "A profile saves playlists, settings, hotkeys, and the theme. Audio files are not included in the ZIP and must remain available at their saved paths.";
    public string ExportProfile => Ru ? "Экспорт профиля" : "Export Profile";
    public string RestoreProfile => Ru ? "Восстановить профиль" : "Restore Profile";
    public string ProfileZipTypeName => Ru ? "ZIP-профиль" : "Profile zip";
    public string ExportProfileTitle => Ru ? "Экспорт профиля Dungeon Soundboard" : "Export Dungeon Soundboard profile";
    public string RestoreProfileTitle => Ru ? "Выберите ZIP-профиль Dungeon Soundboard" : "Choose Dungeon Soundboard profile zip";
    public string AudioImport => Ru ? "Импорт аудио" : "Audio Import";
    public string RemoveMissingMusicFiles => Ru ? "Удалить отсутствующие из музыки" : "Remove Missing Music";
    public string RemoveMissingSfxFiles => Ru ? "Удалить отсутствующие из SFX" : "Remove Missing SFX";
    public string CodecNote => Ru ? "Воспроизведение зависит от кодеков NAudio и компонентов Windows." : "Playback uses the codecs available through NAudio and Windows media components.";
    public string Done => Ru ? "Готово" : "Done";
    public string CloseSettings => Ru ? "Закрыть настройки" : "Close settings";
    public string LocalErrorLog => Ru ? "Локальный журнал ошибок" : "Local Error Log";
    public string OpenErrorLogFolder => Ru ? "Открыть папку журнала" : "Open Log Folder";
    public string About => Ru ? "О программе" : "About";
    public string Version => Ru ? "Версия" : "Version";
    public string AuthorCredit => Ru ? "Автор: MWell" : "Made by MWell";
    public string AuthorLabel => Ru ? "Автор:" : "Made by";
    public string OpenAuthorPage => Ru ? "Открыть страницу MWell на Boosty" : "Open MWell on Boosty";

    public string ThemePresetBackground => Ru ? "Фон из пресета темы" : "Theme preset background";
    public string ImageBackground => Ru ? "Фоновое изображение" : "Image background";
    public string NoBackgroundImageSelected => Ru ? "Фоновое изображение не выбрано" : "No background image selected";
    public string SelectTrack => Ru ? "Выберите трек" : "Select track";
    public string Unassigned => Ru ? "Не назначено" : "Unassigned";
    public string HotkeysActiveStatus => Ru ? "Клавиши активны: Space, Delete, +/-, Shift +/-" : "Hotkeys active: Space, Delete, +/-, Shift +/-";
    public string PressKeyToAssign => Ru ? "Нажмите клавишу для назначения или Esc для отмены" : "Press a key to assign it, or Esc to cancel";
    public string NoSavedCustomThemes => Ru ? "Свои темы не сохранены" : "No saved custom themes";
    public string OneSavedCustomTheme => Ru ? "1 сохранённая своя тема" : "1 saved custom theme";
    public string SfxIdle => Ru ? "SFX не играет" : "SFX idle";
    public string OneSfxActive => Ru ? "1 SFX активен" : "1 SFX active";
    public string DuckingActive => Ru ? "Приглушение активно" : "Ducking active";
    public string DuckingOff => Ru ? "Без приглушения" : "Ducking off";

    public string StopAllSounds => Ru ? "Остановить все звуки" : "Stop all playback";
    public string PlayPauseMusic => Ru ? "Пауза / воспроизведение музыки" : "Play / pause music";
    public string MusicVolumeUp => Ru ? "Музыка громче" : "Music volume up";
    public string MusicVolumeDown => Ru ? "Музыка тише" : "Music volume down";
    public string SfxVolumeUp => Ru ? "SFX громче" : "SFX volume up";
    public string SfxVolumeDown => Ru ? "SFX тише" : "SFX volume down";
    public string MissingMusicTrack => Ru ? "удалённый музыкальный трек" : "missing music track";
    public string MissingSfxTrack => Ru ? "удалённый SFX-трек" : "missing SFX track";
    public string UnknownAction => Ru ? "Неизвестное действие" : "Unknown action";

    public string ShufflePlay => Ru ? "Играть случайно" : "Shuffle Play";
    public string Rename => Ru ? "Переименовать" : "Rename";
    public string MoveUp => Ru ? "Переместить выше" : "Move Up";
    public string MoveDown => Ru ? "Переместить ниже" : "Move Down";
    public string Delete => Ru ? "Удалить" : "Delete";
    public string Cancel => Ru ? "Отмена" : "Cancel";
    public string Replace => Ru ? "Заменить" : "Replace";
    public string Ok => Ru ? "OK" : "OK";
    public string PlaylistName => Ru ? "Название плейлиста" : "Playlist name";
    public string TrackName => Ru ? "Название трека" : "Track name";
    public string AssignHotkeyTitle => Ru ? "Назначить клавишу" : "Assign Hotkey";
    public string OpenFileLocation => Ru ? "Показать файл" : "Open File Location";
    public string Volume => Ru ? "Громкость" : "Volume";
    public string VolumeLabel => Ru ? "Громкость" : "Volume";
    public string MuteVolume => Ru ? "Тихо (0%)" : "Mute (0%)";
    public string NormalVolume => Ru ? "Обычно (100%)" : "Normal (100%)";
    public string BoostVolume => Ru ? "Усилить (150%)" : "Boost (150%)";
    public string BindHotkey => Ru ? "Назначить клавишу" : "Bind Hotkey";
    public string PlaySfx => Ru ? "Играть SFX" : "Play SFX";
    public string PlayThisMusicTrack => Ru ? "Играть этот музыкальный трек" : "Play this music track";
    public string AssignLocalHotkeyToThisTrack => Ru ? "Назначить клавишу этому треку" : "Assign a local hotkey to this track";
    public string ClearThisTrackHotkey => Ru ? "Очистить клавишу этого трека" : "Clear this track hotkey";
    public string DeleteThisTrackFromPlaylist => Ru ? "Удалить этот трек из плейлиста" : "Delete this track from the playlist";
    public string PlayThisSfx => Ru ? "Играть этот SFX" : "Play this SFX";
    public string AssignLocalHotkeyToThisSfx => Ru ? "Назначить клавишу этому SFX" : "Assign a local hotkey to this SFX";
    public string ClearThisSfxHotkey => Ru ? "Очистить клавишу этого SFX" : "Clear this SFX hotkey";
    public string DeleteThisSfxFromPlaylist => Ru ? "Удалить этот SFX из плейлиста" : "Delete this SFX from the playlist";
    public string SelectedTrackTitle => Ru ? "Название выбранного трека" : "Selected track title";
    public string SelectedEffectTitle => Ru ? "Название выбранного SFX" : "Selected effect title";
    public string AssignHotkeyToSelectedTrack => Ru ? "Назначить клавишу выбранному треку" : "Assign hotkey to selected track";
    public string ClearSelectedTrackHotkey => Ru ? "Очистить клавишу выбранного трека" : "Clear selected track hotkey";
    public string MoveSelectedTrackUp => Ru ? "Переместить выбранный трек выше" : "Move selected track up";
    public string MoveSelectedTrackDown => Ru ? "Переместить выбранный трек ниже" : "Move selected track down";
    public string AssignHotkeyToSelectedSfx => Ru ? "Назначить клавишу выбранному SFX" : "Assign hotkey to selected SFX";
    public string ClearSelectedSfxHotkey => Ru ? "Очистить клавишу выбранного SFX" : "Clear selected SFX hotkey";
    public string MoveSelectedSfxUp => Ru ? "Переместить выбранный SFX выше" : "Move selected SFX up";
    public string MoveSelectedSfxDown => Ru ? "Переместить выбранный SFX ниже" : "Move selected SFX down";
    public string FileAvailable => Ru ? "Файл доступен" : "File available";
    public string FileMissing => Ru ? "Файл отсутствует" : "File missing";
    public string MissingFile => Ru ? "Нет файла" : "Missing file";
    public string Missing => Ru ? "Нет файла" : "Missing";
    public string NoTrackSelected => Ru ? "Трек не выбран" : "No track selected";
    public string NoMusicTrackSelected => Ru ? "Музыкальный трек не выбран" : "No music track selected";
    public string NoSfxTrackSelected => Ru ? "SFX-трек не выбран" : "No SFX track selected";
    public string NoPlaylistSelected => Ru ? "Плейлист не выбран" : "No playlist selected";
    public string NewMusicPlaylistPrefix => Ru ? "Плейлист" : "Playlist";
    public string NewSfxPlaylistPrefix => "SFX";
    public string DefaultMusicPlaylistName => Ru ? "Основной плейлист" : "Main Playlist";
    public string DefaultSfxPlaylistName => Ru ? "SFX-мастер" : "SFX Master";
    public string MusicImportTarget => Ru ? "музыкальные плейлисты" : "Music Playlists";
    public string SfxImportTarget => Ru ? "SFX-плейлисты" : "SFX Playlists";
    public string NoSupportedAudioFilesFound => Ru ? "Поддерживаемые аудиофайлы не найдены." : "No supported audio files found.";
    public string AudioFilesTypeName => Ru ? "Аудиофайлы" : "Audio files";
    public string ImageFilesTypeName => Ru ? "Изображения" : "Image files";
    public string ChooseAudioFilesTitle => Ru ? "Выберите аудиофайлы" : "Choose audio files";
    public string ChooseAudioFolderTitle => Ru ? "Выберите папку с аудио" : "Choose folder with audio";
    public string ClassicDungeonPreset => Ru ? "Классическое подземелье" : "Classic Dungeon";
    public string TavernEmberPreset => Ru ? "Угли таверны" : "Tavern Ember";
    public string MoonlitCryptPreset => Ru ? "Лунный склеп" : "Moonlit Crypt";
    public string ForestMistPreset => Ru ? "Лесной туман" : "Forest Mist";
    public string PaletteBackgroundTop => Ru ? "Фон сверху" : "Background Top";
    public string PaletteBackgroundBottom => Ru ? "Фон снизу" : "Background Bottom";
    public string PalettePanel => Ru ? "Панель" : "Panel";
    public string PalettePanelAlt => Ru ? "Дополнительная панель" : "Panel Alt";
    public string PaletteCard => Ru ? "Карточка" : "Card";
    public string PaletteCurrentCard => Ru ? "Текущая карточка" : "Current Card";
    public string PaletteAccentName => Ru ? "Акцент" : "Accent";
    public string PaletteTextPrimary => Ru ? "Основной текст" : "Text Primary";
    public string PaletteTextSecondary => Ru ? "Вторичный текст" : "Text Secondary";
    public string PaletteDanger => Ru ? "Опасность" : "Danger";

    public string SavedCustomThemes(int count) => Ru ? $"{count} {PluralRu(count, "сохранённая своя тема", "сохранённые свои темы", "сохранённых своих тем")}" : $"{count} saved custom themes";
    public string TrackCount(int count) => Ru ? $"{count} {PluralRu(count, "трек", "трека", "треков")}" : $"{count} {(count == 1 ? "track" : "tracks")}";
    public string EffectCount(int count) => Ru ? $"{count} SFX" : $"{count} {(count == 1 ? "effect" : "effects")}";
    public string SfxActive(int count) => Ru ? $"{count} SFX активно" : $"{count} SFX active";
    public string AddedFiles(int count) => Ru ? $"Добавлено файлов: {count}." : $"Added {count} file(s).";
    public string AddedFilesSkippedDuplicates(int addedCount, int duplicateCount) => Ru
        ? $"Добавлено: {addedCount}; пропущено дублей: {duplicateCount}."
        : $"Added {addedCount}; skipped duplicates: {duplicateCount}.";
    public string ProfileExported(string path) => Ru ? $"Профиль экспортирован: {path}" : $"Profile exported: {path}";
    public string ProfileRestored(string backupDirectory) => Ru ? $"Профиль восстановлен. Копия прежних данных: {backupDirectory}" : $"Profile restored. Previous data backup: {backupDirectory}";
    public string ProfileTransferFailed(string message) => Ru ? $"Не удалось обработать профиль: {message}" : $"Could not process profile: {message}";
    public string ImportSummary(int addedCount, int duplicateCount, int unsupportedCount, int skippedCount) => Ru
        ? $"Добавлено {addedCount}; дубли: {duplicateCount}; неподдерживаемые: {unsupportedCount}; недоступные: {skippedCount}."
        : $"Added {addedCount}; duplicates: {duplicateCount}; unsupported: {unsupportedCount}; unavailable: {skippedCount}.";
    public string RemovedMissingFiles(int count) => Ru ? $"Удалено отсутствующих файлов: {count}" : $"Removed missing files: {count}";
    public string NoMissingFilesToRemove => Ru ? "Отсутствующие файлы не найдены." : "No missing files to remove.";
    public string SelectionSummary(int count) => count > 1 ? (Ru ? $"{count} выбрано" : $"{count} selected") : "";
    public string DeleteSelectedTracksToolTip(int count) => count > 1
        ? Ru ? $"Удалить выбранные треки: {count}" : $"Delete {count} selected tracks"
        : DeleteSelectedTrack;
    public string DeleteSelectedSfxToolTip(int count) => count > 1
        ? Ru ? $"Удалить выбранные SFX: {count}" : $"Delete {count} selected SFX"
        : DeleteSelectedSfx;
    public string AssignedTrackHotkeySummary(int count) => count switch
    {
        0 => Ru ? "Клавиши треков не назначены" : "No assigned track hotkeys",
        1 => Ru ? "1 назначенная клавиша трека" : "1 assigned track hotkey",
        _ => Ru ? $"{count} {PluralRu(count, "назначенная клавиша трека", "назначенные клавиши треков", "назначенных клавиш треков")}" : $"{count} assigned track hotkeys"
    };

    public string ImportConflictTitle => Ru ? "Дубли при импорте пропущены" : "Import duplicates skipped";
    public string ImportConflictMessage(int addedCount, int attemptedCount, string target, int duplicateCount) => Ru
        ? $"Импортировано {addedCount} из {attemptedCount} поддерживаемых файлов в {target}; пропущено дублей: {duplicateCount}."
        : $"Imported {addedCount} of {attemptedCount} supported file(s) into {target}; skipped {duplicateCount} duplicate file(s).";
    public string HotkeyConflictTitle => Ru ? "Клавиша уже назначена" : "Hotkey already assigned";
    public string HotkeyConflictMessage(string hotkey, string existingAction, string replacementAction) => Ru
        ? $"{hotkey} уже назначен для «{existingAction}». Заменить на «{replacementAction}»?"
        : $"{hotkey} is already assigned to {existingAction}. Replace it with {replacementAction}?";

    public string DeleteMusicPlaylistTitle => Ru ? "Удалить музыкальный плейлист?" : "Delete music playlist?";
    public string DeleteSfxPlaylistTitle => Ru ? "Удалить SFX-плейлист?" : "Delete SFX playlist?";
    public string DeleteMusicTrackTitle => Ru ? "Удалить музыкальный трек?" : "Delete music track?";
    public string DeleteSfxTrackTitle => Ru ? "Удалить SFX-трек?" : "Delete SFX track?";
    public string DeleteMusicTracksTitle(int count) => Ru ? $"Удалить музыкальные треки: {count}?" : $"Delete {count} music tracks?";
    public string DeleteSfxTracksTitle(int count) => Ru ? $"Удалить SFX-треки: {count}?" : $"Delete {count} SFX tracks?";
    public string DeletePlaylistMessage(string playlistName) => Ru
        ? $"Удалить плейлист «{playlistName}» и все его треки из Dungeon Soundboard? Аудиофайлы останутся на диске."
        : $"Remove playlist \"{playlistName}\" and all of its tracks from Dungeon Soundboard? Audio files stay on disk.";
    public string DeleteTrackMessage(string trackTitle) => Ru
        ? $"Удалить «{trackTitle}» из этого плейлиста? Аудиофайл останется на диске."
        : $"Remove \"{trackTitle}\" from this playlist? The audio file stays on disk.";
    public string DeleteTracksMessage(int count) => Ru
        ? $"Удалить треки из этого плейлиста: {count}? Аудиофайлы останутся на диске."
        : $"Remove {count} tracks from this playlist? The audio files stay on disk.";
    public string RenameMusicPlaylist => Ru ? "Переименовать музыкальный плейлист" : "Rename music playlist";
    public string RenameSfxPlaylist => Ru ? "Переименовать SFX-плейлист" : "Rename SFX playlist";
    public string RenameMusicTrack => Ru ? "Переименовать музыкальный трек" : "Rename music track";
    public string RenameSfxTrack => Ru ? "Переименовать SFX-трек" : "Rename SFX track";
    public string CouldNotOpenDataFolder(string message) => Ru ? $"Не удалось открыть папку данных: {message}" : $"Could not open data folder: {message}";
    public string CouldNotLoadData(string message) => Ru ? $"Не удалось загрузить данные: {message}" : $"Could not load data: {message}";
    public string CouldNotSaveData(string message) => Ru ? $"Не удалось сохранить данные: {message}" : $"Could not save data: {message}";
    public string RecoveredCorruptFiles(int count) => Ru
        ? $"Повреждённые файлы данных восстановлены из значений по умолчанию: {count}. Исходные файлы сохранены в папке Recovery."
        : $"Recovered {count} corrupt data file(s) using defaults. Original files were saved in the Recovery folder.";
    public string AudioFileIsMissing(string path) => Ru ? $"Аудиофайл отсутствует: {path}" : $"Audio file is missing: {path}";
    public string CouldNotOpenFileLocation(string message) => Ru ? $"Не удалось открыть расположение файла: {message}" : $"Could not open file location: {message}";
    public string CouldNotOpenAuthorPage(string message) => Ru ? $"Не удалось открыть страницу автора: {message}" : $"Could not open the author page: {message}";
    public string FailedToPlayFile(string title, string message) => Ru ? $"Не удалось воспроизвести файл «{title}». {message}" : $"Failed to play file: {title}. {message}";
    public string FailedToPlayEffect(string title, string message) => Ru ? $"Не удалось воспроизвести SFX «{title}». {message}" : $"Failed to play effect: {title}. {message}";
    public string UnsupportedAudioCodec(string message) => Ru ? "Файл найден, но Windows/NAudio не смогли декодировать этот аудиоформат." : "File found, but Windows/NAudio could not decode this audio format.";
    public string AudioOutputDeviceFailed(string message) => Ru ? "Не удалось открыть аудиоустройство." : "Could not open audio output device.";
    public string FailedToLoadBackgroundImage(string path) => Ru ? $"Не удалось загрузить фоновое изображение: {path}" : $"Failed to load background image: {path}";

    public IReadOnlyList<InterfaceDensityOption> InterfaceDensityOptions =>
    [
        new(InterfaceDensity.Compact, Ru ? "Компактная" : "Compact"),
        new(InterfaceDensity.Normal, Ru ? "Обычная" : "Normal"),
        new(InterfaceDensity.Spacious, Ru ? "Свободная" : "Spacious")
    ];

    public IReadOnlyList<RepeatModeOption> RepeatModeOptions =>
    [
        new(RepeatMode.Off, Ru ? "Без повтора" : "Repeat off"),
        new(RepeatMode.One, Ru ? "Повтор трека" : "Repeat one"),
        new(RepeatMode.All, Ru ? "Повтор плейлиста" : "Repeat all")
    ];

    public IReadOnlyList<BackgroundLayoutModeOption> BackgroundLayoutModeOptions =>
    [
        new(BackgroundLayoutMode.Fill, Ru ? "Заполнить" : "Fill"),
        new(BackgroundLayoutMode.Fit, Ru ? "Вписать" : "Fit"),
        new(BackgroundLayoutMode.Center, Ru ? "По центру" : "Center"),
        new(BackgroundLayoutMode.Tile, Ru ? "Плитка" : "Tile")
    ];

    public static string LanguageName(AppLanguage language) => language == AppLanguage.Russian ? "Русский" : "English";

    private static string PluralRu(int count, string one, string few, string many)
    {
        var mod10 = Math.Abs(count) % 10;
        var mod100 = Math.Abs(count) % 100;
        if (mod10 == 1 && mod100 != 11)
        {
            return one;
        }

        if (mod10 is >= 2 and <= 4 && mod100 is < 12 or > 14)
        {
            return few;
        }

        return many;
    }
}
