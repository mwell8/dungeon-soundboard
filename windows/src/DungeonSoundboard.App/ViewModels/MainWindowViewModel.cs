using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DungeonSoundboard.App.Services;
using DungeonSoundboard.Core.Models;
using DungeonSoundboard.Core.Services;
using System.Reflection;

namespace DungeonSoundboard.App.ViewModels;

public sealed class MainWindowViewModel : ObservableObject, IDisposable
{
    private const double BoostVolumeMultiplier = 1.5;
    private static readonly TimeSpan MusicPauseFadeDuration = TimeSpan.FromMilliseconds(1200);

    private readonly IStorageService _storage;
    private readonly IFileImportService _importService;
    private readonly IProfileTransferService _profileTransfer;
    private readonly IAudioService _audio;
    private readonly IExternalLauncher _externalLauncher;
    private readonly IErrorLogService _errorLog;
    private readonly DispatcherTimer _playbackProgressTimer;
    private readonly DispatcherTimer _saveDebounceTimer;
    private readonly DispatcherTimer _statusMessageTimer;
    private readonly EventHandler _musicFinishedHandler;
    private readonly EventHandler _effectPlaybackCountChangedHandler;
    private readonly int _ownerThreadId = Environment.CurrentManagedThreadId;
    private readonly Random _random;
    private AppState _state;
    private Playlist? _selectedMusicPlaylist;
    private EffectPlaylist? _selectedEffectPlaylist;
    private Track? _selectedMusicTrack;
    private Track? _selectedEffectTrack;
    private readonly HashSet<Guid> _selectedMusicTrackIds = [];
    private readonly HashSet<Guid> _selectedEffectTrackIds = [];
    private string _musicSearchText = "";
    private string _effectSearchText = "";
    private Track? _currentTrack;
    private Playlist? _playbackMusicPlaylist;
    private readonly PlaybackQueue _playbackQueue = new();
    private readonly Dictionary<Guid, TrackTileViewModel> _trackTileCache = [];
    private CancellationTokenSource? _importCancellation;
    private bool _isPlaying;
    private bool _isMusicPaused;
    private bool _isMusicDropTargetActive;
    private bool _isEffectDropTargetActive;
    private PendingTrackDelete? _pendingTrackDelete;
    private PendingPlaylistDelete? _pendingPlaylistDelete;
    private PendingTrackRename? _pendingTrackRename;
    private PendingPlaylistRename? _pendingPlaylistRename;
    private string _trackRenameName = "";
    private string _playlistRenameName = "";
    private string? _errorMessage;
    private string _lastImportMessage = "";
    private ImportConflictSummary? _importConflictSummary;
    private PendingHotkeyConflict? _pendingHotkeyConflict;
    private HotkeyAction? _captureAction;
    private ThemePresetOption? _selectedThemePreset;
    private string _customThemePresetName = "";
    private IBrush _backgroundBrush = Brushes.Black;
    private IBrush _backgroundOverlayBrush = Brushes.Transparent;
    private IBrush _panelBrush = Brushes.DimGray;
    private IBrush _panelAltBrush = Brushes.Gray;
    private IBrush _cardBrush = Brushes.Gray;
    private IBrush _currentCardBrush = Brushes.DarkGoldenrod;
    private IBrush _accentBrush = Brushes.Goldenrod;
    private IBrush _dangerBrush = Brushes.IndianRed;
    private IBrush _textPrimaryBrush = Brushes.White;
    private IBrush _textSecondaryBrush = Brushes.LightGray;
    private IBrush _panelAltTextPrimaryBrush = Brushes.White;
    private IBrush _panelAltTextSecondaryBrush = Brushes.LightGray;
    private IBrush _cardTextPrimaryBrush = Brushes.White;
    private IBrush _cardTextSecondaryBrush = Brushes.LightGray;
    private IBrush _currentCardTextPrimaryBrush = Brushes.White;
    private IBrush _currentCardTextSecondaryBrush = Brushes.LightGray;
    private IBrush _accentTextPrimaryBrush = Brushes.Black;
    private IBrush _accentTextSecondaryBrush = Brushes.DarkSlateGray;
    private IBrush _dividerBrush = Brushes.DimGray;
    private bool _isApplyingState;
    private bool _isSettingsVisible;
    private int _selectedSettingsTabIndex;
    private bool _isClearingStatusMessage;
    private bool _isDisposed;

    public MainWindowViewModel()
        : this(
            new JsonFileStorageService(),
            new FileImportService(),
            new NAudioAudioService(),
            new WindowsExternalLauncher(),
            errorLog: new LocalErrorLogService())
    {
    }

    public MainWindowViewModel(
        IStorageService storage,
        IFileImportService importService,
        IAudioService audio,
        IExternalLauncher? externalLauncher = null,
        IProfileTransferService? profileTransfer = null,
        Random? random = null,
        IErrorLogService? errorLog = null)
    {
        _storage = storage;
        _importService = importService;
        _profileTransfer = profileTransfer ?? new ProfileTransferService();
        _audio = audio;
        _externalLauncher = externalLauncher ?? new WindowsExternalLauncher();
        _errorLog = errorLog ?? NullErrorLogService.Instance;
        _random = random ?? new Random();
        _musicFinishedHandler = (_, _) => RunOnViewModelThread(NextTrackFromPlaybackEnd);
        _effectPlaybackCountChangedHandler = (_, _) => RunOnViewModelThread(HandleEffectPlaybackCountChanged);
        _audio.MusicFinished += _musicFinishedHandler;
        _audio.EffectPlaybackCountChanged += _effectPlaybackCountChangedHandler;
        _playbackProgressTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _playbackProgressTimer.Tick += (_, _) => RefreshPlaybackProgress();
        _playbackProgressTimer.Start();
        _saveDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _saveDebounceTimer.Tick += (_, _) =>
        {
            _saveDebounceTimer.Stop();
            Save();
        };
        _statusMessageTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
        _statusMessageTimer.Tick += (_, _) => ClearStatusMessage();

        try
        {
            _state = _storage.Load();
        }
        catch (Exception ex) when (IsStorageReadException(ex))
        {
            _state = new AppState();
            _state.EnsureDefaults();
            ReportError("storage.load", Ui.CouldNotLoadData(ex.Message), ex);
        }

        if (_storage is JsonFileStorageService { RecoveryWarnings.Count: > 0 } jsonStorage)
        {
            ErrorMessage = Ui.RecoveredCorruptFiles(jsonStorage.RecoveryWarnings.Count);
            _errorLog.Write("storage.recovery", string.Join(Environment.NewLine, jsonStorage.RecoveryWarnings));
        }

        MusicPlaylists = new ObservableCollection<Playlist>(_state.MusicPlaylists);
        EffectPlaylists = new ObservableCollection<EffectPlaylist>(_state.EffectPlaylists);
        SelectedMusicPlaylist = MusicPlaylists.FirstOrDefault(playlist => playlist.Id == _state.Preferences.SelectedMusicPlaylistId)
            ?? MusicPlaylists.FirstOrDefault();
        SelectedEffectPlaylist = EffectPlaylists.FirstOrDefault(playlist => playlist.Id == _state.Preferences.SelectedEffectPlaylistId)
            ?? EffectPlaylists.FirstOrDefault();
        CurrentTrack = SelectedMusicPlaylist?.Tracks.FirstOrDefault();
        _playbackMusicPlaylist = SelectedMusicPlaylist;

        ThemePresetOptions =
        [
            new ThemePresetOption(ThemePreset.ClassicDungeon, () => Ui.ClassicDungeonPreset),
            new ThemePresetOption(ThemePreset.TavernEmber, () => Ui.TavernEmberPreset),
            new ThemePresetOption(ThemePreset.MoonlitCrypt, () => Ui.MoonlitCryptPreset),
            new ThemePresetOption(ThemePreset.ForestMist, () => Ui.ForestMistPreset)
        ];
        ThemePaletteColorOptions = CreateThemePaletteColorOptions();
        ColumnCountOptions = [2, 3, 4];
        LanguageOptions =
        [
            new AppLanguageOption(AppLanguage.English, AppUiStrings.LanguageName(AppLanguage.English)),
            new AppLanguageOption(AppLanguage.Russian, AppUiStrings.LanguageName(AppLanguage.Russian))
        ];
        _selectedThemePreset = ThemePresetOptionFor(_state.Theme.Preset);
        UpdateThemeBrushes();
        _audio.SetEffectsVolume(EffectsVolume);

        CreateMusicPlaylistCommand = new RelayCommand(CreateMusicPlaylist);
        CreateEffectPlaylistCommand = new RelayCommand(CreateEffectPlaylist);
        DeleteMusicPlaylistCommand = new RelayCommand(DeleteSelectedMusicPlaylist, () => MusicPlaylists.Count > 1 && SelectedMusicPlaylist is not null);
        DeleteEffectPlaylistCommand = new RelayCommand(DeleteSelectedEffectPlaylist, () => EffectPlaylists.Count > 1 && SelectedEffectPlaylist is not null);
        RequestDeleteMusicPlaylistCommand = new RelayCommand(() => RequestDeleteMusicPlaylist(SelectedMusicPlaylist), () => MusicPlaylists.Count > 1 && SelectedMusicPlaylist is not null);
        RequestDeleteEffectPlaylistCommand = new RelayCommand(() => RequestDeleteEffectPlaylist(SelectedEffectPlaylist), () => EffectPlaylists.Count > 1 && SelectedEffectPlaylist is not null);
        MoveMusicPlaylistUpCommand = new RelayCommand(() => MoveSelectedPlaylist(MusicPlaylists, SelectedMusicPlaylist, -1), () => CanMove(MusicPlaylists, SelectedMusicPlaylist, -1));
        MoveMusicPlaylistDownCommand = new RelayCommand(() => MoveSelectedPlaylist(MusicPlaylists, SelectedMusicPlaylist, 1), () => CanMove(MusicPlaylists, SelectedMusicPlaylist, 1));
        MoveEffectPlaylistUpCommand = new RelayCommand(() => MoveSelectedPlaylist(EffectPlaylists, SelectedEffectPlaylist, -1), () => CanMove(EffectPlaylists, SelectedEffectPlaylist, -1));
        MoveEffectPlaylistDownCommand = new RelayCommand(() => MoveSelectedPlaylist(EffectPlaylists, SelectedEffectPlaylist, 1), () => CanMove(EffectPlaylists, SelectedEffectPlaylist, 1));
        PlayMusicPlaylistItemCommand = new RelayCommand<Playlist>(PlayMusicPlaylistItem);
        PlayEffectPlaylistItemCommand = new RelayCommand<EffectPlaylist>(PlayEffectPlaylistItem);
        DeleteMusicPlaylistItemCommand = new RelayCommand<Playlist>(DeleteMusicPlaylist);
        DeleteEffectPlaylistItemCommand = new RelayCommand<EffectPlaylist>(DeleteEffectPlaylist);
        RequestDeleteMusicPlaylistItemCommand = new RelayCommand<Playlist>(RequestDeleteMusicPlaylist);
        RequestDeleteEffectPlaylistItemCommand = new RelayCommand<EffectPlaylist>(RequestDeleteEffectPlaylist);
        RequestRenameMusicPlaylistItemCommand = new RelayCommand<Playlist>(RequestRenameMusicPlaylist);
        RequestRenameEffectPlaylistItemCommand = new RelayCommand<EffectPlaylist>(RequestRenameEffectPlaylist);
        MoveMusicPlaylistItemUpCommand = new RelayCommand<Playlist>(playlist => MoveMusicPlaylistItem(playlist, -1));
        MoveMusicPlaylistItemDownCommand = new RelayCommand<Playlist>(playlist => MoveMusicPlaylistItem(playlist, 1));
        MoveEffectPlaylistItemUpCommand = new RelayCommand<EffectPlaylist>(playlist => MoveEffectPlaylistItem(playlist, -1));
        MoveEffectPlaylistItemDownCommand = new RelayCommand<EffectPlaylist>(playlist => MoveEffectPlaylistItem(playlist, 1));
        AddMusicFilesCommand = new AsyncRelayCommand(AddMusicFilesAsync);
        AddMusicFolderCommand = new AsyncRelayCommand(AddMusicFolderAsync);
        AddEffectFilesCommand = new AsyncRelayCommand(AddEffectFilesAsync);
        AddEffectFolderCommand = new AsyncRelayCommand(AddEffectFolderAsync);
        PlaySelectedMusicCommand = new RelayCommand(() => PlayMusicTrack(SelectedMusicPlaylist, SelectedMusicTrack), () => SelectedMusicTrack is not null);
        PlayMusicTrackCommand = new RelayCommand<Track>(track => PlayMusicTrack(SelectedMusicPlaylist, track));
        PlayEffectCommand = new RelayCommand<Track>(PlayEffect);
        PlayMusicTrackTileCommand = new RelayCommand<TrackTileViewModel>(PlayMusicTrackTile);
        PlayEffectTrackTileCommand = new RelayCommand<TrackTileViewModel>(PlayEffectTrackTile);
        PlayPauseCommand = new RelayCommand(PlayPause);
        StopAllCommand = new RelayCommand(StopAll, CanStopAll);
        StopEffectsCommand = new RelayCommand(StopEffects, () => _audio.ActiveEffectCount > 0);
        NextTrackCommand = new RelayCommand(NextTrack, CanNavigateMusic);
        PreviousTrackCommand = new RelayCommand(PreviousTrack, CanNavigateMusic);
        DeleteMusicTrackCommand = new RelayCommand(DeleteSelectedMusicTrack, () => SelectedMusicTrack is not null);
        DeleteEffectTrackCommand = new RelayCommand(DeleteSelectedEffectTrack, () => SelectedEffectTrack is not null);
        MoveMusicTrackUpCommand = new RelayCommand(() => MoveSelectedTrack(SelectedMusicPlaylist?.Tracks, SelectedMusicTrack, -1, nameof(MusicTracks)), () => CanMove(SelectedMusicPlaylist?.Tracks, SelectedMusicTrack, -1));
        MoveMusicTrackDownCommand = new RelayCommand(() => MoveSelectedTrack(SelectedMusicPlaylist?.Tracks, SelectedMusicTrack, 1, nameof(MusicTracks)), () => CanMove(SelectedMusicPlaylist?.Tracks, SelectedMusicTrack, 1));
        MoveEffectTrackUpCommand = new RelayCommand(() => MoveSelectedTrack(SelectedEffectPlaylist?.Effects, SelectedEffectTrack, -1, nameof(EffectTracks)), () => CanMove(SelectedEffectPlaylist?.Effects, SelectedEffectTrack, -1));
        MoveEffectTrackDownCommand = new RelayCommand(() => MoveSelectedTrack(SelectedEffectPlaylist?.Effects, SelectedEffectTrack, 1, nameof(EffectTracks)), () => CanMove(SelectedEffectPlaylist?.Effects, SelectedEffectTrack, 1));
        RequestDeleteMusicTrackCommand = new RelayCommand(RequestDeleteSelectedMusicTracks, () => HasMusicTrackDeleteSelection);
        RequestDeleteEffectTrackCommand = new RelayCommand(RequestDeleteSelectedEffectTracks, () => HasEffectTrackDeleteSelection);
        DeleteMusicTrackItemCommand = new RelayCommand<Track>(DeleteMusicTrack);
        DeleteEffectTrackItemCommand = new RelayCommand<Track>(DeleteEffectTrack);
        RequestDeleteMusicTrackItemCommand = new RelayCommand<Track>(RequestDeleteMusicTrack);
        RequestDeleteEffectTrackItemCommand = new RelayCommand<Track>(RequestDeleteEffectTrack);
        RequestRenameMusicTrackItemCommand = new RelayCommand<Track>(RequestRenameMusicTrack);
        RequestRenameEffectTrackItemCommand = new RelayCommand<Track>(RequestRenameEffectTrack);
        MoveMusicTrackItemUpCommand = new RelayCommand<Track>(track => MoveTrackItem(SelectedMusicPlaylist?.Tracks, track, -1, nameof(MusicTracks)));
        MoveMusicTrackItemDownCommand = new RelayCommand<Track>(track => MoveTrackItem(SelectedMusicPlaylist?.Tracks, track, 1, nameof(MusicTracks)));
        MoveEffectTrackItemUpCommand = new RelayCommand<Track>(track => MoveTrackItem(SelectedEffectPlaylist?.Effects, track, -1, nameof(EffectTracks)));
        MoveEffectTrackItemDownCommand = new RelayCommand<Track>(track => MoveTrackItem(SelectedEffectPlaylist?.Effects, track, 1, nameof(EffectTracks)));
        MuteMusicTrackItemVolumeCommand = new RelayCommand<Track>(track => SetMusicTrackItemVolume(track, Track.MinimumVolumeMultiplier));
        DefaultMusicTrackItemVolumeCommand = new RelayCommand<Track>(track => SetMusicTrackItemVolume(track, Track.DefaultVolumeMultiplier));
        BoostMusicTrackItemVolumeCommand = new RelayCommand<Track>(track => SetMusicTrackItemVolume(track, BoostVolumeMultiplier));
        MuteEffectTrackItemVolumeCommand = new RelayCommand<Track>(track => SetEffectTrackItemVolume(track, Track.MinimumVolumeMultiplier));
        DefaultEffectTrackItemVolumeCommand = new RelayCommand<Track>(track => SetEffectTrackItemVolume(track, Track.DefaultVolumeMultiplier));
        BoostEffectTrackItemVolumeCommand = new RelayCommand<Track>(track => SetEffectTrackItemVolume(track, BoostVolumeMultiplier));
        ConfirmDeleteCommand = new RelayCommand(ConfirmDelete, () => HasPendingDelete);
        CancelDeleteCommand = new RelayCommand(CancelDelete, () => HasPendingDelete);
        ConfirmPlaylistRenameCommand = new RelayCommand(ConfirmPlaylistRename, CanConfirmPlaylistRename);
        CancelPlaylistRenameCommand = new RelayCommand(CancelPlaylistRename, () => IsPlaylistRenameVisible);
        ConfirmTrackRenameCommand = new RelayCommand(ConfirmTrackRename, CanConfirmTrackRename);
        CancelTrackRenameCommand = new RelayCommand(CancelTrackRename, () => IsTrackRenameVisible);
        DismissImportConflictCommand = new RelayCommand(DismissImportConflict, () => IsImportConflictVisible);
        ConfirmHotkeyConflictCommand = new RelayCommand(ConfirmHotkeyConflict, () => IsHotkeyConflictVisible);
        CancelHotkeyConflictCommand = new RelayCommand(CancelHotkeyConflict, () => IsHotkeyConflictVisible);
        BindSelectedMusicCommand = new RelayCommand(BeginBindSelectedMusic, () => SelectedMusicPlaylist is not null && SelectedMusicTrack is not null);
        BindSelectedEffectCommand = new RelayCommand(BeginBindSelectedEffect, () => SelectedEffectPlaylist is not null && SelectedEffectTrack is not null);
        BindMusicTrackItemCommand = new RelayCommand<Track>(BeginBindMusicTrack);
        BindEffectTrackItemCommand = new RelayCommand<Track>(BeginBindEffectTrack);
        BindSystemHotkeyCommand = new RelayCommand<HotkeyAction>(BeginBindSystemHotkey);
        ClearSystemHotkeyCommand = new RelayCommand<HotkeyAction>(ClearSystemHotkey, CanClearSystemHotkey);
        RestoreDefaultSystemHotkeyCommand = new RelayCommand<HotkeyAction>(RestoreDefaultSystemHotkey);
        RestoreDefaultHotkeysCommand = new RelayCommand(RestoreDefaultHotkeys);
        CancelHotkeyCaptureCommand = new RelayCommand(CancelHotkeyCapture, () => CaptureAction is not null);
        ClearSelectedMusicBindingCommand = new RelayCommand(ClearSelectedMusicBinding, CanClearSelectedMusicBinding);
        ClearSelectedEffectBindingCommand = new RelayCommand(ClearSelectedEffectBinding, CanClearSelectedEffectBinding);
        ClearMusicTrackItemBindingCommand = new RelayCommand<Track>(ClearMusicTrackBinding);
        ClearEffectTrackItemBindingCommand = new RelayCommand<Track>(ClearEffectTrackBinding);
        ChooseBackgroundImageCommand = new AsyncRelayCommand(ChooseBackgroundImageAsync);
        ClearBackgroundImageCommand = new RelayCommand(ClearBackgroundImage, () => HasBackgroundImage);
        ClearMusicSearchCommand = new RelayCommand(() => MusicSearchText = "");
        ClearEffectSearchCommand = new RelayCommand(() => EffectSearchText = "");
        ResetThemeCommand = new RelayCommand(ResetTheme);
        RestoreVisualDefaultsCommand = new RelayCommand(RestoreVisualDefaults);
        SaveCurrentThemeAsCustomPresetCommand = new RelayCommand(SaveCurrentThemeAsCustomPreset, CanSaveCurrentThemeAsCustomPreset);
        ApplyCustomThemePresetCommand = new RelayCommand<CustomThemePresetOption>(ApplyCustomThemePreset);
        DeleteCustomThemePresetCommand = new RelayCommand<CustomThemePresetOption>(DeleteCustomThemePreset);
        OpenDataDirectoryCommand = new RelayCommand(OpenDataDirectory);
        OpenErrorLogDirectoryCommand = new RelayCommand(OpenErrorLogDirectory);
        OpenAuthorPageCommand = new RelayCommand(OpenAuthorPage);
        OpenTrackLocationCommand = new RelayCommand<Track>(OpenTrackLocation);
        ExportProfileCommand = new AsyncRelayCommand(ExportProfileAsync);
        RestoreProfileCommand = new AsyncRelayCommand(RestoreProfileAsync);
        RemoveMissingMusicFilesCommand = new RelayCommand(RemoveMissingMusicFiles, () => SelectedMusicPlaylist is not null);
        RemoveMissingEffectFilesCommand = new RelayCommand(RemoveMissingEffectFiles, () => SelectedEffectPlaylist is not null);
        OpenSettingsCommand = new RelayCommand(OpenSettings);
        CloseSettingsCommand = new RelayCommand(CloseSettings);
    }

    public IFileDialogService? FileDialogService { get; set; }

    public ObservableCollection<Playlist> MusicPlaylists { get; }
    public ObservableCollection<EffectPlaylist> EffectPlaylists { get; }
    public IReadOnlyList<ThemePresetOption> ThemePresetOptions { get; }
    public IReadOnlyList<ThemePaletteColorOption> ThemePaletteColorOptions { get; }
    public IReadOnlyList<CustomThemePresetOption> CustomThemePresetOptions => _state.CustomThemePresets
        .Select(preset => new CustomThemePresetOption(preset.Id, preset.Name, preset))
        .ToArray();

    public bool HasCustomThemePresets => _state.CustomThemePresets.Count > 0;

    public string CustomThemePresetSummary => _state.CustomThemePresets.Count switch
    {
        0 => Ui.NoSavedCustomThemes,
        1 => Ui.OneSavedCustomTheme,
        var count => Ui.SavedCustomThemes(count)
    };

    public IReadOnlyList<InterfaceDensityOption> InterfaceDensityOptions => Ui.InterfaceDensityOptions;
    public IReadOnlyList<RepeatModeOption> RepeatModeOptions => Ui.RepeatModeOptions;
    public IReadOnlyList<int> ColumnCountOptions { get; }
    public IReadOnlyList<AppLanguageOption> LanguageOptions { get; }
    public IReadOnlyList<BackgroundLayoutModeOption> BackgroundLayoutModeOptions => Ui.BackgroundLayoutModeOptions;

    public IRelayCommand CreateMusicPlaylistCommand { get; }
    public IRelayCommand CreateEffectPlaylistCommand { get; }
    public IRelayCommand DeleteMusicPlaylistCommand { get; }
    public IRelayCommand DeleteEffectPlaylistCommand { get; }
    public IRelayCommand RequestDeleteMusicPlaylistCommand { get; }
    public IRelayCommand RequestDeleteEffectPlaylistCommand { get; }
    public IRelayCommand MoveMusicPlaylistUpCommand { get; }
    public IRelayCommand MoveMusicPlaylistDownCommand { get; }
    public IRelayCommand MoveEffectPlaylistUpCommand { get; }
    public IRelayCommand MoveEffectPlaylistDownCommand { get; }
    public IRelayCommand<Playlist> PlayMusicPlaylistItemCommand { get; }
    public IRelayCommand<EffectPlaylist> PlayEffectPlaylistItemCommand { get; }
    public IRelayCommand<Playlist> DeleteMusicPlaylistItemCommand { get; }
    public IRelayCommand<EffectPlaylist> DeleteEffectPlaylistItemCommand { get; }
    public IRelayCommand<Playlist> RequestDeleteMusicPlaylistItemCommand { get; }
    public IRelayCommand<EffectPlaylist> RequestDeleteEffectPlaylistItemCommand { get; }
    public IRelayCommand<Playlist> RequestRenameMusicPlaylistItemCommand { get; }
    public IRelayCommand<EffectPlaylist> RequestRenameEffectPlaylistItemCommand { get; }
    public IRelayCommand<Playlist> MoveMusicPlaylistItemUpCommand { get; }
    public IRelayCommand<Playlist> MoveMusicPlaylistItemDownCommand { get; }
    public IRelayCommand<EffectPlaylist> MoveEffectPlaylistItemUpCommand { get; }
    public IRelayCommand<EffectPlaylist> MoveEffectPlaylistItemDownCommand { get; }
    public IAsyncRelayCommand AddMusicFilesCommand { get; }
    public IAsyncRelayCommand AddMusicFolderCommand { get; }
    public IAsyncRelayCommand AddEffectFilesCommand { get; }
    public IAsyncRelayCommand AddEffectFolderCommand { get; }
    public IRelayCommand PlaySelectedMusicCommand { get; }
    public IRelayCommand<Track> PlayMusicTrackCommand { get; }
    public IRelayCommand<Track> PlayEffectCommand { get; }
    public IRelayCommand<TrackTileViewModel> PlayMusicTrackTileCommand { get; }
    public IRelayCommand<TrackTileViewModel> PlayEffectTrackTileCommand { get; }
    public IRelayCommand PlayPauseCommand { get; }
    public IRelayCommand StopAllCommand { get; }
    public IRelayCommand StopEffectsCommand { get; }
    public IRelayCommand NextTrackCommand { get; }
    public IRelayCommand PreviousTrackCommand { get; }
    public IRelayCommand DeleteMusicTrackCommand { get; }
    public IRelayCommand DeleteEffectTrackCommand { get; }
    public IRelayCommand MoveMusicTrackUpCommand { get; }
    public IRelayCommand MoveMusicTrackDownCommand { get; }
    public IRelayCommand MoveEffectTrackUpCommand { get; }
    public IRelayCommand MoveEffectTrackDownCommand { get; }
    public IRelayCommand RequestDeleteMusicTrackCommand { get; }
    public IRelayCommand RequestDeleteEffectTrackCommand { get; }
    public IRelayCommand<Track> DeleteMusicTrackItemCommand { get; }
    public IRelayCommand<Track> DeleteEffectTrackItemCommand { get; }
    public IRelayCommand<Track> RequestDeleteMusicTrackItemCommand { get; }
    public IRelayCommand<Track> RequestDeleteEffectTrackItemCommand { get; }
    public IRelayCommand<Track> RequestRenameMusicTrackItemCommand { get; }
    public IRelayCommand<Track> RequestRenameEffectTrackItemCommand { get; }
    public IRelayCommand<Track> MoveMusicTrackItemUpCommand { get; }
    public IRelayCommand<Track> MoveMusicTrackItemDownCommand { get; }
    public IRelayCommand<Track> MoveEffectTrackItemUpCommand { get; }
    public IRelayCommand<Track> MoveEffectTrackItemDownCommand { get; }
    public IRelayCommand<Track> MuteMusicTrackItemVolumeCommand { get; }
    public IRelayCommand<Track> DefaultMusicTrackItemVolumeCommand { get; }
    public IRelayCommand<Track> BoostMusicTrackItemVolumeCommand { get; }
    public IRelayCommand<Track> MuteEffectTrackItemVolumeCommand { get; }
    public IRelayCommand<Track> DefaultEffectTrackItemVolumeCommand { get; }
    public IRelayCommand<Track> BoostEffectTrackItemVolumeCommand { get; }
    public IRelayCommand ConfirmDeleteCommand { get; }
    public IRelayCommand CancelDeleteCommand { get; }
    public IRelayCommand ConfirmPlaylistRenameCommand { get; }
    public IRelayCommand CancelPlaylistRenameCommand { get; }
    public IRelayCommand ConfirmTrackRenameCommand { get; }
    public IRelayCommand CancelTrackRenameCommand { get; }
    public IRelayCommand DismissImportConflictCommand { get; }
    public IRelayCommand ConfirmHotkeyConflictCommand { get; }
    public IRelayCommand CancelHotkeyConflictCommand { get; }
    public IRelayCommand BindSelectedMusicCommand { get; }
    public IRelayCommand BindSelectedEffectCommand { get; }
    public IRelayCommand<Track> BindMusicTrackItemCommand { get; }
    public IRelayCommand<Track> BindEffectTrackItemCommand { get; }
    public IRelayCommand<HotkeyAction> BindSystemHotkeyCommand { get; }
    public IRelayCommand<HotkeyAction> ClearSystemHotkeyCommand { get; }
    public IRelayCommand<HotkeyAction> RestoreDefaultSystemHotkeyCommand { get; }
    public IRelayCommand RestoreDefaultHotkeysCommand { get; }
    public IRelayCommand CancelHotkeyCaptureCommand { get; }
    public IRelayCommand ClearSelectedMusicBindingCommand { get; }
    public IRelayCommand ClearSelectedEffectBindingCommand { get; }
    public IRelayCommand<Track> ClearMusicTrackItemBindingCommand { get; }
    public IRelayCommand<Track> ClearEffectTrackItemBindingCommand { get; }
    public IAsyncRelayCommand ChooseBackgroundImageCommand { get; }
    public IRelayCommand ClearBackgroundImageCommand { get; }
    public IRelayCommand ClearMusicSearchCommand { get; }
    public IRelayCommand ClearEffectSearchCommand { get; }
    public IRelayCommand ResetThemeCommand { get; }
    public IRelayCommand RestoreVisualDefaultsCommand { get; }
    public IRelayCommand SaveCurrentThemeAsCustomPresetCommand { get; }
    public IRelayCommand<CustomThemePresetOption> ApplyCustomThemePresetCommand { get; }
    public IRelayCommand<CustomThemePresetOption> DeleteCustomThemePresetCommand { get; }
    public IRelayCommand OpenDataDirectoryCommand { get; }
    public IRelayCommand OpenErrorLogDirectoryCommand { get; }
    public IRelayCommand OpenAuthorPageCommand { get; }
    public IRelayCommand<Track> OpenTrackLocationCommand { get; }
    public IAsyncRelayCommand ExportProfileCommand { get; }
    public IAsyncRelayCommand RestoreProfileCommand { get; }
    public IRelayCommand RemoveMissingMusicFilesCommand { get; }
    public IRelayCommand RemoveMissingEffectFilesCommand { get; }
    public IRelayCommand OpenSettingsCommand { get; }
    public IRelayCommand CloseSettingsCommand { get; }

    public Playlist? SelectedMusicPlaylist
    {
        get => _selectedMusicPlaylist;
        set
        {
            if (!SetProperty(ref _selectedMusicPlaylist, value))
            {
                return;
            }

            _state.Preferences.SelectedMusicPlaylistId = value?.Id;
            SetSelectedMusicTrackIds([]);
            SelectedMusicTrack = value?.Tracks.FirstOrDefault();
            NotifyMusicTracksChanged();
            OnPropertyChanged(nameof(SelectedMusicPlaylistName));
            OnPropertyChanged(nameof(CurrentPlaybackPlaylistName));
            if (!_isApplyingState)
            {
                Save();
            }

            NotifyCommandStates();
        }
    }

    public EffectPlaylist? SelectedEffectPlaylist
    {
        get => _selectedEffectPlaylist;
        set
        {
            if (!SetProperty(ref _selectedEffectPlaylist, value))
            {
                return;
            }

            _state.Preferences.SelectedEffectPlaylistId = value?.Id;
            SetSelectedEffectTrackIds([]);
            SelectedEffectTrack = value?.Effects.FirstOrDefault();
            NotifyEffectTracksChanged();
            OnPropertyChanged(nameof(SelectedEffectPlaylistName));
            if (!_isApplyingState)
            {
                Save();
            }

            NotifyCommandStates();
        }
    }

    public Track? SelectedMusicTrack
    {
        get => _selectedMusicTrack;
        set
        {
            if (SetProperty(ref _selectedMusicTrack, value))
            {
                OnPropertyChanged(nameof(SelectedMusicTrackTitle));
                OnPropertyChanged(nameof(SelectedMusicTrackDisplayTitle));
                OnPropertyChanged(nameof(SelectedMusicTrackVolume));
                OnPropertyChanged(nameof(SelectedMusicHotkeyText));
                OnPropertyChanged(nameof(SelectedMusicTrackFileStatus));
                OnPropertyChanged(nameof(SelectedMusicTrackTile));
                OnPropertyChanged(nameof(HasSelectedMusicTrack));
                OnPropertyChanged(nameof(HasMusicTrackDeleteSelection));
                NotifyCommandStates();
            }
        }
    }

    public Track? SelectedEffectTrack
    {
        get => _selectedEffectTrack;
        set
        {
            if (SetProperty(ref _selectedEffectTrack, value))
            {
                OnPropertyChanged(nameof(SelectedEffectTrackTitle));
                OnPropertyChanged(nameof(SelectedEffectTrackDisplayTitle));
                OnPropertyChanged(nameof(SelectedEffectTrackVolume));
                OnPropertyChanged(nameof(SelectedEffectHotkeyText));
                OnPropertyChanged(nameof(SelectedEffectTrackFileStatus));
                OnPropertyChanged(nameof(SelectedEffectTrackTile));
                OnPropertyChanged(nameof(HasSelectedEffectTrack));
                OnPropertyChanged(nameof(HasEffectTrackDeleteSelection));
                NotifyCommandStates();
            }
        }
    }

    public IReadOnlyList<Track> MusicTracks => SelectedMusicPlaylist?.Tracks ?? [];
    public IReadOnlyList<Track> EffectTracks => SelectedEffectPlaylist?.Effects ?? [];
    public string MusicSearchText
    {
        get => _musicSearchText;
        set
        {
            if (SetProperty(ref _musicSearchText, value ?? ""))
            {
                NotifyMusicTrackFilterChanged();
            }
        }
    }

    public string EffectSearchText
    {
        get => _effectSearchText;
        set
        {
            if (SetProperty(ref _effectSearchText, value ?? ""))
            {
                NotifyEffectTrackFilterChanged();
            }
        }
    }

    public IReadOnlyList<TrackTileViewModel> MusicTrackTiles => SelectedMusicPlaylist is null
        ? []
        : SelectedMusicPlaylist.Tracks
            .Where(track => TrackMatchesSearch(track, MusicSearchText))
            .Select(track => TrackTileFor(track, HotkeyAction.PlayMusicTrack(SelectedMusicPlaylist.Id, track.Id), IsMusicTransportActive && CurrentTrack?.Id == track.Id))
            .ToList();

    public IReadOnlyList<TrackTileViewModel> EffectTrackTiles => SelectedEffectPlaylist is null
        ? []
        : SelectedEffectPlaylist.Effects
            .Where(track => TrackMatchesSearch(track, EffectSearchText))
            .Select(track => TrackTileFor(track, HotkeyAction.PlayEffect(SelectedEffectPlaylist.Id, track.Id), isCurrent: false))
            .ToList();

    public TrackTileViewModel? SelectedMusicTrackTile
    {
        get => SelectedMusicTrack is null
            ? null
            : MusicTrackTiles.FirstOrDefault(tile => tile.Track.Id == SelectedMusicTrack.Id);
        set => SelectedMusicTrack = value?.Track;
    }

    public TrackTileViewModel? SelectedEffectTrackTile
    {
        get => SelectedEffectTrack is null
            ? null
            : EffectTrackTiles.FirstOrDefault(tile => tile.Track.Id == SelectedEffectTrack.Id);
        set => SelectedEffectTrack = value?.Track;
    }

    public bool HasMusicTracks => MusicTracks.Count > 0;
    public bool HasEffectTracks => EffectTracks.Count > 0;
    public bool HasMusicSearchText => !string.IsNullOrWhiteSpace(MusicSearchText);
    public bool HasEffectSearchText => !string.IsNullOrWhiteSpace(EffectSearchText);
    public bool HasVisibleMusicTrackTiles => MusicTrackTiles.Count > 0;
    public bool HasVisibleEffectTrackTiles => EffectTrackTiles.Count > 0;
    public bool HasSelectedMusicTrack => SelectedMusicTrack is not null;
    public bool HasSelectedEffectTrack => SelectedEffectTrack is not null;
    public bool HasMusicTrackDeleteSelection => _selectedMusicTrackIds.Count > 0 || SelectedMusicTrack is not null;
    public bool HasEffectTrackDeleteSelection => _selectedEffectTrackIds.Count > 0 || SelectedEffectTrack is not null;
    public bool HasMusicMultiSelection => _selectedMusicTrackIds.Count > 1;
    public bool HasEffectMultiSelection => _selectedEffectTrackIds.Count > 1;
    public string MusicSelectionSummary => Ui.SelectionSummary(_selectedMusicTrackIds.Count);
    public string EffectSelectionSummary => Ui.SelectionSummary(_selectedEffectTrackIds.Count);
    public string MusicDeleteSelectionToolTip => _selectedMusicTrackIds.Count > 1
        ? Ui.DeleteSelectedTracksToolTip(_selectedMusicTrackIds.Count)
        : Ui.DeleteSelectedTrack;
    public string EffectDeleteSelectionToolTip => _selectedEffectTrackIds.Count > 1
        ? Ui.DeleteSelectedSfxToolTip(_selectedEffectTrackIds.Count)
        : Ui.DeleteSelectedSfx;
    public bool IsMusicTracksEmpty => !HasMusicTracks;
    public bool IsEffectTracksEmpty => !HasEffectTracks;
    public bool IsMusicSearchEmpty => HasMusicTracks && HasMusicSearchText && !HasVisibleMusicTrackTiles;
    public bool IsEffectSearchEmpty => HasEffectTracks && HasEffectSearchText && !HasVisibleEffectTrackTiles;
    public string MusicTrackCountText => Ui.TrackCount(MusicTracks.Count);
    public string EffectTrackCountText => Ui.EffectCount(EffectTracks.Count);

    public void SetSelectedMusicTrackTiles(IEnumerable<TrackTileViewModel>? tiles)
    {
        var validTrackIds = SelectedMusicPlaylist?.Tracks.Select(track => track.Id).ToHashSet() ?? [];
        SetSelectedMusicTrackIds((tiles ?? [])
            .Select(tile => tile.Track.Id)
            .Where(validTrackIds.Contains));
    }

    public void SetSelectedEffectTrackTiles(IEnumerable<TrackTileViewModel>? tiles)
    {
        var validTrackIds = SelectedEffectPlaylist?.Effects.Select(track => track.Id).ToHashSet() ?? [];
        SetSelectedEffectTrackIds((tiles ?? [])
            .Select(tile => tile.Track.Id)
            .Where(validTrackIds.Contains));
    }

    private void SetSelectedMusicTrackIds(IEnumerable<Guid> trackIds)
    {
        var nextIds = trackIds.ToHashSet();
        if (_selectedMusicTrackIds.SetEquals(nextIds))
        {
            return;
        }

        _selectedMusicTrackIds.Clear();
        foreach (var trackId in nextIds)
        {
            _selectedMusicTrackIds.Add(trackId);
        }

        OnPropertyChanged(nameof(HasMusicTrackDeleteSelection));
        OnPropertyChanged(nameof(HasMusicMultiSelection));
        OnPropertyChanged(nameof(MusicSelectionSummary));
        OnPropertyChanged(nameof(MusicDeleteSelectionToolTip));
        RequestDeleteMusicTrackCommand?.NotifyCanExecuteChanged();
    }

    private void SetSelectedEffectTrackIds(IEnumerable<Guid> trackIds)
    {
        var nextIds = trackIds.ToHashSet();
        if (_selectedEffectTrackIds.SetEquals(nextIds))
        {
            return;
        }

        _selectedEffectTrackIds.Clear();
        foreach (var trackId in nextIds)
        {
            _selectedEffectTrackIds.Add(trackId);
        }

        OnPropertyChanged(nameof(HasEffectTrackDeleteSelection));
        OnPropertyChanged(nameof(HasEffectMultiSelection));
        OnPropertyChanged(nameof(EffectSelectionSummary));
        OnPropertyChanged(nameof(EffectDeleteSelectionToolTip));
        RequestDeleteEffectTrackCommand?.NotifyCanExecuteChanged();
    }

    public string SelectedMusicPlaylistName
    {
        get => SelectedMusicPlaylist?.Name ?? "";
        set
        {
            if (SelectedMusicPlaylist is null)
            {
                return;
            }

            RenamePlaylist(SelectedMusicPlaylist, value, Ui.DefaultMusicPlaylistName);
        }
    }

    public string SelectedEffectPlaylistName
    {
        get => SelectedEffectPlaylist?.Name ?? "";
        set
        {
            if (SelectedEffectPlaylist is null)
            {
                return;
            }

            RenamePlaylist(SelectedEffectPlaylist, value, Ui.DefaultSfxPlaylistName);
        }
    }

    public string SelectedMusicTrackTitle
    {
        get => SelectedMusicTrack?.Title ?? "";
        set
        {
            if (SelectedMusicTrack is null)
            {
                return;
            }

            RenameTrack(SelectedMusicTrack, value);
            OnPropertyChanged(nameof(SelectedMusicTrackDisplayTitle));
            NotifyMusicTracksChanged();
        }
    }

    public string SelectedMusicTrackDisplayTitle => SelectedMusicTrack?.Title ?? Ui.NoMusicTrackSelected;

    public string SelectedEffectTrackTitle
    {
        get => SelectedEffectTrack?.Title ?? "";
        set
        {
            if (SelectedEffectTrack is null)
            {
                return;
            }

            RenameTrack(SelectedEffectTrack, value);
            OnPropertyChanged(nameof(SelectedEffectTrackDisplayTitle));
            NotifyEffectTracksChanged();
        }
    }

    public string SelectedEffectTrackDisplayTitle => SelectedEffectTrack?.Title ?? Ui.NoSfxTrackSelected;

    public double SelectedMusicTrackVolume
    {
        get => SelectedMusicTrack?.VolumeMultiplier ?? Track.DefaultVolumeMultiplier;
        set
        {
            if (SelectedMusicTrack is null)
            {
                return;
            }

            SelectedMusicTrack.VolumeMultiplier = value;
            ApplyMusicVolume();
            Save();
            OnPropertyChanged();
            NotifyMusicTracksChanged();
        }
    }

    public double SelectedEffectTrackVolume
    {
        get => SelectedEffectTrack?.VolumeMultiplier ?? Track.DefaultVolumeMultiplier;
        set
        {
            if (SelectedEffectTrack is null)
            {
                return;
            }

            SelectedEffectTrack.VolumeMultiplier = value;
            Save();
            OnPropertyChanged();
            NotifyEffectTracksChanged();
        }
    }

    public Track? CurrentTrack
    {
        get => _currentTrack;
        private set
        {
            if (SetProperty(ref _currentTrack, value))
            {
                OnPropertyChanged(nameof(CurrentTrackTitle));
                OnPropertyChanged(nameof(MusicTrackTiles));
                OnPropertyChanged(nameof(SelectedMusicTrackTile));
                ApplyMusicVolume();
                PreviousTrackCommand?.NotifyCanExecuteChanged();
                NextTrackCommand?.NotifyCanExecuteChanged();
            }
        }
    }

    public string CurrentTrackTitle => IsMusicTransportActive && CurrentTrack is not null
        ? CurrentTrack.Title
        : Ui.NothingIsPlaying;

    public string CurrentPlaybackPlaylistName => (_playbackMusicPlaylist ?? SelectedMusicPlaylist)?.Name ?? Ui.NoPlaylistSelected;

    public string PlaybackStatus => IsPlaying ? Ui.Playing : Ui.PausedStopped;

    public string PlayPauseButtonText => IsPlaying ? Ui.Pause : _isMusicPaused ? Ui.Resume : Ui.Play;

    public string PlayPauseIconData => IsPlaying
        ? "M7,5H10V19H7V5M14,5H17V19H14V5Z"
        : "M8,5V19L19,12L8,5Z";

    public double PlaybackPositionSeconds
    {
        get => Math.Max(0, _audio.MusicPosition.TotalSeconds);
        set => SeekPlayback(value);
    }

    public double PlaybackDurationSeconds => Math.Max(0, _audio.MusicDuration.TotalSeconds);

    public string PlaybackTimeText => $"{FormatPlaybackTime(_audio.MusicPosition)} / {FormatPlaybackTime(_audio.MusicDuration)}";

    public string EffectsPlaybackStatus => _audio.ActiveEffectCount switch
    {
        0 => Ui.SfxIdle,
        1 => Ui.OneSfxActive,
        var count => Ui.SfxActive(count)
    };

    public string DuckingStatus => _audio.ActiveEffectCount > 0 ? Ui.DuckingActive : Ui.DuckingOff;

    public string DataDirectory => _storage.DataDirectory;

    public string ErrorLogDirectory => _errorLog.LogDirectory;

    public string AppVersion
    {
        get
        {
            var assembly = typeof(MainWindowViewModel).Assembly;
            var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            return (informational ?? assembly.GetName().Version?.ToString() ?? "development").Split('+')[0];
        }
    }

    public string AppVersionText => $"{Ui.Version} {AppVersion}";

    public string AppAuthorName => "MWell";

    public bool IsSettingsVisible
    {
        get => _isSettingsVisible;
        private set => SetProperty(ref _isSettingsVisible, value);
    }

    public int SelectedSettingsTabIndex
    {
        get => _selectedSettingsTabIndex;
        set => SetProperty(ref _selectedSettingsTabIndex, value);
    }

    public string BackgroundImagePath => string.IsNullOrWhiteSpace(_state.Theme.Background.ImageOriginalPath)
        ? Ui.NoBackgroundImageSelected
        : _state.Theme.Background.ImageOriginalPath;

    public string BackgroundImageStatus => _state.Theme.Background.Mode == BackgroundMode.Image
        ? Ui.ImageBackground
        : Ui.ThemePresetBackground;

    public bool HasBackgroundImage => _state.Theme.Background.Mode == BackgroundMode.Image
        && !string.IsNullOrWhiteSpace(_state.Theme.Background.ImageOriginalPath);

    public AppUiStrings Ui => AppUiStrings.For(_state.Preferences.Language);

    private FileDialogLabels DialogLabels => new(
        Ui.AudioFilesTypeName,
        Ui.ImageFilesTypeName,
        Ui.ProfileZipTypeName,
        Ui.ChooseAudioFilesTitle,
        Ui.ChooseAudioFolderTitle,
        Ui.ChooseBackgroundImage,
        Ui.ExportProfileTitle,
        Ui.RestoreProfileTitle);

    public AppLanguageOption? SelectedLanguage
    {
        get => LanguageOptions.FirstOrDefault(option => option.Language == _state.Preferences.Language)
            ?? LanguageOptions[0];
        set
        {
            if (value is null || _state.Preferences.Language == value.Language)
            {
                return;
            }

            _state.Preferences.Language = value.Language;
            OnPropertyChanged();
            NotifyLanguageChanged();
            Save();
        }
    }

    public double PanelOpacity
    {
        get => ThemeRenderer.Resolve(_state.Theme).PanelOpacity;
        set
        {
            var clamped = Clamp(value, 0.55, 0.98);
            if (Math.Abs(_state.Theme.Chrome.PanelOpacity - clamped) < 0.0001)
            {
                return;
            }

            MarkThemeAsCustom();
            _state.Theme.Chrome.PanelOpacity = clamped;
            OnPropertyChanged();
            UpdateThemeBrushes();
            SaveDebounced();
        }
    }

    public double AccentIntensity
    {
        get => ThemeRenderer.Sanitize(_state.Theme).Chrome.AccentIntensity;
        set
        {
            var clamped = ThemeColor.ClampUnit(value);
            if (Math.Abs(_state.Theme.Chrome.AccentIntensity - clamped) < 0.0001)
            {
                return;
            }

            MarkThemeAsCustom();
            _state.Theme.Chrome.AccentIntensity = clamped;
            OnPropertyChanged();
            UpdateThemeBrushes();
            SaveDebounced();
        }
    }

    public double ChromeCornerRadius
    {
        get => ThemeRenderer.Resolve(_state.Theme).CornerRadius;
        set
        {
            var clamped = Clamp(value, 8, 24);
            if (Math.Abs(_state.Theme.Chrome.CornerRadius - clamped) < 0.0001)
            {
                return;
            }

            MarkThemeAsCustom();
            _state.Theme.Chrome.CornerRadius = clamped;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PanelCornerRadius));
            UpdateThemeBrushes();
            SaveDebounced();
        }
    }

    public CornerRadius PanelCornerRadius => new(ChromeCornerRadius);

    public InterfaceDensity InterfaceDensity
    {
        get => ThemeRenderer.Resolve(_state.Theme).Density;
        set
        {
            if (_state.Theme.Chrome.Density == value)
            {
                return;
            }

            MarkThemeAsCustom();
            _state.Theme.Chrome.Density = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedInterfaceDensityOption));
            NotifyLayoutDensityChanged();
            Save();
        }
    }

    public InterfaceDensityOption? SelectedInterfaceDensityOption
    {
        get => InterfaceDensityOptions.FirstOrDefault(option => option.Value == InterfaceDensity);
        set
        {
            if (value is not null)
            {
                InterfaceDensity = value.Value;
            }
        }
    }

    private LayoutDensityMetrics DensityMetrics => MetricsForDensity(InterfaceDensity);

    public Thickness PanelPadding => new(DensityMetrics.PanelPadding);

    public Thickness TrackTilePadding => DensityMetrics.TrackTilePadding;

    public double TrackTileMinHeight => DensityMetrics.TrackTileMinHeight;

    public double TrackTileSpacing => DensityMetrics.TrackTileSpacing;

    public GridLength SidebarMusicSectionGridLength => new(PlayerPreferences.NormalizeSectionHeight(_state.Preferences.SidebarMusicSectionHeight, 220));

    public GridLength CenterMusicSectionGridLength => new(PlayerPreferences.NormalizeSectionHeight(_state.Preferences.CenterMusicSectionHeight, 260));

    public Thickness TransportBarPadding => new(DensityMetrics.TransportBarPadding);

    public Thickness TransportPanelPadding => DensityMetrics.TransportPanelPadding;

    public double TransportMinHeight => DensityMetrics.TransportMinHeight;

    public BackgroundLayoutMode BackgroundLayoutMode
    {
        get => _state.Theme.Background.LayoutMode;
        set
        {
            if (_state.Theme.Background.LayoutMode == value)
            {
                return;
            }

            MarkThemeAsCustom();
            _state.Theme.Background.LayoutMode = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedBackgroundLayoutModeOption));
            UpdateThemeBrushes();
            Save();
        }
    }

    public BackgroundLayoutModeOption? SelectedBackgroundLayoutModeOption
    {
        get => BackgroundLayoutModeOptions.FirstOrDefault(option => option.Value == BackgroundLayoutMode);
        set
        {
            if (value is not null)
            {
                BackgroundLayoutMode = value.Value;
            }
        }
    }

    public double BackgroundOpacity
    {
        get => _state.Theme.Background.Opacity;
        set
        {
            var clamped = ThemeColor.ClampUnit(value);
            if (Math.Abs(_state.Theme.Background.Opacity - clamped) < 0.0001)
            {
                return;
            }

            MarkThemeAsCustom();
            _state.Theme.Background.Opacity = clamped;
            OnPropertyChanged();
            UpdateThemeBrushes();
            SaveDebounced();
        }
    }

    public double BackgroundDimOverlay
    {
        get => _state.Theme.Background.DimOverlay;
        set
        {
            var clamped = Clamp(value, 0.15, 0.65);
            if (Math.Abs(_state.Theme.Background.DimOverlay - clamped) < 0.0001)
            {
                return;
            }

            MarkThemeAsCustom();
            _state.Theme.Background.DimOverlay = clamped;
            OnPropertyChanged();
            UpdateThemeBrushes();
            SaveDebounced();
        }
    }

    public double BackgroundBlurRadius
    {
        get => _state.Theme.Background.BlurRadius;
        set
        {
            var clamped = Clamp(value, 0, 24);
            if (Math.Abs(_state.Theme.Background.BlurRadius - clamped) < 0.0001)
            {
                return;
            }

            MarkThemeAsCustom();
            _state.Theme.Background.BlurRadius = clamped;
            OnPropertyChanged();
            UpdateThemeBrushes();
            SaveDebounced();
        }
    }

    public string SelectedMusicHotkeyText => SelectedMusicPlaylist is null || SelectedMusicTrack is null
        ? Ui.SelectTrack
        : HotkeyTextFor(HotkeyAction.PlayMusicTrack(SelectedMusicPlaylist.Id, SelectedMusicTrack.Id));

    public string SelectedEffectHotkeyText => SelectedEffectPlaylist is null || SelectedEffectTrack is null
        ? Ui.SelectTrack
        : HotkeyTextFor(HotkeyAction.PlayEffect(SelectedEffectPlaylist.Id, SelectedEffectTrack.Id));

    public string SelectedMusicTrackFileStatus => TrackFileStatus(SelectedMusicTrack);

    public string SelectedEffectTrackFileStatus => TrackFileStatus(SelectedEffectTrack);

    public int MusicColumns
    {
        get => _state.Preferences.MusicColumns;
        set
        {
            var normalized = PlayerPreferences.NormalizeColumns(value);
            if (_state.Preferences.MusicColumns == normalized)
            {
                return;
            }

            _state.Preferences.MusicColumns = normalized;
            OnPropertyChanged();
            OnPropertyChanged(nameof(MusicTileWidth));
            Save();
        }
    }

    public int EffectsColumns
    {
        get => _state.Preferences.EffectsColumns;
        set
        {
            var normalized = PlayerPreferences.NormalizeColumns(value);
            if (_state.Preferences.EffectsColumns == normalized)
            {
                return;
            }

            _state.Preferences.EffectsColumns = normalized;
            OnPropertyChanged();
            OnPropertyChanged(nameof(EffectTileWidth));
            Save();
        }
    }

    public double MusicTileWidth => TileWidthForColumns(MusicColumns);

    public double EffectTileWidth => TileWidthForColumns(EffectsColumns);

    public IReadOnlyList<HotkeyDisplayRow> SystemHotkeyRows => HotkeyAction.SystemActions
        .Select(action => new HotkeyDisplayRow(action, SystemActionName(action), HotkeyTextFor(action)))
        .ToList();

    public IReadOnlyList<HotkeyDisplayRow> AssignedTrackHotkeyRows => _state.Hotkeys.Bindings
        .Where(binding => !binding.Action.IsSystemAction)
        .Select(binding => new HotkeyDisplayRow(binding.Action, HotkeyActionName(binding.Action), binding.Hotkey.DisplayText))
        .OrderBy(row => row.Name, StringComparer.CurrentCultureIgnoreCase)
        .ToList();

    public string AssignedTrackHotkeySummary => Ui.AssignedTrackHotkeySummary(AssignedTrackHotkeyRows.Count);

    public bool IsPlaying
    {
        get => _isPlaying;
        private set
        {
            if (SetProperty(ref _isPlaying, value))
            {
                OnPropertyChanged(nameof(PlaybackStatus));
                OnPropertyChanged(nameof(PlayPauseButtonText));
                OnPropertyChanged(nameof(PlayPauseIconData));
                OnPropertyChanged(nameof(CurrentTrackTitle));
                OnPropertyChanged(nameof(MusicTrackTiles));
                OnPropertyChanged(nameof(SelectedMusicTrackTile));
                StopAllCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(StatusMessage));
                OnPropertyChanged(nameof(HasStatusMessage));
                OnPropertyChanged(nameof(IsStatusError));
                OnPropertyChanged(nameof(StatusMessageBrush));
                ScheduleStatusMessageDismiss();
            }
        }
    }

    public string LastImportMessage
    {
        get => _lastImportMessage;
        private set
        {
            if (SetProperty(ref _lastImportMessage, value))
            {
                OnPropertyChanged(nameof(StatusMessage));
                OnPropertyChanged(nameof(HasStatusMessage));
                ScheduleStatusMessageDismiss();
            }
        }
    }

    public string StatusMessage => ErrorMessage ?? LastImportMessage;

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    public bool IsStatusError => ErrorMessage is not null;

    public IBrush StatusMessageBrush => IsStatusError ? DangerBrush : TextSecondaryBrush;

    public bool IsImportConflictVisible => _importConflictSummary is not null;

    public string ImportConflictTitle => _importConflictSummary is null ? "" : Ui.ImportConflictTitle;

    public string ImportConflictMessage => _importConflictSummary is null
        ? ""
        : Ui.ImportConflictMessage(
            _importConflictSummary.AddedCount,
            _importConflictSummary.AttemptedCount,
            _importConflictSummary.Target,
            _importConflictSummary.DuplicateCount);

    public string ImportConflictDuplicateText => _importConflictSummary is null
        ? ""
        : string.Join(", ", _importConflictSummary.DuplicateTitles);

    public bool IsHotkeyConflictVisible => _pendingHotkeyConflict is not null;

    public string HotkeyConflictTitle => _pendingHotkeyConflict is null ? "" : Ui.HotkeyConflictTitle;

    public string HotkeyConflictMessage => _pendingHotkeyConflict is null
        ? ""
        : Ui.HotkeyConflictMessage(
            _pendingHotkeyConflict.Hotkey.DisplayText,
            HotkeyActionName(_pendingHotkeyConflict.ExistingAction),
            HotkeyActionName(_pendingHotkeyConflict.ReplacementAction));

    public bool IsDeleteConfirmationVisible => HasPendingDelete;

    public string DeleteConfirmationTitle => _pendingPlaylistDelete?.Role switch
    {
        TrackRole.Music => Ui.DeleteMusicPlaylistTitle,
        TrackRole.Effect => Ui.DeleteSfxPlaylistTitle,
        _ => DeleteTrackConfirmationTitle
    };

    public string DeleteConfirmationMessage => _pendingPlaylistDelete is not null
        ? Ui.DeletePlaylistMessage(_pendingPlaylistDelete.PlaylistName)
        : DeleteTrackConfirmationMessage;

    private bool HasPendingDelete => _pendingTrackDelete is not null || _pendingPlaylistDelete is not null;

    private string DeleteTrackConfirmationMessage => _pendingTrackDelete is null
        ? ""
        : _pendingTrackDelete.TrackCount == 1
            ? Ui.DeleteTrackMessage(_pendingTrackDelete.TrackTitle)
            : Ui.DeleteTracksMessage(_pendingTrackDelete.TrackCount);

    private string DeleteTrackConfirmationTitle => _pendingTrackDelete?.Role switch
    {
        TrackRole.Music when _pendingTrackDelete.TrackCount > 1 => Ui.DeleteMusicTracksTitle(_pendingTrackDelete.TrackCount),
        TrackRole.Music => Ui.DeleteMusicTrackTitle,
        TrackRole.Effect when _pendingTrackDelete.TrackCount > 1 => Ui.DeleteSfxTracksTitle(_pendingTrackDelete.TrackCount),
        TrackRole.Effect => Ui.DeleteSfxTrackTitle,
        _ => ""
    };

    public bool IsPlaylistRenameVisible => _pendingPlaylistRename is not null;

    public string PlaylistRenameTitle => _pendingPlaylistRename?.Role switch
    {
        TrackRole.Music => Ui.RenameMusicPlaylist,
        TrackRole.Effect => Ui.RenameSfxPlaylist,
        _ => ""
    };

    public string PlaylistRenameName
    {
        get => _playlistRenameName;
        set
        {
            if (SetProperty(ref _playlistRenameName, value))
            {
                ConfirmPlaylistRenameCommand?.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsTrackRenameVisible => _pendingTrackRename is not null;

    public string TrackRenameTitle => _pendingTrackRename?.Role switch
    {
        TrackRole.Music => Ui.RenameMusicTrack,
        TrackRole.Effect => Ui.RenameSfxTrack,
        _ => ""
    };

    public string TrackRenameName
    {
        get => _trackRenameName;
        set
        {
            if (SetProperty(ref _trackRenameName, value))
            {
                ConfirmTrackRenameCommand?.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsMusicDropTargetActive
    {
        get => _isMusicDropTargetActive;
        private set => SetProperty(ref _isMusicDropTargetActive, value);
    }

    public bool IsEffectDropTargetActive
    {
        get => _isEffectDropTargetActive;
        private set => SetProperty(ref _isEffectDropTargetActive, value);
    }

    public double MusicVolume
    {
        get => _state.Preferences.Volume;
        set
        {
            var clamped = PlayerPreferences.ClampUnit(value);
            if (Math.Abs(_state.Preferences.Volume - clamped) < 0.0001)
            {
                return;
            }

            _state.Preferences.Volume = clamped;
            OnPropertyChanged();
            OnPropertyChanged(nameof(MusicVolumePercentText));
            ApplyMusicVolume();
            SaveDebounced();
        }
    }

    public string MusicVolumePercentText => $"{MusicVolume * 100:0}%";

    public double EffectsVolume
    {
        get => _state.Preferences.EffectsVolume;
        set
        {
            var clamped = PlayerPreferences.ClampUnit(value);
            if (Math.Abs(_state.Preferences.EffectsVolume - clamped) < 0.0001)
            {
                return;
            }

            _state.Preferences.EffectsVolume = clamped;
            OnPropertyChanged();
            OnPropertyChanged(nameof(EffectsVolumePercentText));
            _audio.SetEffectsVolume(clamped);
            SaveDebounced();
        }
    }

    public string EffectsVolumePercentText => $"{EffectsVolume * 100:0}%";

    public double DuckingAmount
    {
        get => _state.Preferences.DuckingAmount;
        set
        {
            var clamped = PlayerPreferences.ClampDucking(value);
            if (Math.Abs(_state.Preferences.DuckingAmount - clamped) < 0.0001)
            {
                return;
            }

            _state.Preferences.DuckingAmount = clamped;
            OnPropertyChanged();
            ApplyMusicVolume();
            SaveDebounced();
        }
    }

    public bool ShuffleEnabled
    {
        get => _state.Preferences.ShuffleEnabled;
        set
        {
            if (_state.Preferences.ShuffleEnabled == value)
            {
                return;
            }

            _state.Preferences.ShuffleEnabled = value;
            _playbackQueue.Reset(_playbackMusicPlaylist?.Id);
            OnPropertyChanged();
            Save();
        }
    }

    public bool MusicFadeOutOnPauseEnabled
    {
        get => _state.Preferences.MusicFadeOutOnPauseEnabled;
        set
        {
            if (_state.Preferences.MusicFadeOutOnPauseEnabled == value)
            {
                return;
            }

            _state.Preferences.MusicFadeOutOnPauseEnabled = value;
            OnPropertyChanged();
            Save();
        }
    }

    public RepeatMode RepeatMode
    {
        get => _state.Preferences.RepeatMode;
        set
        {
            if (_state.Preferences.RepeatMode == value)
            {
                return;
            }

            _state.Preferences.RepeatMode = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedRepeatModeOption));
            Save();
        }
    }

    public RepeatModeOption? SelectedRepeatModeOption
    {
        get => RepeatModeOptions.FirstOrDefault(option => option.Value == RepeatMode);
        set
        {
            if (value is not null)
            {
                RepeatMode = value.Value;
            }
        }
    }

    public HotkeyAction? CaptureAction
    {
        get => _captureAction;
        private set
        {
            if (SetProperty(ref _captureAction, value))
            {
                OnPropertyChanged(nameof(CaptureStatus));
                OnPropertyChanged(nameof(IsCapturingHotkey));
                NotifyCommandStates();
            }
        }
    }

    public string CaptureStatus => CaptureAction is null
        ? Ui.HotkeysActiveStatus
        : Ui.PressKeyToAssign;

    public bool IsCapturingHotkey => CaptureAction is not null;

    public string CustomThemePresetName
    {
        get => _customThemePresetName;
        set
        {
            if (SetProperty(ref _customThemePresetName, value))
            {
                SaveCurrentThemeAsCustomPresetCommand?.NotifyCanExecuteChanged();
            }
        }
    }

    public ThemePresetOption? SelectedThemePreset
    {
        get => _selectedThemePreset;
        set
        {
            if (value is null || !SetProperty(ref _selectedThemePreset, value))
            {
                return;
            }

            ApplyTheme(ThemeRenderer.ThemeFor(value.Preset), preserveBackground: true);
        }
    }

    public IBrush BackgroundBrush
    {
        get => _backgroundBrush;
        private set => SetProperty(ref _backgroundBrush, value);
    }

    public IBrush BackgroundOverlayBrush
    {
        get => _backgroundOverlayBrush;
        private set => SetProperty(ref _backgroundOverlayBrush, value);
    }

    public IBrush PanelBrush
    {
        get => _panelBrush;
        private set => SetProperty(ref _panelBrush, value);
    }

    public IBrush PanelAltBrush
    {
        get => _panelAltBrush;
        private set => SetProperty(ref _panelAltBrush, value);
    }

    public IBrush CardBrush
    {
        get => _cardBrush;
        private set => SetProperty(ref _cardBrush, value);
    }

    public IBrush CurrentCardBrush
    {
        get => _currentCardBrush;
        private set => SetProperty(ref _currentCardBrush, value);
    }

    public IBrush AccentBrush
    {
        get => _accentBrush;
        private set => SetProperty(ref _accentBrush, value);
    }

    public IBrush DangerBrush
    {
        get => _dangerBrush;
        private set => SetProperty(ref _dangerBrush, value);
    }

    public IBrush TextPrimaryBrush
    {
        get => _textPrimaryBrush;
        private set => SetProperty(ref _textPrimaryBrush, value);
    }

    public IBrush TextSecondaryBrush
    {
        get => _textSecondaryBrush;
        private set => SetProperty(ref _textSecondaryBrush, value);
    }

    public IBrush PanelAltTextPrimaryBrush
    {
        get => _panelAltTextPrimaryBrush;
        private set => SetProperty(ref _panelAltTextPrimaryBrush, value);
    }

    public IBrush PanelAltTextSecondaryBrush
    {
        get => _panelAltTextSecondaryBrush;
        private set => SetProperty(ref _panelAltTextSecondaryBrush, value);
    }

    public IBrush CardTextPrimaryBrush
    {
        get => _cardTextPrimaryBrush;
        private set => SetProperty(ref _cardTextPrimaryBrush, value);
    }

    public IBrush CardTextSecondaryBrush
    {
        get => _cardTextSecondaryBrush;
        private set => SetProperty(ref _cardTextSecondaryBrush, value);
    }

    public IBrush CurrentCardTextPrimaryBrush
    {
        get => _currentCardTextPrimaryBrush;
        private set => SetProperty(ref _currentCardTextPrimaryBrush, value);
    }

    public IBrush CurrentCardTextSecondaryBrush
    {
        get => _currentCardTextSecondaryBrush;
        private set => SetProperty(ref _currentCardTextSecondaryBrush, value);
    }

    public IBrush AccentTextPrimaryBrush
    {
        get => _accentTextPrimaryBrush;
        private set => SetProperty(ref _accentTextPrimaryBrush, value);
    }

    public IBrush AccentTextSecondaryBrush
    {
        get => _accentTextSecondaryBrush;
        private set => SetProperty(ref _accentTextSecondaryBrush, value);
    }

    public IBrush DividerBrush
    {
        get => _dividerBrush;
        private set => SetProperty(ref _dividerBrush, value);
    }

    public void ImportDroppedMusic(IEnumerable<string> paths)
    {
        ClearDropTargets();
        ImportTracks(paths, TrackRole.Music, SelectedMusicPlaylist, null);
    }

    public void SaveLayoutSplitHeights(double sidebarMusicHeight, double centerMusicHeight)
    {
        _state.Preferences.SidebarMusicSectionHeight = PlayerPreferences.NormalizeSectionHeight(sidebarMusicHeight, 220);
        _state.Preferences.CenterMusicSectionHeight = PlayerPreferences.NormalizeSectionHeight(centerMusicHeight, 260);
        Save();
    }

    public void ImportDroppedMusic(IEnumerable<string> paths, Playlist? targetPlaylist)
    {
        ClearDropTargets();
        ImportTracks(paths, TrackRole.Music, targetPlaylist, null);
    }

    public void ImportDroppedEffects(IEnumerable<string> paths)
    {
        ClearDropTargets();
        ImportTracks(paths, TrackRole.Effect, null, SelectedEffectPlaylist);
    }

    public void ImportDroppedEffects(IEnumerable<string> paths, EffectPlaylist? targetPlaylist)
    {
        ClearDropTargets();
        ImportTracks(paths, TrackRole.Effect, null, targetPlaylist);
    }

    public async Task ImportDroppedMusicAsync(IEnumerable<string> paths, Playlist? targetPlaylist = null)
    {
        ClearDropTargets();
        await ImportTracksAsync(paths, TrackRole.Music, targetPlaylist ?? SelectedMusicPlaylist, null);
    }

    public async Task ImportDroppedEffectsAsync(IEnumerable<string> paths, EffectPlaylist? targetPlaylist = null)
    {
        ClearDropTargets();
        await ImportTracksAsync(paths, TrackRole.Effect, null, targetPlaylist ?? SelectedEffectPlaylist);
    }

    public void MoveMusicPlaylistToTarget(Playlist? draggedPlaylist, Playlist? targetPlaylist)
    {
        var dragged = draggedPlaylist is null
            ? null
            : MusicPlaylists.FirstOrDefault(candidate => candidate.Id == draggedPlaylist.Id);
        var target = targetPlaylist is null
            ? null
            : MusicPlaylists.FirstOrDefault(candidate => candidate.Id == targetPlaylist.Id);

        MovePlaylistToTarget(MusicPlaylists, dragged, target);
    }

    public void MoveEffectPlaylistToTarget(EffectPlaylist? draggedPlaylist, EffectPlaylist? targetPlaylist)
    {
        var dragged = draggedPlaylist is null
            ? null
            : EffectPlaylists.FirstOrDefault(candidate => candidate.Id == draggedPlaylist.Id);
        var target = targetPlaylist is null
            ? null
            : EffectPlaylists.FirstOrDefault(candidate => candidate.Id == targetPlaylist.Id);

        MovePlaylistToTarget(EffectPlaylists, dragged, target);
    }

    public void MoveMusicTrackToTarget(Track? draggedTrack, Track? targetTrack)
    {
        if (SelectedMusicPlaylist is null || draggedTrack is null || targetTrack is null)
        {
            return;
        }

        TransferMusicTracks(
            SelectedMusicPlaylist.Id,
            SelectedMusicPlaylist.Id,
            MusicTrackIdsForDrag(draggedTrack),
            targetTrack?.Id,
            TrackTransferMode.Move);
    }

    public void MoveEffectTrackToTarget(Track? draggedTrack, Track? targetTrack)
    {
        if (SelectedEffectPlaylist is null || draggedTrack is null || targetTrack is null)
        {
            return;
        }

        TransferEffectTracks(
            SelectedEffectPlaylist.Id,
            SelectedEffectPlaylist.Id,
            EffectTrackIdsForDrag(draggedTrack),
            targetTrack?.Id,
            TrackTransferMode.Move);
    }

    public IReadOnlyList<Guid> MusicTrackIdsForDrag(Track? track)
    {
        if (SelectedMusicPlaylist is null || track is null)
        {
            return [];
        }

        return _selectedMusicTrackIds.Contains(track.Id)
            ? SelectedMusicPlaylist.Tracks.Where(item => _selectedMusicTrackIds.Contains(item.Id)).Select(item => item.Id).ToArray()
            : [track.Id];
    }

    public IReadOnlyList<Guid> EffectTrackIdsForDrag(Track? track)
    {
        if (SelectedEffectPlaylist is null || track is null)
        {
            return [];
        }

        return _selectedEffectTrackIds.Contains(track.Id)
            ? SelectedEffectPlaylist.Effects.Where(item => _selectedEffectTrackIds.Contains(item.Id)).Select(item => item.Id).ToArray()
            : [track.Id];
    }

    public bool TransferMusicTracks(
        Guid sourcePlaylistId,
        Guid destinationPlaylistId,
        IReadOnlyCollection<Guid> trackIds,
        Guid? targetTrackId,
        TrackTransferMode mode)
    {
        var source = MusicPlaylists.FirstOrDefault(playlist => playlist.Id == sourcePlaylistId);
        var destination = MusicPlaylists.FirstOrDefault(playlist => playlist.Id == destinationPlaylistId);
        if (source is null || destination is null)
        {
            return false;
        }

        var result = LibraryTransferService.TransferTracks(
            source.Tracks,
            destination.Tracks,
            trackIds,
            targetTrackId,
            mode);
        if (!result.Changed)
        {
            return false;
        }

        if (mode == TrackTransferMode.Move && sourcePlaylistId != destinationPlaylistId)
        {
            _state.Hotkeys.MoveTrackBindings(
                TrackRole.Music,
                sourcePlaylistId,
                destinationPlaylistId,
                trackIds.ToHashSet());
            if (_playbackMusicPlaylist?.Id == sourcePlaylistId
                && CurrentTrack is not null
                && trackIds.Contains(CurrentTrack.Id))
            {
                SetPlaybackMusicPlaylist(destination);
            }
        }

        if (sourcePlaylistId == destinationPlaylistId)
        {
            SetSelectedMusicTrackIds(_selectedMusicTrackIds);
        }
        else if (SelectedMusicPlaylist?.Id == destinationPlaylistId)
        {
            SetSelectedMusicTrackIds(result.ResultTrackIds);
            SelectedMusicTrack = destination.Tracks.FirstOrDefault(track => result.ResultTrackIds.Contains(track.Id));
        }
        else if (SelectedMusicPlaylist?.Id == sourcePlaylistId)
        {
            SetSelectedMusicTrackIds(_selectedMusicTrackIds.Where(id => !trackIds.Contains(id)));
            SelectedMusicTrack = source.Tracks.FirstOrDefault();
        }

        NotifyMusicTracksChanged();
        NotifyHotkeysChanged();
        Save();
        return true;
    }

    public bool TransferEffectTracks(
        Guid sourcePlaylistId,
        Guid destinationPlaylistId,
        IReadOnlyCollection<Guid> trackIds,
        Guid? targetTrackId,
        TrackTransferMode mode)
    {
        var source = EffectPlaylists.FirstOrDefault(playlist => playlist.Id == sourcePlaylistId);
        var destination = EffectPlaylists.FirstOrDefault(playlist => playlist.Id == destinationPlaylistId);
        if (source is null || destination is null)
        {
            return false;
        }

        var result = LibraryTransferService.TransferTracks(
            source.Effects,
            destination.Effects,
            trackIds,
            targetTrackId,
            mode);
        if (!result.Changed)
        {
            return false;
        }

        if (mode == TrackTransferMode.Move && sourcePlaylistId != destinationPlaylistId)
        {
            _state.Hotkeys.MoveTrackBindings(
                TrackRole.Effect,
                sourcePlaylistId,
                destinationPlaylistId,
                trackIds.ToHashSet());
        }

        if (sourcePlaylistId == destinationPlaylistId)
        {
            SetSelectedEffectTrackIds(_selectedEffectTrackIds);
        }
        else if (SelectedEffectPlaylist?.Id == destinationPlaylistId)
        {
            SetSelectedEffectTrackIds(result.ResultTrackIds);
            SelectedEffectTrack = destination.Effects.FirstOrDefault(track => result.ResultTrackIds.Contains(track.Id));
        }
        else if (SelectedEffectPlaylist?.Id == sourcePlaylistId)
        {
            SetSelectedEffectTrackIds(_selectedEffectTrackIds.Where(id => !trackIds.Contains(id)));
            SelectedEffectTrack = source.Effects.FirstOrDefault();
        }

        NotifyEffectTracksChanged();
        NotifyHotkeysChanged();
        Save();
        return true;
    }

    public void SetMusicDropTargetActive(bool isActive)
    {
        if (isActive)
        {
            IsEffectDropTargetActive = false;
        }

        IsMusicDropTargetActive = isActive;
    }

    public void SetEffectDropTargetActive(bool isActive)
    {
        if (isActive)
        {
            IsMusicDropTargetActive = false;
        }

        IsEffectDropTargetActive = isActive;
    }

    public void ClearDropTargets()
    {
        IsMusicDropTargetActive = false;
        IsEffectDropTargetActive = false;
    }

    public bool HandleDialogHotkey(Hotkey hotkey)
    {
        if (hotkey.KeyCode == HotkeyConfiguration.EscapeKeyCode)
        {
            if (IsDeleteConfirmationVisible)
            {
                CancelDelete();
                return true;
            }

            if (IsPlaylistRenameVisible)
            {
                CancelPlaylistRename();
                return true;
            }

            if (IsTrackRenameVisible)
            {
                CancelTrackRename();
                return true;
            }

            if (IsImportConflictVisible)
            {
                DismissImportConflict();
                return true;
            }

            if (IsHotkeyConflictVisible)
            {
                CancelHotkeyConflict();
                return true;
            }

            if (IsSettingsVisible)
            {
                CloseSettings();
                return true;
            }
        }

        if (hotkey.KeyCode == HotkeyConfiguration.ReturnKeyCode)
        {
            if (ConfirmHotkeyConflictCommand.CanExecute(null))
            {
                ConfirmHotkeyConflictCommand.Execute(null);
                return true;
            }

            if (DismissImportConflictCommand.CanExecute(null))
            {
                DismissImportConflictCommand.Execute(null);
                return true;
            }

            if (ConfirmDeleteCommand.CanExecute(null))
            {
                ConfirmDeleteCommand.Execute(null);
                return true;
            }

            if (ConfirmPlaylistRenameCommand.CanExecute(null))
            {
                ConfirmPlaylistRenameCommand.Execute(null);
                return true;
            }

            if (ConfirmTrackRenameCommand.CanExecute(null))
            {
                ConfirmTrackRenameCommand.Execute(null);
                return true;
            }
        }

        return IsDeleteConfirmationVisible || IsPlaylistRenameVisible || IsTrackRenameVisible || IsImportConflictVisible || IsHotkeyConflictVisible;
    }

    public bool HandleHotkey(Hotkey hotkey)
    {
        if (hotkey.KeyCode == HotkeyConfiguration.EscapeKeyCode && CaptureAction is not null)
        {
            CancelHotkeyCapture();
            return true;
        }

        if (CaptureAction is not null)
        {
            var capturedAction = CaptureAction;
            CaptureAction = null;
            var conflict = _state.Hotkeys.Assign(hotkey, capturedAction, resolvingConflicts: false);
            if (conflict is not null)
            {
                SetPendingHotkeyConflict(new PendingHotkeyConflict(conflict.ExistingAction, capturedAction, hotkey));
                return true;
            }

            NotifyHotkeysChanged();
            Save();
            return true;
        }

        var action = _state.Hotkeys.ActionFor(hotkey);
        if (action is null)
        {
            return false;
        }

        ExecuteHotkeyAction(action);
        return true;
    }

    public void RefreshPlaybackProgress()
    {
        OnPropertyChanged(nameof(PlaybackPositionSeconds));
        OnPropertyChanged(nameof(PlaybackDurationSeconds));
        OnPropertyChanged(nameof(PlaybackTimeText));
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _statusMessageTimer.Stop();
        if (_saveDebounceTimer.IsEnabled)
        {
            _saveDebounceTimer.Stop();
            Save();
        }
        _importCancellation?.Cancel();
        _importCancellation?.Dispose();
        _playbackProgressTimer.Stop();
        _audio.MusicFinished -= _musicFinishedHandler;
        _audio.EffectPlaybackCountChanged -= _effectPlaybackCountChangedHandler;
        _audio.Dispose();
    }

    private async Task AddMusicFilesAsync()
    {
        if (FileDialogService is null)
        {
            return;
        }

        await ImportTracksAsync(await FileDialogService.OpenAudioFilesAsync(DialogLabels), TrackRole.Music);
    }

    private async Task AddMusicFolderAsync()
    {
        if (FileDialogService is null)
        {
            return;
        }

        await ImportTracksAsync(await FileDialogService.OpenAudioFolderAsync(DialogLabels), TrackRole.Music);
    }

    private async Task AddEffectFilesAsync()
    {
        if (FileDialogService is null)
        {
            return;
        }

        await ImportTracksAsync(await FileDialogService.OpenAudioFilesAsync(DialogLabels), TrackRole.Effect);
    }

    private async Task AddEffectFolderAsync()
    {
        if (FileDialogService is null)
        {
            return;
        }

        await ImportTracksAsync(await FileDialogService.OpenAudioFolderAsync(DialogLabels), TrackRole.Effect);
    }

    private async Task ChooseBackgroundImageAsync()
    {
        if (FileDialogService is null)
        {
            return;
        }

        var path = (await FileDialogService.OpenBackgroundImageAsync(DialogLabels)).FirstOrDefault();
        if (path is null)
        {
            return;
        }

        try
        {
            var bitmap = await Task.Run(() => BackgroundImageProcessor.LoadBitmap(path, _state.Theme.Background.BlurRadius));
            MarkThemeAsCustom();
            _state.Theme.Background.Mode = BackgroundMode.Image;
            _state.Theme.Background.ImageOriginalPath = path;
            UpdateThemeBrushes(bitmap);
            Save();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            ReportError("background.load", Ui.FailedToLoadBackgroundImage(path), ex);
        }
    }

    private async Task ExportProfileAsync()
    {
        if (FileDialogService is null)
        {
            return;
        }

        var path = await FileDialogService.SaveProfileZipAsync(DialogLabels);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            Save();
            var appVersion = typeof(MainWindowViewModel).Assembly.GetName().Version?.ToString();
            var result = _profileTransfer.Export(_storage.DataDirectory, path, appVersion);
            ErrorMessage = null;
            LastImportMessage = Ui.ProfileExported(result.ZipPath);
        }
        catch (Exception ex) when (IsProfileTransferException(ex))
        {
            ReportError("profile.export", Ui.ProfileTransferFailed(ex.Message), ex);
        }
    }

    private async Task RestoreProfileAsync()
    {
        if (FileDialogService is null)
        {
            return;
        }

        var path = await FileDialogService.OpenProfileZipAsync(DialogLabels);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            var result = _profileTransfer.Restore(_storage.DataDirectory, path);
            StopAll();
            ReloadStateFromStorage();
            ErrorMessage = null;
            LastImportMessage = Ui.ProfileRestored(result.BackupDirectory);
        }
        catch (Exception ex) when (IsProfileTransferException(ex))
        {
            ReportError("profile.restore", Ui.ProfileTransferFailed(ex.Message), ex);
        }
    }

    private void ClearBackgroundImage()
    {
        MarkThemeAsCustom();
        _state.Theme.Background.Mode = BackgroundMode.None;
        _state.Theme.Background.ImageBookmarkData = null;
        _state.Theme.Background.ImageOriginalPath = null;
        _state.Theme.Background.BlurRadius = 0;
        UpdateThemeBrushes();
        Save();
    }

    private void ResetTheme()
    {
        ApplyTheme(ThemeRenderer.DefaultTheme, preserveBackground: true);
    }

    private void RestoreVisualDefaults()
    {
        ApplyTheme(ThemeRenderer.DefaultTheme, preserveBackground: false);
    }

    private bool CanSaveCurrentThemeAsCustomPreset()
    {
        return !string.IsNullOrWhiteSpace(CustomThemePresetName);
    }

    private void SaveCurrentThemeAsCustomPreset()
    {
        var name = CustomThemePresetName.Trim();
        if (name.Length == 0)
        {
            return;
        }

        var theme = _state.Theme.Clone();
        theme.Preset = null;
        _state.CustomThemePresets.Add(new CustomThemePreset
        {
            Name = name,
            Theme = theme
        });
        CustomThemePresetName = "";
        NotifyCustomThemePresetsChanged();
        Save();
    }

    private void ApplyCustomThemePreset(CustomThemePresetOption? option)
    {
        if (option is null)
        {
            return;
        }

        var preset = _state.CustomThemePresets.FirstOrDefault(candidate => candidate.Id == option.Id);
        if (preset is null)
        {
            return;
        }

        ApplyTheme(preset.Theme, preserveBackground: false);
    }

    private void DeleteCustomThemePreset(CustomThemePresetOption? option)
    {
        if (option is null)
        {
            return;
        }

        var removed = _state.CustomThemePresets.RemoveAll(candidate => candidate.Id == option.Id);
        if (removed == 0)
        {
            return;
        }

        NotifyCustomThemePresetsChanged();
        Save();
    }

    private void ApplyTheme(AppTheme theme, bool preserveBackground)
    {
        var currentBackground = preserveBackground
            ? CloneBackground(_state.Theme.Background)
            : null;

        _state.Theme = theme.Clone();
        if (currentBackground is not null)
        {
            _state.Theme.Background = currentBackground;
        }

        _selectedThemePreset = ThemePresetOptionFor(_state.Theme.Preset);
        OnPropertyChanged(nameof(SelectedThemePreset));
        UpdateThemeBrushes();
        Save();
    }

    private void MarkThemeAsCustom()
    {
        if (_state.Theme.Preset is null)
        {
            return;
        }

        _state.Theme.Preset = null;
        _selectedThemePreset = null;
        OnPropertyChanged(nameof(SelectedThemePreset));
    }

    private ThemePresetOption? ThemePresetOptionFor(ThemePreset? preset)
    {
        return preset is null
            ? null
            : ThemePresetOptions.FirstOrDefault(option => option.Preset == preset);
    }

    private IReadOnlyList<ThemePaletteColorOption> CreateThemePaletteColorOptions()
    {
        return
        [
            new ThemePaletteColorOption(() => Ui.PaletteBackgroundTop, () => _state.Theme.Palette.BackgroundTop, ApplyPaletteColorChange),
            new ThemePaletteColorOption(() => Ui.PaletteBackgroundBottom, () => _state.Theme.Palette.BackgroundBottom, ApplyPaletteColorChange),
            new ThemePaletteColorOption(() => Ui.PalettePanel, () => _state.Theme.Palette.SurfacePrimary, ApplyPaletteColorChange),
            new ThemePaletteColorOption(() => Ui.PalettePanelAlt, () => _state.Theme.Palette.SurfaceSecondary, ApplyPaletteColorChange),
            new ThemePaletteColorOption(() => Ui.PaletteCard, () => _state.Theme.Palette.Card, ApplyPaletteColorChange),
            new ThemePaletteColorOption(() => Ui.PaletteCurrentCard, () => _state.Theme.Palette.CardCurrent, ApplyPaletteColorChange),
            new ThemePaletteColorOption(() => Ui.PaletteAccentName, () => _state.Theme.Palette.Accent, ApplyPaletteColorChange),
            new ThemePaletteColorOption(() => Ui.PaletteTextPrimary, () => _state.Theme.Palette.TextPrimary, ApplyPaletteColorChange),
            new ThemePaletteColorOption(() => Ui.PaletteTextSecondary, () => _state.Theme.Palette.TextSecondary, ApplyPaletteColorChange),
            new ThemePaletteColorOption(() => Ui.PaletteDanger, () => _state.Theme.Palette.Danger, ApplyPaletteColorChange)
        ];
    }

    private void ApplyPaletteColorChange()
    {
        MarkThemeAsCustom();
        UpdateThemeBrushes();
        Save();
    }

    private void NotifyCustomThemePresetsChanged()
    {
        OnPropertyChanged(nameof(CustomThemePresetOptions));
        OnPropertyChanged(nameof(HasCustomThemePresets));
        OnPropertyChanged(nameof(CustomThemePresetSummary));
    }

    private void NotifyLanguageChanged()
    {
        OnPropertyChanged(nameof(Ui));
        OnPropertyChanged(nameof(AppVersionText));
        OnPropertyChanged(nameof(SelectedLanguage));
        NotifyThemePresetOptionsChanged();
        NotifyThemePaletteColorsChanged();
        OnPropertyChanged(nameof(InterfaceDensityOptions));
        OnPropertyChanged(nameof(SelectedInterfaceDensityOption));
        OnPropertyChanged(nameof(RepeatModeOptions));
        OnPropertyChanged(nameof(SelectedRepeatModeOption));
        OnPropertyChanged(nameof(BackgroundLayoutModeOptions));
        OnPropertyChanged(nameof(SelectedBackgroundLayoutModeOption));
        OnPropertyChanged(nameof(CaptureStatus));
        OnPropertyChanged(nameof(CustomThemePresetSummary));
        OnPropertyChanged(nameof(BackgroundImagePath));
        OnPropertyChanged(nameof(BackgroundImageStatus));
        OnPropertyChanged(nameof(MusicTrackCountText));
        OnPropertyChanged(nameof(EffectTrackCountText));
        OnPropertyChanged(nameof(HasMusicSearchText));
        OnPropertyChanged(nameof(HasEffectSearchText));
        OnPropertyChanged(nameof(HasVisibleMusicTrackTiles));
        OnPropertyChanged(nameof(HasVisibleEffectTrackTiles));
        OnPropertyChanged(nameof(IsMusicSearchEmpty));
        OnPropertyChanged(nameof(IsEffectSearchEmpty));
        OnPropertyChanged(nameof(MusicSelectionSummary));
        OnPropertyChanged(nameof(EffectSelectionSummary));
        OnPropertyChanged(nameof(MusicDeleteSelectionToolTip));
        OnPropertyChanged(nameof(EffectDeleteSelectionToolTip));
        OnPropertyChanged(nameof(SelectedMusicTrackDisplayTitle));
        OnPropertyChanged(nameof(SelectedEffectTrackDisplayTitle));
        OnPropertyChanged(nameof(SelectedMusicTrackFileStatus));
        OnPropertyChanged(nameof(SelectedEffectTrackFileStatus));
        OnPropertyChanged(nameof(CurrentTrackTitle));
        OnPropertyChanged(nameof(CurrentPlaybackPlaylistName));
        OnPropertyChanged(nameof(PlaybackStatus));
        OnPropertyChanged(nameof(PlayPauseButtonText));
        OnPropertyChanged(nameof(EffectsPlaybackStatus));
        OnPropertyChanged(nameof(DuckingStatus));
        OnPropertyChanged(nameof(ImportConflictTitle));
        OnPropertyChanged(nameof(ImportConflictMessage));
        OnPropertyChanged(nameof(HotkeyConflictTitle));
        OnPropertyChanged(nameof(HotkeyConflictMessage));
        OnPropertyChanged(nameof(DeleteConfirmationTitle));
        OnPropertyChanged(nameof(DeleteConfirmationMessage));
        OnPropertyChanged(nameof(PlaylistRenameTitle));
        OnPropertyChanged(nameof(TrackRenameTitle));
        NotifyHotkeysChanged();
    }

    private void NotifyThemePresetOptionsChanged()
    {
        foreach (var option in ThemePresetOptions)
        {
            option.Refresh();
        }

        OnPropertyChanged(nameof(ThemePresetOptions));
        OnPropertyChanged(nameof(SelectedThemePreset));
    }

    private void NotifyThemePaletteColorsChanged()
    {
        foreach (var option in ThemePaletteColorOptions)
        {
            option.Refresh();
        }

        OnPropertyChanged(nameof(ThemePaletteColorOptions));
    }

    private void OpenDataDirectory()
    {
        try
        {
            Directory.CreateDirectory(_storage.DataDirectory);
            _externalLauncher.OpenFolder(_storage.DataDirectory);
            ErrorMessage = null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            ReportError("storage.open", Ui.CouldNotOpenDataFolder(ex.Message), ex);
        }
    }

    private void OpenAuthorPage()
    {
        try
        {
            _externalLauncher.OpenUrl("https://boosty.to/mwell");
            ErrorMessage = null;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            ReportError("author.open", Ui.CouldNotOpenAuthorPage(ex.Message), ex);
        }
    }

    private void OpenSettings()
    {
        SelectedSettingsTabIndex = 0;
        IsSettingsVisible = true;
    }

    private void CloseSettings()
    {
        CancelHotkeyCapture();
        CancelHotkeyConflict();
        IsSettingsVisible = false;
    }

    private void OpenErrorLogDirectory()
    {
        try
        {
            Directory.CreateDirectory(_errorLog.LogDirectory);
            _externalLauncher.OpenFolder(_errorLog.LogDirectory);
            ErrorMessage = null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            ReportError("log.open", Ui.CouldNotOpenDataFolder(ex.Message), ex);
        }
    }

    private void OpenTrackLocation(Track? track)
    {
        if (track is null)
        {
            return;
        }

        try
        {
            if (File.Exists(track.Path))
            {
                _externalLauncher.RevealFile(track.Path);
                ErrorMessage = null;
                return;
            }

            var directory = Path.GetDirectoryName(track.Path);
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                _externalLauncher.OpenFolder(directory);
            }

            ErrorMessage = Ui.AudioFileIsMissing(track.Path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            ReportError("track.reveal", Ui.CouldNotOpenFileLocation(ex.Message), ex);
        }
    }

    private void RemoveMissingMusicFiles()
    {
        if (SelectedMusicPlaylist is null)
        {
            return;
        }

        var selectedPlaylist = SelectedMusicPlaylist;
        var currentTrackRemoved = CurrentTrack is not null
            && selectedPlaylist.Tracks.Any(track => track.Id == CurrentTrack.Id && !File.Exists(track.Path));
        var removed = _state.RemoveMissingMusicTracks(selectedPlaylist.Id, File.Exists);
        if (removed == 0)
        {
            ErrorMessage = null;
            LastImportMessage = Ui.NoMissingFilesToRemove;
            return;
        }

        if (currentTrackRemoved)
        {
            StopAll();
            CurrentTrack = selectedPlaylist.Tracks.FirstOrDefault();
        }

        if (SelectedMusicTrack is not null && !selectedPlaylist.Tracks.Any(track => track.Id == SelectedMusicTrack.Id))
        {
            SelectedMusicTrack = selectedPlaylist.Tracks.FirstOrDefault();
        }

        SetSelectedMusicTrackIds(_selectedMusicTrackIds.Where(trackId => selectedPlaylist.Tracks.Any(track => track.Id == trackId)));
        NotifyMusicTracksChanged();
        NotifyHotkeysChanged();
        Save();
        ErrorMessage = null;
        LastImportMessage = Ui.RemovedMissingFiles(removed);
        NotifyCommandStates();
    }

    private void RemoveMissingEffectFiles()
    {
        if (SelectedEffectPlaylist is null)
        {
            return;
        }

        var selectedPlaylist = SelectedEffectPlaylist;
        var removed = _state.RemoveMissingEffectTracks(selectedPlaylist.Id, File.Exists);
        if (removed == 0)
        {
            ErrorMessage = null;
            LastImportMessage = Ui.NoMissingFilesToRemove;
            return;
        }

        if (SelectedEffectTrack is not null && !selectedPlaylist.Effects.Any(track => track.Id == SelectedEffectTrack.Id))
        {
            SelectedEffectTrack = selectedPlaylist.Effects.FirstOrDefault();
        }

        SetSelectedEffectTrackIds(_selectedEffectTrackIds.Where(trackId => selectedPlaylist.Effects.Any(track => track.Id == trackId)));
        NotifyEffectTracksChanged();
        NotifyHotkeysChanged();
        Save();
        ErrorMessage = null;
        LastImportMessage = Ui.RemovedMissingFiles(removed);
        NotifyCommandStates();
    }

    private void ImportTracks(
        IEnumerable<string> paths,
        TrackRole role,
        Playlist? musicTarget = null,
        EffectPlaylist? effectTarget = null)
    {
        var inputPaths = paths.ToArray();
        if (inputPaths.Length == 0)
        {
            return;
        }

        ErrorMessage = null;
        if (role == TrackRole.Music)
        {
            var target = musicTarget is null
                ? SelectedMusicPlaylist
                : MusicPlaylists.FirstOrDefault(playlist => playlist.Id == musicTarget.Id);
            if (target is null)
            {
                return;
            }

            var result = _importService.BuildUniqueTracks(inputPaths, role, target.Tracks, target.Name);
            target.Tracks.AddRange(result.AddedTracks);
            if (CurrentTrack is null)
            {
                SetPlaybackMusicPlaylist(target);
                CurrentTrack = target.Tracks.FirstOrDefault();
            }

            NotifyMusicTracksChanged();
            LastImportMessage = ImportMessage(result);
            SetImportConflict(result.ConflictSummary);
        }
        else
        {
            var target = effectTarget is null
                ? SelectedEffectPlaylist
                : EffectPlaylists.FirstOrDefault(playlist => playlist.Id == effectTarget.Id);
            if (target is null)
            {
                return;
            }

            var result = _importService.BuildUniqueTracks(inputPaths, role, target.Effects, target.Name);
            target.Effects.AddRange(result.AddedTracks);
            NotifyEffectTracksChanged();
            LastImportMessage = ImportMessage(result);
            SetImportConflict(result.ConflictSummary);
        }

        Save();
    }

    private string ImportMessage(FileImportResult result)
    {
        var duplicateCount = result.ConflictSummary?.DuplicateCount ?? 0;
        if (duplicateCount == 0 && result.ScanSummary.UnsupportedFileCount == 0 && result.ScanSummary.SkippedPathCount == 0)
        {
            return result.AddedTracks.Count == 0 ? Ui.NoSupportedAudioFilesFound : Ui.AddedFiles(result.AddedTracks.Count);
        }

        return Ui.ImportSummary(
            result.AddedTracks.Count,
            duplicateCount,
            result.ScanSummary.UnsupportedFileCount,
            result.ScanSummary.SkippedPathCount);
    }

    private void DismissImportConflict()
    {
        SetImportConflict(null);
    }

    private void SetImportConflict(ImportConflictSummary? conflictSummary)
    {
        if (_importConflictSummary == conflictSummary)
        {
            return;
        }

        _importConflictSummary = conflictSummary;
        if (conflictSummary is not null)
        {
            ClearPendingDelete();
            ClearPendingPlaylistRename();
            ClearPendingTrackRename();
            ClearPendingHotkeyConflict();
        }

        NotifyImportConflictChanged();
    }

    private void NotifyImportConflictChanged()
    {
        OnPropertyChanged(nameof(IsImportConflictVisible));
        OnPropertyChanged(nameof(ImportConflictTitle));
        OnPropertyChanged(nameof(ImportConflictMessage));
        OnPropertyChanged(nameof(ImportConflictDuplicateText));
        DismissImportConflictCommand?.NotifyCanExecuteChanged();
    }

    private void ConfirmHotkeyConflict()
    {
        var pending = _pendingHotkeyConflict;
        if (pending is null)
        {
            return;
        }

        _state.Hotkeys.Assign(pending.Hotkey, pending.ReplacementAction, resolvingConflicts: true);
        SetPendingHotkeyConflict(null);
        NotifyHotkeysChanged();
        Save();
    }

    private void CancelHotkeyConflict()
    {
        ClearPendingHotkeyConflict();
    }

    private void SetPendingHotkeyConflict(PendingHotkeyConflict? pendingConflict)
    {
        if (_pendingHotkeyConflict == pendingConflict)
        {
            return;
        }

        _pendingHotkeyConflict = pendingConflict;
        if (pendingConflict is not null)
        {
            ClearPendingDelete();
            ClearPendingPlaylistRename();
            ClearPendingTrackRename();
            SetImportConflict(null);
        }

        NotifyHotkeyConflictChanged();
    }

    private void ClearPendingHotkeyConflict()
    {
        if (_pendingHotkeyConflict is null)
        {
            return;
        }

        _pendingHotkeyConflict = null;
        NotifyHotkeyConflictChanged();
    }

    private void NotifyHotkeyConflictChanged()
    {
        OnPropertyChanged(nameof(IsHotkeyConflictVisible));
        OnPropertyChanged(nameof(HotkeyConflictTitle));
        OnPropertyChanged(nameof(HotkeyConflictMessage));
        ConfirmHotkeyConflictCommand?.NotifyCanExecuteChanged();
        CancelHotkeyConflictCommand?.NotifyCanExecuteChanged();
    }

    private void CreateMusicPlaylist()
    {
        var playlist = new Playlist(NextPlaylistName(Ui.NewMusicPlaylistPrefix, MusicPlaylists.Select(item => item.Name)));
        MusicPlaylists.Add(playlist);
        SelectedMusicPlaylist = playlist;
        Save();
        NotifyCommandStates();
    }

    private void CreateEffectPlaylist()
    {
        var playlist = new EffectPlaylist(NextPlaylistName(Ui.NewSfxPlaylistPrefix, EffectPlaylists.Select(item => item.Name)));
        EffectPlaylists.Add(playlist);
        SelectedEffectPlaylist = playlist;
        Save();
        NotifyCommandStates();
    }

    private void DeleteSelectedMusicPlaylist()
    {
        DeleteMusicPlaylist(SelectedMusicPlaylist);
    }

    private void DeleteSelectedEffectPlaylist()
    {
        DeleteEffectPlaylist(SelectedEffectPlaylist);
    }

    private void DeleteMusicPlaylist(Playlist? playlist)
    {
        if (playlist is null || MusicPlaylists.Count <= 1)
        {
            return;
        }

        var removed = MusicPlaylists.FirstOrDefault(candidate => candidate.Id == playlist.Id);
        if (removed is null)
        {
            return;
        }

        var wasSelected = SelectedMusicPlaylist?.Id == removed.Id;
        var wasPlaybackPlaylist = _playbackMusicPlaylist?.Id == removed.Id;
        MusicPlaylists.Remove(removed);
        if (wasSelected)
        {
            SelectedMusicPlaylist = MusicPlaylists.FirstOrDefault();
        }

        if (wasPlaybackPlaylist)
        {
            StopAll();
            SetPlaybackMusicPlaylist(SelectedMusicPlaylist);
            CurrentTrack = SelectedMusicPlaylist?.Tracks.FirstOrDefault();
        }

        ClearPendingPlaylistDeleteIfMatches(TrackRole.Music, removed.Id);
        Save();
        NotifyCommandStates();
    }

    private void DeleteEffectPlaylist(EffectPlaylist? playlist)
    {
        if (playlist is null || EffectPlaylists.Count <= 1)
        {
            return;
        }

        var removed = EffectPlaylists.FirstOrDefault(candidate => candidate.Id == playlist.Id);
        if (removed is null)
        {
            return;
        }

        var wasSelected = SelectedEffectPlaylist?.Id == removed.Id;
        EffectPlaylists.Remove(removed);
        if (wasSelected)
        {
            SelectedEffectPlaylist = EffectPlaylists.FirstOrDefault();
        }

        ClearPendingPlaylistDeleteIfMatches(TrackRole.Effect, removed.Id);
        Save();
        NotifyCommandStates();
    }

    private void DeleteSelectedMusicTrack()
    {
        DeleteMusicTrack(SelectedMusicTrack);
    }

    private void DeleteSelectedEffectTrack()
    {
        DeleteEffectTrack(SelectedEffectTrack);
    }

    private void DeleteMusicTrack(Track? track)
    {
        DeleteMusicTrackFromPlaylist(SelectedMusicPlaylist, track);
    }

    private void DeleteMusicTrackFromPlaylist(Playlist? playlist, Track? track)
    {
        if (playlist is null || track is null)
        {
            return;
        }

        var removed = playlist.Tracks.FirstOrDefault(candidate => candidate.Id == track.Id);
        if (removed is null)
        {
            return;
        }

        playlist.Tracks.Remove(removed);
        if (CurrentTrack?.Id == removed.Id)
        {
            StopAll();
            CurrentTrack = playlist.Tracks.FirstOrDefault();
        }

        if (SelectedMusicPlaylist?.Id == playlist.Id && SelectedMusicTrack?.Id == removed.Id)
        {
            SelectedMusicTrack = playlist.Tracks.FirstOrDefault();
        }

        if (SelectedMusicPlaylist?.Id == playlist.Id)
        {
            NotifyMusicTracksChanged();
        }

        SetSelectedMusicTrackIds(_selectedMusicTrackIds.Where(trackId => trackId != removed.Id));
        ClearPendingDeleteIfMatches(TrackRole.Music, playlist.Id, removed.Id);
        Save();
        NotifyCommandStates();
    }

    private void DeleteMusicTracksFromPlaylist(Playlist? playlist, IReadOnlyCollection<Guid> trackIds)
    {
        if (playlist is null || trackIds.Count == 0)
        {
            return;
        }

        var requestedIds = trackIds.ToHashSet();
        var removedIds = playlist.Tracks
            .Where(track => requestedIds.Contains(track.Id))
            .Select(track => track.Id)
            .ToHashSet();
        if (removedIds.Count == 0)
        {
            return;
        }

        playlist.Tracks.RemoveAll(track => removedIds.Contains(track.Id));
        if (CurrentTrack is not null && removedIds.Contains(CurrentTrack.Id))
        {
            StopAll();
            CurrentTrack = playlist.Tracks.FirstOrDefault();
        }

        if (SelectedMusicPlaylist?.Id == playlist.Id
            && SelectedMusicTrack is not null
            && removedIds.Contains(SelectedMusicTrack.Id))
        {
            SelectedMusicTrack = playlist.Tracks.FirstOrDefault();
        }

        if (SelectedMusicPlaylist?.Id == playlist.Id)
        {
            NotifyMusicTracksChanged();
        }

        SetSelectedMusicTrackIds(_selectedMusicTrackIds.Except(removedIds));
        Save();
        NotifyCommandStates();
    }

    private void DeleteEffectTrack(Track? track)
    {
        DeleteEffectTrackFromPlaylist(SelectedEffectPlaylist, track);
    }

    private void DeleteEffectTrackFromPlaylist(EffectPlaylist? playlist, Track? track)
    {
        if (playlist is null || track is null)
        {
            return;
        }

        var removed = playlist.Effects.FirstOrDefault(candidate => candidate.Id == track.Id);
        if (removed is null)
        {
            return;
        }

        playlist.Effects.Remove(removed);
        if (SelectedEffectPlaylist?.Id == playlist.Id && SelectedEffectTrack?.Id == removed.Id)
        {
            SelectedEffectTrack = playlist.Effects.FirstOrDefault();
        }

        if (SelectedEffectPlaylist?.Id == playlist.Id)
        {
            NotifyEffectTracksChanged();
        }

        SetSelectedEffectTrackIds(_selectedEffectTrackIds.Where(trackId => trackId != removed.Id));
        ClearPendingDeleteIfMatches(TrackRole.Effect, playlist.Id, removed.Id);
        Save();
        NotifyCommandStates();
    }

    private void DeleteEffectTracksFromPlaylist(EffectPlaylist? playlist, IReadOnlyCollection<Guid> trackIds)
    {
        if (playlist is null || trackIds.Count == 0)
        {
            return;
        }

        var requestedIds = trackIds.ToHashSet();
        var removedIds = playlist.Effects
            .Where(track => requestedIds.Contains(track.Id))
            .Select(track => track.Id)
            .ToHashSet();
        if (removedIds.Count == 0)
        {
            return;
        }

        playlist.Effects.RemoveAll(track => removedIds.Contains(track.Id));
        if (SelectedEffectPlaylist?.Id == playlist.Id
            && SelectedEffectTrack is not null
            && removedIds.Contains(SelectedEffectTrack.Id))
        {
            SelectedEffectTrack = playlist.Effects.FirstOrDefault();
        }

        if (SelectedEffectPlaylist?.Id == playlist.Id)
        {
            NotifyEffectTracksChanged();
        }

        SetSelectedEffectTrackIds(_selectedEffectTrackIds.Except(removedIds));
        Save();
        NotifyCommandStates();
    }

    private void RequestDeleteMusicTrack(Track? track)
    {
        if (track is null)
        {
            return;
        }

        if (_selectedMusicTrackIds.Count > 1 && _selectedMusicTrackIds.Contains(track.Id))
        {
            RequestDeleteSelectedMusicTracks();
            return;
        }

        RequestDeleteMusicTracks([track]);
    }

    private void RequestDeleteEffectTrack(Track? track)
    {
        if (track is null)
        {
            return;
        }

        if (_selectedEffectTrackIds.Count > 1 && _selectedEffectTrackIds.Contains(track.Id))
        {
            RequestDeleteSelectedEffectTracks();
            return;
        }

        RequestDeleteEffectTracks([track]);
    }

    private void RequestDeleteSelectedMusicTracks()
    {
        if (SelectedMusicPlaylist is null)
        {
            return;
        }

        IEnumerable<Track> selectedTracks = _selectedMusicTrackIds.Count == 0
            ? SelectedMusicTrack is null ? [] : [SelectedMusicTrack]
            : SelectedMusicPlaylist.Tracks
                .Where(track => _selectedMusicTrackIds.Contains(track.Id))
                .ToList();

        RequestDeleteMusicTracks(selectedTracks);
    }

    private void RequestDeleteSelectedEffectTracks()
    {
        if (SelectedEffectPlaylist is null)
        {
            return;
        }

        IEnumerable<Track> selectedTracks = _selectedEffectTrackIds.Count == 0
            ? SelectedEffectTrack is null ? [] : [SelectedEffectTrack]
            : SelectedEffectPlaylist.Effects
                .Where(track => _selectedEffectTrackIds.Contains(track.Id))
                .ToList();

        RequestDeleteEffectTracks(selectedTracks);
    }

    public void RequestDeleteMusicTracks(IEnumerable<Track>? tracks)
    {
        if (SelectedMusicPlaylist is null || tracks is null)
        {
            return;
        }

        var requestedIds = tracks.Select(track => track.Id).ToHashSet();
        var selectedTracks = SelectedMusicPlaylist.Tracks
            .Where(track => requestedIds.Contains(track.Id))
            .ToList();
        if (selectedTracks.Count == 0)
        {
            return;
        }

        SetPendingTrackDelete(new PendingTrackDelete(
            TrackRole.Music,
            SelectedMusicPlaylist.Id,
            selectedTracks.Select(track => track.Id).ToArray(),
            selectedTracks.Count == 1 ? selectedTracks[0].Title : ""));
    }

    public void RequestDeleteEffectTracks(IEnumerable<Track>? tracks)
    {
        if (SelectedEffectPlaylist is null || tracks is null)
        {
            return;
        }

        var requestedIds = tracks.Select(track => track.Id).ToHashSet();
        var selectedTracks = SelectedEffectPlaylist.Effects
            .Where(track => requestedIds.Contains(track.Id))
            .ToList();
        if (selectedTracks.Count == 0)
        {
            return;
        }

        SetPendingTrackDelete(new PendingTrackDelete(
            TrackRole.Effect,
            SelectedEffectPlaylist.Id,
            selectedTracks.Select(track => track.Id).ToArray(),
            selectedTracks.Count == 1 ? selectedTracks[0].Title : ""));
    }

    private void RequestRenameMusicTrack(Track? track)
    {
        if (SelectedMusicPlaylist is null || track is null || !SelectedMusicPlaylist.Tracks.Any(candidate => candidate.Id == track.Id))
        {
            return;
        }

        SetPendingTrackRename(new PendingTrackRename(TrackRole.Music, SelectedMusicPlaylist.Id, track.Id, track.Title));
    }

    private void RequestRenameEffectTrack(Track? track)
    {
        if (SelectedEffectPlaylist is null || track is null || !SelectedEffectPlaylist.Effects.Any(candidate => candidate.Id == track.Id))
        {
            return;
        }

        SetPendingTrackRename(new PendingTrackRename(TrackRole.Effect, SelectedEffectPlaylist.Id, track.Id, track.Title));
    }

    private void RequestDeleteMusicPlaylist(Playlist? playlist)
    {
        if (playlist is null || MusicPlaylists.Count <= 1 || !MusicPlaylists.Any(candidate => candidate.Id == playlist.Id))
        {
            return;
        }

        SetPendingPlaylistDelete(new PendingPlaylistDelete(TrackRole.Music, playlist.Id, playlist.Name));
    }

    private void RequestDeleteEffectPlaylist(EffectPlaylist? playlist)
    {
        if (playlist is null || EffectPlaylists.Count <= 1 || !EffectPlaylists.Any(candidate => candidate.Id == playlist.Id))
        {
            return;
        }

        SetPendingPlaylistDelete(new PendingPlaylistDelete(TrackRole.Effect, playlist.Id, playlist.Name));
    }

    private void RequestRenameMusicPlaylist(Playlist? playlist)
    {
        if (playlist is null || !MusicPlaylists.Any(candidate => candidate.Id == playlist.Id))
        {
            return;
        }

        SetPendingPlaylistRename(new PendingPlaylistRename(TrackRole.Music, playlist.Id, playlist.Name));
    }

    private void RequestRenameEffectPlaylist(EffectPlaylist? playlist)
    {
        if (playlist is null || !EffectPlaylists.Any(candidate => candidate.Id == playlist.Id))
        {
            return;
        }

        SetPendingPlaylistRename(new PendingPlaylistRename(TrackRole.Effect, playlist.Id, playlist.Name));
    }

    private void ConfirmPlaylistRename()
    {
        var pending = _pendingPlaylistRename;
        if (pending is null || string.IsNullOrWhiteSpace(PlaylistRenameName))
        {
            return;
        }

        if (pending.Role == TrackRole.Music)
        {
            var playlist = MusicPlaylists.FirstOrDefault(candidate => candidate.Id == pending.PlaylistId);
            if (playlist is not null)
            {
                RenamePlaylist(playlist, PlaylistRenameName, playlist.Name);
                OnPropertyChanged(nameof(MusicPlaylists));
                OnPropertyChanged(nameof(CurrentPlaybackPlaylistName));
            }
        }
        else
        {
            var playlist = EffectPlaylists.FirstOrDefault(candidate => candidate.Id == pending.PlaylistId);
            if (playlist is not null)
            {
                RenamePlaylist(playlist, PlaylistRenameName, playlist.Name);
                OnPropertyChanged(nameof(EffectPlaylists));
            }
        }

        CancelPlaylistRename();
    }

    private bool CanConfirmPlaylistRename()
    {
        return _pendingPlaylistRename is not null && !string.IsNullOrWhiteSpace(PlaylistRenameName);
    }

    private void CancelPlaylistRename()
    {
        ClearPendingPlaylistRename();
    }

    private void ConfirmTrackRename()
    {
        var pending = _pendingTrackRename;
        if (pending is null || string.IsNullOrWhiteSpace(TrackRenameName))
        {
            return;
        }

        if (pending.Role == TrackRole.Music)
        {
            var playlist = MusicPlaylists.FirstOrDefault(candidate => candidate.Id == pending.PlaylistId);
            var track = playlist?.Tracks.FirstOrDefault(candidate => candidate.Id == pending.TrackId);
            if (track is not null)
            {
                RenameTrack(track, TrackRenameName);
                if (SelectedMusicPlaylist?.Id == pending.PlaylistId)
                {
                    NotifyMusicTracksChanged();
                }

                if (SelectedMusicTrack?.Id == pending.TrackId)
                {
                    OnPropertyChanged(nameof(SelectedMusicTrackTitle));
                    OnPropertyChanged(nameof(SelectedMusicTrackDisplayTitle));
                }

                if (CurrentTrack?.Id == pending.TrackId)
                {
                    OnPropertyChanged(nameof(CurrentTrackTitle));
                }
            }
        }
        else
        {
            var playlist = EffectPlaylists.FirstOrDefault(candidate => candidate.Id == pending.PlaylistId);
            var track = playlist?.Effects.FirstOrDefault(candidate => candidate.Id == pending.TrackId);
            if (track is not null)
            {
                RenameTrack(track, TrackRenameName);
                if (SelectedEffectPlaylist?.Id == pending.PlaylistId)
                {
                    NotifyEffectTracksChanged();
                }

                if (SelectedEffectTrack?.Id == pending.TrackId)
                {
                    OnPropertyChanged(nameof(SelectedEffectTrackTitle));
                    OnPropertyChanged(nameof(SelectedEffectTrackDisplayTitle));
                }
            }
        }

        CancelTrackRename();
    }

    private bool CanConfirmTrackRename()
    {
        return _pendingTrackRename is not null && !string.IsNullOrWhiteSpace(TrackRenameName);
    }

    private void CancelTrackRename()
    {
        ClearPendingTrackRename();
    }

    private void ConfirmDelete()
    {
        var pendingPlaylist = _pendingPlaylistDelete;
        if (pendingPlaylist is not null)
        {
            if (pendingPlaylist.Role == TrackRole.Music)
            {
                var playlist = MusicPlaylists.FirstOrDefault(candidate => candidate.Id == pendingPlaylist.PlaylistId);
                DeleteMusicPlaylist(playlist);
            }
            else
            {
                var playlist = EffectPlaylists.FirstOrDefault(candidate => candidate.Id == pendingPlaylist.PlaylistId);
                DeleteEffectPlaylist(playlist);
            }

            CancelDelete();
            return;
        }

        var pending = _pendingTrackDelete;
        if (pending is null)
        {
            return;
        }

        if (pending.Role == TrackRole.Music)
        {
            var playlist = MusicPlaylists.FirstOrDefault(candidate => candidate.Id == pending.PlaylistId);
            if (pending.TrackCount == 1)
            {
                var track = playlist?.Tracks.FirstOrDefault(candidate => candidate.Id == pending.TrackId);
                DeleteMusicTrackFromPlaylist(playlist, track);
            }
            else
            {
                DeleteMusicTracksFromPlaylist(playlist, pending.TrackIds);
            }
        }
        else
        {
            var playlist = EffectPlaylists.FirstOrDefault(candidate => candidate.Id == pending.PlaylistId);
            if (pending.TrackCount == 1)
            {
                var track = playlist?.Effects.FirstOrDefault(candidate => candidate.Id == pending.TrackId);
                DeleteEffectTrackFromPlaylist(playlist, track);
            }
            else
            {
                DeleteEffectTracksFromPlaylist(playlist, pending.TrackIds);
            }
        }

        CancelDelete();
    }

    private void CancelDelete()
    {
        ClearPendingDelete();
    }

    private void SetPendingTrackDelete(PendingTrackDelete? pendingDelete)
    {
        _pendingTrackDelete = pendingDelete;
        if (pendingDelete is not null)
        {
            _pendingPlaylistDelete = null;
            ClearPendingPlaylistRename();
            ClearPendingTrackRename();
            ClearPendingHotkeyConflict();
        }

        NotifyDeleteConfirmationChanged();
    }

    private void SetPendingPlaylistDelete(PendingPlaylistDelete? pendingDelete)
    {
        _pendingPlaylistDelete = pendingDelete;
        if (pendingDelete is not null)
        {
            _pendingTrackDelete = null;
            ClearPendingPlaylistRename();
            ClearPendingTrackRename();
            ClearPendingHotkeyConflict();
        }

        NotifyDeleteConfirmationChanged();
    }

    private void SetPendingPlaylistRename(PendingPlaylistRename? pendingRename)
    {
        _pendingPlaylistRename = pendingRename;
        _playlistRenameName = pendingRename?.PlaylistName ?? "";
        if (pendingRename is not null)
        {
            ClearPendingDelete();
            ClearPendingTrackRename();
            ClearPendingHotkeyConflict();
        }

        NotifyPlaylistRenameChanged();
    }

    private void SetPendingTrackRename(PendingTrackRename? pendingRename)
    {
        _pendingTrackRename = pendingRename;
        _trackRenameName = pendingRename?.TrackTitle ?? "";
        if (pendingRename is not null)
        {
            ClearPendingDelete();
            ClearPendingPlaylistRename();
            ClearPendingHotkeyConflict();
        }

        NotifyTrackRenameChanged();
    }

    private void ClearPendingDelete()
    {
        if (!HasPendingDelete)
        {
            return;
        }

        _pendingTrackDelete = null;
        _pendingPlaylistDelete = null;
        NotifyDeleteConfirmationChanged();
    }

    private void ClearPendingPlaylistRename()
    {
        if (_pendingPlaylistRename is null && string.IsNullOrEmpty(_playlistRenameName))
        {
            return;
        }

        _pendingPlaylistRename = null;
        _playlistRenameName = "";
        NotifyPlaylistRenameChanged();
    }

    private void ClearPendingTrackRename()
    {
        if (_pendingTrackRename is null && string.IsNullOrEmpty(_trackRenameName))
        {
            return;
        }

        _pendingTrackRename = null;
        _trackRenameName = "";
        NotifyTrackRenameChanged();
    }

    private void NotifyDeleteConfirmationChanged()
    {
        OnPropertyChanged(nameof(IsDeleteConfirmationVisible));
        OnPropertyChanged(nameof(DeleteConfirmationTitle));
        OnPropertyChanged(nameof(DeleteConfirmationMessage));
        ConfirmDeleteCommand?.NotifyCanExecuteChanged();
        CancelDeleteCommand?.NotifyCanExecuteChanged();
    }

    private void NotifyPlaylistRenameChanged()
    {
        OnPropertyChanged(nameof(IsPlaylistRenameVisible));
        OnPropertyChanged(nameof(PlaylistRenameTitle));
        OnPropertyChanged(nameof(PlaylistRenameName));
        ConfirmPlaylistRenameCommand?.NotifyCanExecuteChanged();
        CancelPlaylistRenameCommand?.NotifyCanExecuteChanged();
    }

    private void NotifyTrackRenameChanged()
    {
        OnPropertyChanged(nameof(IsTrackRenameVisible));
        OnPropertyChanged(nameof(TrackRenameTitle));
        OnPropertyChanged(nameof(TrackRenameName));
        ConfirmTrackRenameCommand?.NotifyCanExecuteChanged();
        CancelTrackRenameCommand?.NotifyCanExecuteChanged();
    }

    private void ClearPendingDeleteIfMatches(TrackRole role, Guid playlistId, Guid trackId)
    {
        if (_pendingTrackDelete?.Role == role &&
            _pendingTrackDelete.PlaylistId == playlistId &&
            _pendingTrackDelete.TrackId == trackId)
        {
            SetPendingTrackDelete(null);
        }
    }

    private void ClearPendingPlaylistDeleteIfMatches(TrackRole role, Guid playlistId)
    {
        if (_pendingPlaylistDelete?.Role == role &&
            _pendingPlaylistDelete.PlaylistId == playlistId)
        {
            SetPendingPlaylistDelete(null);
        }
    }

    private void MoveSelectedPlaylist<T>(ObservableCollection<T> collection, T? selected, int delta)
        where T : class
    {
        MovePlaylistItem(collection, selected, delta);
    }

    private void MoveMusicPlaylistItem(Playlist? playlist, int delta)
    {
        var target = playlist is null
            ? null
            : MusicPlaylists.FirstOrDefault(candidate => candidate.Id == playlist.Id);
        MovePlaylistItem(MusicPlaylists, target, delta);
    }

    private void MoveEffectPlaylistItem(EffectPlaylist? playlist, int delta)
    {
        var target = playlist is null
            ? null
            : EffectPlaylists.FirstOrDefault(candidate => candidate.Id == playlist.Id);
        MovePlaylistItem(EffectPlaylists, target, delta);
    }

    private void MovePlaylistItem<T>(ObservableCollection<T> collection, T? item, int delta)
        where T : class
    {
        if (item is null)
        {
            return;
        }

        var index = collection.IndexOf(item);
        var targetIndex = index + delta;
        if (index < 0 || targetIndex < 0 || targetIndex >= collection.Count)
        {
            return;
        }

        collection.Move(index, targetIndex);
        Save();
        NotifyCommandStates();
    }

    private void MovePlaylistToTarget<T>(ObservableCollection<T> collection, T? draggedItem, T? targetItem)
        where T : class
    {
        if (draggedItem is null || targetItem is null || ReferenceEquals(draggedItem, targetItem))
        {
            return;
        }

        var sourceIndex = collection.IndexOf(draggedItem);
        var targetIndex = collection.IndexOf(targetItem);
        if (sourceIndex < 0 || targetIndex < 0)
        {
            return;
        }

        var item = collection[sourceIndex];
        collection.RemoveAt(sourceIndex);
        collection.Insert(Math.Min(targetIndex, collection.Count), item);
        Save();
        NotifyCommandStates();
    }

    private void MoveSelectedTrack(List<Track>? tracks, Track? selected, int delta, string propertyName)
    {
        MoveTrackItem(tracks, selected, delta, propertyName);
    }

    private void MoveTrackItem(List<Track>? tracks, Track? selected, int delta, string propertyName)
    {
        if (tracks is null || selected is null)
        {
            return;
        }

        var index = tracks.FindIndex(track => track.Id == selected.Id);
        var targetIndex = index + delta;
        if (index < 0 || targetIndex < 0 || targetIndex >= tracks.Count)
        {
            return;
        }

        tracks.RemoveAt(index);
        tracks.Insert(targetIndex, selected);
        NotifyTracksChanged(propertyName);
        Save();
        NotifyCommandStates();
    }

    private void MoveTrackToTarget(List<Track>? tracks, Track? draggedTrack, Track? targetTrack, string propertyName)
    {
        if (tracks is null || draggedTrack is null || targetTrack is null)
        {
            return;
        }

        if (!Track.MoveTrack(tracks, draggedTrack.Id, targetTrack.Id))
        {
            return;
        }

        NotifyTracksChanged(propertyName);
        Save();
        NotifyCommandStates();
    }

    private void NotifyTracksChanged(string propertyName)
    {
        if (propertyName == nameof(MusicTracks))
        {
            NotifyMusicTracksChanged();
        }
        else if (propertyName == nameof(EffectTracks))
        {
            NotifyEffectTracksChanged();
        }
        else
        {
            OnPropertyChanged(propertyName);
        }
    }

    private void SetMusicTrackItemVolume(Track? track, double volume)
    {
        if (SelectedMusicPlaylist is null || track is null)
        {
            return;
        }

        var target = SelectedMusicPlaylist.Tracks.FirstOrDefault(candidate => candidate.Id == track.Id);
        if (target is null)
        {
            return;
        }

        target.VolumeMultiplier = volume;
        if (CurrentTrack?.Id == target.Id)
        {
            ApplyMusicVolume();
        }

        Save();
        if (SelectedMusicTrack?.Id == target.Id)
        {
            OnPropertyChanged(nameof(SelectedMusicTrackVolume));
        }

        NotifyMusicTracksChanged();
    }

    private void SetEffectTrackItemVolume(Track? track, double volume)
    {
        if (SelectedEffectPlaylist is null || track is null)
        {
            return;
        }

        var target = SelectedEffectPlaylist.Effects.FirstOrDefault(candidate => candidate.Id == track.Id);
        if (target is null)
        {
            return;
        }

        target.VolumeMultiplier = volume;
        Save();
        if (SelectedEffectTrack?.Id == target.Id)
        {
            OnPropertyChanged(nameof(SelectedEffectTrackVolume));
        }

        NotifyEffectTracksChanged();
    }

    private static bool CanMove<T>(IList<T>? collection, T? selected, int delta)
        where T : class
    {
        if (collection is null || selected is null)
        {
            return false;
        }

        var index = collection.IndexOf(selected);
        var targetIndex = index + delta;
        return index >= 0 && targetIndex >= 0 && targetIndex < collection.Count;
    }

    private void PlayMusicTrack(Playlist? playlist, Track? track, bool rememberCurrentTrack = true)
    {
        if (playlist is null || track is null)
        {
            return;
        }

        try
        {
            var isSamePlaybackPlaylist = _playbackMusicPlaylist?.Id == playlist.Id;
            var previousTrackId = isSamePlaybackPlaylist ? CurrentTrack?.Id : null;
            var outputVolume = MusicOutputVolumeFor(track);
            _audio.PlayMusic(track, outputVolume);
            if (!isSamePlaybackPlaylist)
            {
                _playbackQueue.Reset(playlist.Id);
            }
            else if (rememberCurrentTrack && previousTrackId is not null && previousTrackId != track.Id)
            {
                _playbackQueue.RecordDirectTransition(
                    playlist.Id,
                    playlist.Tracks.Select(item => item.Id).ToArray(),
                    previousTrackId,
                    track.Id);
            }

            SetPlaybackMusicPlaylist(playlist);
            CurrentTrack = track;
            RefreshPlaybackProgress();
            SetMusicPaused(false);
            IsPlaying = true;
            ErrorMessage = null;
        }
        catch (FileNotFoundException ex)
        {
            SetMusicPaused(false);
            IsPlaying = _audio.IsMusicPlaying;
            ReportError("audio.music.missing", Ui.AudioFileIsMissing(MissingFilePath(ex, track)), ex);
        }
        catch (AudioPlaybackException ex)
        {
            SetMusicPaused(false);
            IsPlaying = _audio.IsMusicPlaying;
            ReportError("audio.music.playback", Ui.FailedToPlayFile(track.Title, AudioPlaybackErrorMessage(ex)), ex);
        }
        catch (Exception ex)
        {
            SetMusicPaused(false);
            IsPlaying = _audio.IsMusicPlaying;
            ReportError("audio.music.unknown", Ui.FailedToPlayFile(track.Title, ex.Message), ex);
        }
    }

    private void PlayMusicPlaylistItem(Playlist? playlist)
    {
        var targetPlaylist = playlist is null
            ? null
            : MusicPlaylists.FirstOrDefault(candidate => candidate.Id == playlist.Id);
        if (targetPlaylist is null)
        {
            return;
        }

        SelectedMusicPlaylist = targetPlaylist;
        ShuffleEnabled = true;
        if (targetPlaylist.Tracks.Count == 0)
        {
            return;
        }

        var track = targetPlaylist.Tracks[_random.Next(targetPlaylist.Tracks.Count)];
        SelectedMusicTrack = track;
        PlayMusicTrack(targetPlaylist, track);
    }

    private void PlayEffectPlaylistItem(EffectPlaylist? playlist)
    {
        var targetPlaylist = playlist is null
            ? null
            : EffectPlaylists.FirstOrDefault(candidate => candidate.Id == playlist.Id);
        if (targetPlaylist is null)
        {
            return;
        }

        SelectedEffectPlaylist = targetPlaylist;
        if (targetPlaylist.Effects.Count == 0)
        {
            return;
        }

        var track = targetPlaylist.Effects[_random.Next(targetPlaylist.Effects.Count)];
        SelectedEffectTrack = track;
        PlayEffect(track);
    }

    private void PlayMusicTrackTile(TrackTileViewModel? tile)
    {
        if (SelectedMusicPlaylist is null || tile is null || !SelectedMusicPlaylist.Tracks.Any(track => track.Id == tile.Track.Id))
        {
            return;
        }

        SelectedMusicTrack = tile.Track;
        PlayMusicTrack(SelectedMusicPlaylist, tile.Track);
    }

    private void PlayEffect(Track? track)
    {
        if (track is null)
        {
            return;
        }

        try
        {
            _audio.PlayEffect(track, track.OutputVolume(EffectsVolume));
            ErrorMessage = null;
        }
        catch (FileNotFoundException ex)
        {
            ReportError("audio.sfx.missing", Ui.AudioFileIsMissing(MissingFilePath(ex, track)), ex);
        }
        catch (AudioPlaybackException ex)
        {
            ReportError("audio.sfx.playback", Ui.FailedToPlayEffect(track.Title, AudioPlaybackErrorMessage(ex)), ex);
        }
        catch (Exception ex)
        {
            ReportError("audio.sfx.unknown", Ui.FailedToPlayEffect(track.Title, ex.Message), ex);
        }
    }

    private void ReportError(string category, string userMessage, Exception exception)
    {
        ErrorMessage = userMessage;
        _errorLog.Write(category, userMessage, exception);
    }

    private void ScheduleStatusMessageDismiss()
    {
        if (_isClearingStatusMessage || !HasStatusMessage)
        {
            return;
        }

        _statusMessageTimer.Stop();
        _statusMessageTimer.Start();
    }

    private void ClearStatusMessage()
    {
        _statusMessageTimer.Stop();
        _isClearingStatusMessage = true;
        ErrorMessage = null;
        LastImportMessage = "";
        _isClearingStatusMessage = false;
    }

    private void PlayEffectTrackTile(TrackTileViewModel? tile)
    {
        if (SelectedEffectPlaylist is null || tile is null || !SelectedEffectPlaylist.Effects.Any(track => track.Id == tile.Track.Id))
        {
            return;
        }

        SelectedEffectTrack = tile.Track;
        PlayEffect(tile.Track);
    }

    private void PlayPause()
    {
        if (IsPlaying)
        {
            if (MusicFadeOutOnPauseEnabled)
            {
                _audio.FadeOutAndPauseMusic(MusicPauseFadeDuration, CurrentMusicOutputVolume());
            }
            else
            {
                _audio.PauseMusic();
            }

            SetMusicPaused(true);
            IsPlaying = false;
            return;
        }

        if (_isMusicPaused)
        {
            ApplyMusicVolume();
            _audio.ResumeMusic();
            SetMusicPaused(false);
            IsPlaying = true;
            return;
        }

        if (_audio.IsMusicPlaying)
        {
            ApplyMusicVolume();
            _audio.ResumeMusic();
            SetMusicPaused(false);
            IsPlaying = true;
            return;
        }

        var playlist = _playbackMusicPlaylist ?? SelectedMusicPlaylist;
        var track = CurrentTrack ?? playlist?.Tracks.FirstOrDefault();
        PlayMusicTrack(playlist, track, rememberCurrentTrack: false);
    }

    private void StopAll()
    {
        _audio.StopAll();
        SetMusicPaused(false);
        NotifyEffectsPlaybackStatusChanged();
        RefreshPlaybackProgress();
        IsPlaying = false;
    }

    private void StopEffects()
    {
        _audio.StopEffects();
        NotifyEffectsPlaybackStatusChanged();
    }

    private void HandleEffectPlaybackCountChanged()
    {
        ApplyMusicVolume();
        NotifyEffectsPlaybackStatusChanged();
    }

    private void RunOnViewModelThread(Action action)
    {
        if (_isDisposed)
        {
            return;
        }

        if (Dispatcher.UIThread.CheckAccess() || Environment.CurrentManagedThreadId == _ownerThreadId)
        {
            action();
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            if (!_isDisposed)
            {
                action();
            }
        });
    }

    private void SetPlaybackMusicPlaylist(Playlist? playlist)
    {
        if (_playbackMusicPlaylist?.Id == playlist?.Id)
        {
            _playbackMusicPlaylist = playlist;
            return;
        }

        _playbackMusicPlaylist = playlist;
        OnPropertyChanged(nameof(CurrentPlaybackPlaylistName));
    }

    private void SeekPlayback(double seconds)
    {
        if (!double.IsFinite(seconds))
        {
            return;
        }

        _audio.SeekMusic(TimeSpan.FromSeconds(Math.Max(0, seconds)));
        RefreshPlaybackProgress();
    }

    private void NotifyEffectsPlaybackStatusChanged()
    {
        OnPropertyChanged(nameof(EffectsPlaybackStatus));
        OnPropertyChanged(nameof(DuckingStatus));
        StopAllCommand.NotifyCanExecuteChanged();
        StopEffectsCommand.NotifyCanExecuteChanged();
    }

    private bool CanStopAll()
    {
        return IsPlaying || _isMusicPaused || _audio.IsMusicPlaying || _audio.ActiveEffectCount > 0;
    }

    private bool IsMusicTransportActive => IsPlaying || _isMusicPaused || _audio.IsMusicPlaying;

    private void SetMusicPaused(bool value)
    {
        if (_isMusicPaused == value)
        {
            return;
        }

        _isMusicPaused = value;
        OnPropertyChanged(nameof(PlayPauseButtonText));
        OnPropertyChanged(nameof(PlayPauseIconData));
        OnPropertyChanged(nameof(CurrentTrackTitle));
        OnPropertyChanged(nameof(MusicTrackTiles));
        OnPropertyChanged(nameof(SelectedMusicTrackTile));
        StopAllCommand.NotifyCanExecuteChanged();
    }

    private void NextTrackFromPlaybackEnd()
    {
        SetMusicPaused(false);
        IsPlaying = false;
        var playlist = NavigationMusicPlaylist();
        if (playlist is null)
        {
            return;
        }

        var decision = _playbackQueue.TrackFinished(
            playlist.Id,
            playlist.Tracks.Select(track => track.Id).ToArray(),
            CurrentTrack?.Id,
            ShuffleEnabled,
            RepeatMode,
            _random);
        ApplyPlaybackQueueDecision(playlist, decision);
    }

    private void NextTrack()
    {
        var playlist = NavigationMusicPlaylist();
        if (playlist is null || playlist.Tracks.Count == 0)
        {
            return;
        }

        var decision = _playbackQueue.ManualNext(
            playlist.Id,
            playlist.Tracks.Select(track => track.Id).ToArray(),
            CurrentTrack?.Id,
            ShuffleEnabled,
            _random);
        ApplyPlaybackQueueDecision(playlist, decision);
    }

    private void PreviousTrack()
    {
        var playlist = NavigationMusicPlaylist();
        if (playlist is null || playlist.Tracks.Count == 0)
        {
            return;
        }

        if (_audio.MusicPosition > TimeSpan.FromSeconds(3))
        {
            SeekPlayback(0);
            return;
        }

        var decision = _playbackQueue.ManualPrevious(
            playlist.Id,
            playlist.Tracks.Select(track => track.Id).ToArray(),
            CurrentTrack?.Id,
            ShuffleEnabled,
            _random);
        ApplyPlaybackQueueDecision(playlist, decision);
    }

    private void ApplyPlaybackQueueDecision(Playlist playlist, PlaybackQueueDecision decision)
    {
        if (decision.Action == PlaybackQueueAction.Stop)
        {
            _audio.StopMusic();
            SetMusicPaused(false);
            IsPlaying = false;
            RefreshPlaybackProgress();
            return;
        }

        if (decision.Action != PlaybackQueueAction.Play || decision.TrackId is not { } trackId)
        {
            return;
        }

        var track = playlist.Tracks.FirstOrDefault(candidate => candidate.Id == trackId);
        PlayMusicTrack(playlist, track, rememberCurrentTrack: false);
    }

    private bool CanNavigateMusic()
    {
        return NavigationMusicPlaylist()?.Tracks.Count > 0;
    }

    private Playlist? NavigationMusicPlaylist()
    {
        return CurrentTrack is null
            ? SelectedMusicPlaylist
            : _playbackMusicPlaylist ?? SelectedMusicPlaylist;
    }

    private void BeginBindSelectedMusic()
    {
        BeginBindMusicTrack(SelectedMusicTrack);
    }

    private void BeginBindSelectedEffect()
    {
        BeginBindEffectTrack(SelectedEffectTrack);
    }

    private void BeginBindMusicTrack(Track? track)
    {
        if (SelectedMusicPlaylist is null || track is null || !SelectedMusicPlaylist.Tracks.Any(candidate => candidate.Id == track.Id))
        {
            return;
        }

        CaptureAction = HotkeyAction.PlayMusicTrack(SelectedMusicPlaylist.Id, track.Id);
    }

    private void BeginBindEffectTrack(Track? track)
    {
        if (SelectedEffectPlaylist is null || track is null || !SelectedEffectPlaylist.Effects.Any(candidate => candidate.Id == track.Id))
        {
            return;
        }

        CaptureAction = HotkeyAction.PlayEffect(SelectedEffectPlaylist.Id, track.Id);
    }

    private void BeginBindSystemHotkey(HotkeyAction? action)
    {
        if (action is not null)
        {
            CaptureAction = action;
        }
    }

    private void ClearSystemHotkey(HotkeyAction? action)
    {
        if (action is null)
        {
            return;
        }

        _state.Hotkeys.Clear(action);
        NotifyHotkeysChanged();
        Save();
    }

    private bool CanClearSystemHotkey(HotkeyAction? action)
    {
        return action is not null && _state.Hotkeys.HotkeyFor(action) is not null;
    }

    private void RestoreDefaultSystemHotkey(HotkeyAction? action)
    {
        if (action is null || !action.IsSystemAction)
        {
            return;
        }

        CaptureAction = null;
        ClearPendingHotkeyConflict();
        _state.Hotkeys.RestoreDefault(action);
        NotifyHotkeysChanged();
        Save();
    }

    private async Task ImportTracksAsync(
        IEnumerable<string> paths,
        TrackRole role,
        Playlist? musicTarget = null,
        EffectPlaylist? effectTarget = null)
    {
        musicTarget ??= SelectedMusicPlaylist;
        effectTarget ??= SelectedEffectPlaylist;
        var inputPaths = paths.ToArray();
        if (inputPaths.Length == 0)
        {
            return;
        }

        var targetName = role == TrackRole.Music ? musicTarget?.Name : effectTarget?.Name;
        var existingTracks = role == TrackRole.Music ? musicTarget?.Tracks : effectTarget?.Effects;
        if (targetName is null || existingTracks is null)
        {
            return;
        }

        _importCancellation?.Cancel();
        _importCancellation?.Dispose();
        _importCancellation = new CancellationTokenSource();
        var cancellationToken = _importCancellation.Token;
        try
        {
            var result = await _importService.BuildUniqueTracksAsync(
                inputPaths,
                role,
                existingTracks.ToArray(),
                targetName,
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            ErrorMessage = null;
            if (role == TrackRole.Music && musicTarget is not null && MusicPlaylists.Any(item => item.Id == musicTarget.Id))
            {
                musicTarget.Tracks.AddRange(result.AddedTracks);
                if (CurrentTrack is null)
                {
                    SetPlaybackMusicPlaylist(musicTarget);
                    CurrentTrack = musicTarget.Tracks.FirstOrDefault();
                }

                NotifyMusicTracksChanged();
            }
            else if (role == TrackRole.Effect && effectTarget is not null && EffectPlaylists.Any(item => item.Id == effectTarget.Id))
            {
                effectTarget.Effects.AddRange(result.AddedTracks);
                NotifyEffectTracksChanged();
            }
            else
            {
                return;
            }

            LastImportMessage = ImportMessage(result);
            SetImportConflict(result.ConflictSummary);
            Save();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private void RestoreDefaultHotkeys()
    {
        CaptureAction = null;
        ClearPendingHotkeyConflict();
        _state.Hotkeys = HotkeyConfiguration.Defaults;
        NotifyHotkeysChanged();
        Save();
    }

    private void CancelHotkeyCapture()
    {
        CaptureAction = null;
    }

    private void ClearSelectedMusicBinding()
    {
        ClearMusicTrackBinding(SelectedMusicTrack);
    }

    private bool CanClearSelectedMusicBinding()
    {
        if (SelectedMusicPlaylist is null || SelectedMusicTrack is null)
        {
            return false;
        }

        return _state.Hotkeys.HotkeyFor(HotkeyAction.PlayMusicTrack(SelectedMusicPlaylist.Id, SelectedMusicTrack.Id)) is not null;
    }

    private void ClearSelectedEffectBinding()
    {
        ClearEffectTrackBinding(SelectedEffectTrack);
    }

    private bool CanClearSelectedEffectBinding()
    {
        if (SelectedEffectPlaylist is null || SelectedEffectTrack is null)
        {
            return false;
        }

        return _state.Hotkeys.HotkeyFor(HotkeyAction.PlayEffect(SelectedEffectPlaylist.Id, SelectedEffectTrack.Id)) is not null;
    }

    private void ClearMusicTrackBinding(Track? track)
    {
        if (SelectedMusicPlaylist is null || track is null || !SelectedMusicPlaylist.Tracks.Any(candidate => candidate.Id == track.Id))
        {
            return;
        }

        _state.Hotkeys.Clear(HotkeyAction.PlayMusicTrack(SelectedMusicPlaylist.Id, track.Id));
        NotifyHotkeysChanged();
        Save();
    }

    private void ClearEffectTrackBinding(Track? track)
    {
        if (SelectedEffectPlaylist is null || track is null || !SelectedEffectPlaylist.Effects.Any(candidate => candidate.Id == track.Id))
        {
            return;
        }

        _state.Hotkeys.Clear(HotkeyAction.PlayEffect(SelectedEffectPlaylist.Id, track.Id));
        NotifyHotkeysChanged();
        Save();
    }

    private void ExecuteHotkeyAction(HotkeyAction action)
    {
        switch (action.Kind)
        {
            case HotkeyActionKind.StopAll:
                StopAll();
                break;
            case HotkeyActionKind.StopEffects:
                StopEffects();
                break;
            case HotkeyActionKind.PlayPause:
                PlayPause();
                break;
            case HotkeyActionKind.MusicVolumeUp:
                MusicVolume += 0.05;
                break;
            case HotkeyActionKind.MusicVolumeDown:
                MusicVolume -= 0.05;
                break;
            case HotkeyActionKind.EffectsVolumeUp:
                EffectsVolume += 0.05;
                break;
            case HotkeyActionKind.EffectsVolumeDown:
                EffectsVolume -= 0.05;
                break;
            case HotkeyActionKind.PlayMusicTrack:
                var musicPlaylist = MusicPlaylists.FirstOrDefault(playlist => playlist.Id == action.PlaylistId);
                var musicTrack = musicPlaylist?.Tracks.FirstOrDefault(track => track.Id == action.TrackId);
                PlayMusicTrack(musicPlaylist, musicTrack);
                break;
            case HotkeyActionKind.PlayEffect:
                var effectPlaylist = EffectPlaylists.FirstOrDefault(playlist => playlist.Id == action.PlaylistId);
                var effectTrack = effectPlaylist?.Effects.FirstOrDefault(track => track.Id == action.TrackId);
                PlayEffect(effectTrack);
                break;
        }
    }

    private void ApplyMusicVolume()
    {
        _audio.SetMusicVolume(CurrentMusicOutputVolume());
    }

    private double CurrentMusicOutputVolume()
    {
        return MusicOutputVolumeFor(CurrentTrack);
    }

    private double MusicOutputVolumeFor(Track? track)
    {
        var ducking = _audio.ActiveEffectCount > 0 ? 1.0 - DuckingAmount : 1.0;
        return Track.OutputVolume(
            MusicVolume,
            track?.VolumeMultiplier ?? Track.DefaultVolumeMultiplier,
            ducking);
    }

    private void ReloadStateFromStorage()
    {
        var restoredState = _storage.Load();
        restoredState.EnsureDefaults();

        _isApplyingState = true;
        try
        {
            _state = restoredState;
            MusicPlaylists.Clear();
            foreach (var playlist in _state.MusicPlaylists)
            {
                MusicPlaylists.Add(playlist);
            }

            EffectPlaylists.Clear();
            foreach (var playlist in _state.EffectPlaylists)
            {
                EffectPlaylists.Add(playlist);
            }

            SelectedMusicPlaylist = MusicPlaylists.FirstOrDefault(playlist => playlist.Id == _state.Preferences.SelectedMusicPlaylistId)
                ?? MusicPlaylists.FirstOrDefault();
            SelectedEffectPlaylist = EffectPlaylists.FirstOrDefault(playlist => playlist.Id == _state.Preferences.SelectedEffectPlaylistId)
                ?? EffectPlaylists.FirstOrDefault();
            CurrentTrack = SelectedMusicPlaylist?.Tracks.FirstOrDefault();
            SetPlaybackMusicPlaylist(SelectedMusicPlaylist);
            _selectedThemePreset = ThemePresetOptionFor(_state.Theme.Preset);
            SetImportConflict(null);
            ClearPendingDelete();
            ClearPendingPlaylistRename();
            ClearPendingTrackRename();
            ClearPendingHotkeyConflict();
            _audio.SetEffectsVolume(EffectsVolume);
            UpdateThemeBrushes();
        }
        finally
        {
            _isApplyingState = false;
        }

        NotifyStateReloaded();
    }

    private void NotifyStateReloaded()
    {
        OnPropertyChanged(nameof(Ui));
        OnPropertyChanged(nameof(AppVersionText));
        OnPropertyChanged(nameof(SelectedThemePreset));
        OnPropertyChanged(nameof(SelectedLanguage));
        OnPropertyChanged(nameof(SelectedMusicPlaylistName));
        OnPropertyChanged(nameof(SelectedEffectPlaylistName));
        OnPropertyChanged(nameof(CurrentTrackTitle));
        OnPropertyChanged(nameof(CurrentPlaybackPlaylistName));
        OnPropertyChanged(nameof(PlaybackStatus));
        OnPropertyChanged(nameof(PlayPauseButtonText));
        OnPropertyChanged(nameof(PlayPauseIconData));
        OnPropertyChanged(nameof(BackgroundImagePath));
        OnPropertyChanged(nameof(BackgroundImageStatus));
        OnPropertyChanged(nameof(HasBackgroundImage));
        NotifyMusicTracksChanged();
        NotifyEffectTracksChanged();
        NotifyHotkeysChanged();
        NotifyCustomThemePresetsChanged();
        NotifyCommandStates();
        RefreshPlaybackProgress();
    }

    private void Save()
    {
        _state.MusicPlaylists = MusicPlaylists.ToList();
        _state.EffectPlaylists = EffectPlaylists.ToList();
        _state.EnsureDefaults();
        try
        {
            _storage.Save(_state);
        }
        catch (Exception ex) when (IsStorageWriteException(ex))
        {
            ReportError("storage.save", Ui.CouldNotSaveData(ex.Message), ex);
        }
    }

    private void SaveDebounced()
    {
        if (!_saveDebounceTimer.IsEnabled)
        {
            Save();
        }

        _saveDebounceTimer.Stop();
        _saveDebounceTimer.Start();
    }

    private void UpdateThemeBrushes(Bitmap? preparedBackground = null)
    {
        var resolved = ThemeRenderer.Resolve(_state.Theme);
        BackgroundBrush = CreateBackgroundBrush(resolved, preparedBackground);
        BackgroundOverlayBrush = CreateBackgroundOverlayBrush();
        PanelBrush = ToBrush(PanelBrushColor(resolved.Panel, resolved.PanelOpacity));
        PanelAltBrush = ToBrush(PanelBrushColor(resolved.PanelAlt, resolved.PanelOpacity));
        CardBrush = ToBrush(PanelBrushColor(resolved.Card, resolved.PanelOpacity));
        CurrentCardBrush = ToBrush(PanelBrushColor(resolved.CardCurrent, resolved.PanelOpacity));
        AccentBrush = ToBrush(resolved.Accent);
        DangerBrush = ToBrush(resolved.Danger);
        TextPrimaryBrush = ToBrush(resolved.TextPrimary);
        TextSecondaryBrush = ToBrush(resolved.TextSecondary);
        PanelAltTextPrimaryBrush = ToBrush(resolved.PanelAltTextPrimary);
        PanelAltTextSecondaryBrush = ToBrush(resolved.PanelAltTextSecondary);
        CardTextPrimaryBrush = ToBrush(resolved.CardTextPrimary);
        CardTextSecondaryBrush = ToBrush(resolved.CardTextSecondary);
        CurrentCardTextPrimaryBrush = ToBrush(resolved.CardCurrentTextPrimary);
        CurrentCardTextSecondaryBrush = ToBrush(resolved.CardCurrentTextSecondary);
        AccentTextPrimaryBrush = ToBrush(resolved.AccentTextPrimary);
        AccentTextSecondaryBrush = ToBrush(resolved.AccentTextSecondary);
        DividerBrush = ToBrush(resolved.Divider);
        OnPropertyChanged(nameof(StatusMessageBrush));
        OnPropertyChanged(nameof(PanelOpacity));
        OnPropertyChanged(nameof(AccentIntensity));
        OnPropertyChanged(nameof(ChromeCornerRadius));
        OnPropertyChanged(nameof(PanelCornerRadius));
        NotifyThemePaletteColorsChanged();
        NotifyLayoutDensityChanged();
        NotifyBackgroundChanged();
    }

    private IBrush CreateBackgroundBrush(ResolvedTheme resolved, Bitmap? preparedBackground = null)
    {
        var imagePath = _state.Theme.Background.ImageOriginalPath;
        if (_state.Theme.Background.Mode == BackgroundMode.Image &&
            !string.IsNullOrWhiteSpace(imagePath) &&
            File.Exists(imagePath))
        {
            try
            {
                var brush = new ImageBrush(preparedBackground ?? BackgroundImageProcessor.LoadBitmap(imagePath, _state.Theme.Background.BlurRadius))
                {
                    AlignmentX = AlignmentX.Center,
                    AlignmentY = AlignmentY.Center,
                    DestinationRect = _state.Theme.Background.LayoutMode == BackgroundLayoutMode.Tile
                        ? new RelativeRect(0, 0, 256, 256, RelativeUnit.Absolute)
                        : RelativeRect.Fill,
                    Stretch = StretchFor(_state.Theme.Background.LayoutMode),
                    TileMode = _state.Theme.Background.LayoutMode == BackgroundLayoutMode.Tile
                        ? TileMode.Tile
                        : TileMode.None,
                    Opacity = _state.Theme.Background.Opacity
                };
                return brush;
            }
            catch (Exception ex)
            {
                ReportError("background.load", Ui.FailedToLoadBackgroundImage(imagePath), ex);
            }
        }

        return ToBrush(resolved.BackgroundBottom);
    }

    private IBrush CreateBackgroundOverlayBrush()
    {
        if (!HasBackgroundImage)
        {
            return Brushes.Transparent;
        }

        return ToBrush(ThemeColor.Black.WithAlpha(_state.Theme.Background.DimOverlay));
    }

    private static ThemeColor PanelBrushColor(ThemeColor color, double panelOpacity)
    {
        return color.WithAlpha(ThemeColor.ClampUnit(panelOpacity));
    }

    private static Stretch StretchFor(BackgroundLayoutMode layoutMode)
    {
        return layoutMode switch
        {
            BackgroundLayoutMode.Fill => Stretch.UniformToFill,
            BackgroundLayoutMode.Fit => Stretch.Uniform,
            BackgroundLayoutMode.Center => Stretch.None,
            BackgroundLayoutMode.Tile => Stretch.None,
            _ => Stretch.UniformToFill
        };
    }

    private static LayoutDensityMetrics MetricsForDensity(InterfaceDensity density)
    {
        return density switch
        {
            InterfaceDensity.Compact => new LayoutDensityMetrics(
                PanelPadding: 10,
                TrackTilePadding: new Thickness(8, 5),
                TrackTileMinHeight: 32,
                TrackTileSpacing: 6,
                TransportBarPadding: 8,
                TransportPanelPadding: new Thickness(9, 5),
                TransportMinHeight: 42),
            InterfaceDensity.Spacious => new LayoutDensityMetrics(
                PanelPadding: 16,
                TrackTilePadding: new Thickness(12, 8),
                TrackTileMinHeight: 42,
                TrackTileSpacing: 10,
                TransportBarPadding: 10,
                TransportPanelPadding: new Thickness(12, 8),
                TransportMinHeight: 50),
            _ => new LayoutDensityMetrics(
                PanelPadding: 12,
                TrackTilePadding: new Thickness(10, 6),
                TrackTileMinHeight: 36,
                TrackTileSpacing: 8,
                TransportBarPadding: 9,
                TransportPanelPadding: new Thickness(10, 6),
                TransportMinHeight: 44)
        };
    }

    private static SolidColorBrush ToBrush(ThemeColor color)
    {
        static byte Byte(double value) => (byte)Math.Round(ThemeColor.ClampUnit(value) * 255);
        return new SolidColorBrush(Color.FromArgb(Byte(color.Alpha), Byte(color.Red), Byte(color.Green), Byte(color.Blue)));
    }

    private static BackgroundConfig CloneBackground(BackgroundConfig background)
    {
        return new BackgroundConfig
        {
            Mode = background.Mode,
            ImageBookmarkData = background.ImageBookmarkData?.ToArray(),
            ImageOriginalPath = background.ImageOriginalPath,
            LayoutMode = background.LayoutMode,
            Opacity = background.Opacity,
            DimOverlay = background.DimOverlay,
            BlurRadius = background.BlurRadius
        };
    }

    private static double Clamp(double value, double min, double max)
    {
        if (!double.IsFinite(value))
        {
            return min;
        }

        return Math.Min(Math.Max(value, min), max);
    }

    private string HotkeyTextFor(HotkeyAction action)
    {
        return _state.Hotkeys.HotkeyFor(action)?.DisplayText ?? Ui.Unassigned;
    }

    private TrackTileViewModel TrackTileFor(Track track, HotkeyAction action, bool isCurrent)
    {
        var hotkeyText = _state.Hotkeys.HotkeyFor(action)?.DisplayText;
        var isFileMissing = !File.Exists(track.Path);
        if (!_trackTileCache.TryGetValue(track.Id, out var tile) || !ReferenceEquals(tile.Track, track))
        {
            tile = new TrackTileViewModel(
                track,
                hotkeyText,
                isCurrent,
                isFileMissing,
                Ui.Missing,
                Ui.MissingFile,
                Ui.FileAvailable,
                Ui.Missing,
                SetTrackVolume,
                Ui,
                action.Kind == HotkeyActionKind.PlayEffect);
            _trackTileCache[track.Id] = tile;
            return tile;
        }

        tile.Refresh(
            hotkeyText,
            isCurrent,
            isFileMissing,
            Ui.Missing,
            Ui.MissingFile,
            Ui.FileAvailable,
            Ui.Missing,
            Ui,
            action.Kind == HotkeyActionKind.PlayEffect);
        return tile;
    }

    private void SetTrackVolume(Track track, double value)
    {
        track.VolumeMultiplier = value;
        if (CurrentTrack?.Id == track.Id)
        {
            ApplyMusicVolume();
        }

        SaveDebounced();
        OnPropertyChanged(nameof(SelectedMusicTrackVolume));
        OnPropertyChanged(nameof(SelectedEffectTrackVolume));
    }

    private static bool TrackMatchesSearch(Track track, string? searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return true;
        }

        var query = searchText.Trim();
        return track.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
            || track.Path.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatPlaybackTime(TimeSpan value)
    {
        if (value < TimeSpan.Zero)
        {
            value = TimeSpan.Zero;
        }

        var totalSeconds = (int)Math.Round(value.TotalSeconds);
        var hours = totalSeconds / 3600;
        var minutes = totalSeconds / 60 % 60;
        var seconds = totalSeconds % 60;
        return hours > 0
            ? $"{hours}:{minutes:00}:{seconds:00}"
            : $"{minutes:00}:{seconds:00}";
    }

    private static string MissingFilePath(FileNotFoundException exception, Track track)
    {
        return string.IsNullOrWhiteSpace(exception.FileName)
            ? track.Path
            : exception.FileName;
    }

    private string AudioPlaybackErrorMessage(AudioPlaybackException exception)
    {
        return exception.Kind switch
        {
            AudioPlaybackFailureKind.UnsupportedCodec => Ui.UnsupportedAudioCodec(exception.Message),
            AudioPlaybackFailureKind.OutputDevice => Ui.AudioOutputDeviceFailed(exception.Message),
            _ => exception.Message
        };
    }

    private string TrackFileStatus(Track? track)
    {
        if (track is null)
        {
            return Ui.NoTrackSelected;
        }

        return File.Exists(track.Path) ? Ui.FileAvailable : Ui.FileMissing;
    }

    private static double TileWidthForColumns(int columns)
    {
        return PlayerPreferences.NormalizeColumns(columns) switch
        {
            2 => 420,
            4 => 204,
            _ => 268
        };
    }

    private static bool IsProfileTransferException(Exception ex)
    {
        return ex is IOException
            or UnauthorizedAccessException
            or InvalidDataException
            or NotSupportedException
            or ArgumentException;
    }

    private static bool IsStorageWriteException(Exception ex)
    {
        return ex is IOException
            or UnauthorizedAccessException
            or NotSupportedException
            or System.Text.Json.JsonException;
    }

    private static bool IsStorageReadException(Exception ex)
    {
        return ex is IOException
            or UnauthorizedAccessException
            or NotSupportedException
            or System.Text.Json.JsonException;
    }

    private string SystemActionName(HotkeyAction action)
    {
        return action.Kind switch
        {
            HotkeyActionKind.StopAll => Ui.StopAllSounds,
            HotkeyActionKind.StopEffects => Ui.StopSfx,
            HotkeyActionKind.PlayPause => Ui.PlayPauseMusic,
            HotkeyActionKind.MusicVolumeUp => Ui.MusicVolumeUp,
            HotkeyActionKind.MusicVolumeDown => Ui.MusicVolumeDown,
            HotkeyActionKind.EffectsVolumeUp => Ui.SfxVolumeUp,
            HotkeyActionKind.EffectsVolumeDown => Ui.SfxVolumeDown,
            _ => Ui.UnknownAction
        };
    }

    private string HotkeyActionName(HotkeyAction action)
    {
        if (action.IsSystemAction)
        {
            return SystemActionName(action);
        }

        if (action.Kind == HotkeyActionKind.PlayMusicTrack)
        {
            var playlist = MusicPlaylists.FirstOrDefault(candidate => candidate.Id == action.PlaylistId);
            var track = playlist?.Tracks.FirstOrDefault(candidate => candidate.Id == action.TrackId);
            return track is null ? Ui.MissingMusicTrack : $"{track.Title} ({playlist?.Name ?? Ui.Music})";
        }

        if (action.Kind == HotkeyActionKind.PlayEffect)
        {
            var playlist = EffectPlaylists.FirstOrDefault(candidate => candidate.Id == action.PlaylistId);
            var track = playlist?.Effects.FirstOrDefault(candidate => candidate.Id == action.TrackId);
            return track is null ? Ui.MissingSfxTrack : $"{track.Title} ({playlist?.Name ?? Ui.Sfx})";
        }

        return Ui.UnknownAction;
    }

    private void NotifyHotkeysChanged()
    {
        OnPropertyChanged(nameof(SystemHotkeyRows));
        OnPropertyChanged(nameof(AssignedTrackHotkeyRows));
        OnPropertyChanged(nameof(AssignedTrackHotkeySummary));
        OnPropertyChanged(nameof(MusicTrackTiles));
        OnPropertyChanged(nameof(EffectTrackTiles));
        OnPropertyChanged(nameof(SelectedMusicTrackTile));
        OnPropertyChanged(nameof(SelectedEffectTrackTile));
        OnPropertyChanged(nameof(SelectedMusicHotkeyText));
        OnPropertyChanged(nameof(SelectedEffectHotkeyText));
        ClearSystemHotkeyCommand?.NotifyCanExecuteChanged();
        ClearSelectedMusicBindingCommand?.NotifyCanExecuteChanged();
        ClearSelectedEffectBindingCommand?.NotifyCanExecuteChanged();
        RemoveMissingMusicFilesCommand?.NotifyCanExecuteChanged();
        RemoveMissingEffectFilesCommand?.NotifyCanExecuteChanged();
    }

    private void NotifyBackgroundChanged()
    {
        OnPropertyChanged(nameof(BackgroundImagePath));
        OnPropertyChanged(nameof(BackgroundImageStatus));
        OnPropertyChanged(nameof(HasBackgroundImage));
        OnPropertyChanged(nameof(BackgroundLayoutMode));
        OnPropertyChanged(nameof(BackgroundOpacity));
        OnPropertyChanged(nameof(BackgroundDimOverlay));
        OnPropertyChanged(nameof(BackgroundBlurRadius));
        ClearBackgroundImageCommand?.NotifyCanExecuteChanged();
    }

    private void NotifyLayoutDensityChanged()
    {
        OnPropertyChanged(nameof(InterfaceDensity));
        OnPropertyChanged(nameof(PanelPadding));
        OnPropertyChanged(nameof(TrackTilePadding));
        OnPropertyChanged(nameof(TrackTileMinHeight));
        OnPropertyChanged(nameof(TrackTileSpacing));
        OnPropertyChanged(nameof(TransportBarPadding));
        OnPropertyChanged(nameof(TransportPanelPadding));
        OnPropertyChanged(nameof(TransportMinHeight));
    }

    private void NotifyMusicTracksChanged()
    {
        PruneTrackTileCache();
        OnPropertyChanged(nameof(MusicTracks));
        OnPropertyChanged(nameof(MusicTrackTiles));
        OnPropertyChanged(nameof(SelectedMusicTrackTile));
        OnPropertyChanged(nameof(HasMusicTracks));
        OnPropertyChanged(nameof(HasVisibleMusicTrackTiles));
        OnPropertyChanged(nameof(IsMusicTracksEmpty));
        OnPropertyChanged(nameof(IsMusicSearchEmpty));
        OnPropertyChanged(nameof(MusicTrackCountText));
        OnPropertyChanged(nameof(SelectedMusicTrackFileStatus));
        PreviousTrackCommand?.NotifyCanExecuteChanged();
        NextTrackCommand?.NotifyCanExecuteChanged();
    }

    private void NotifyEffectTracksChanged()
    {
        PruneTrackTileCache();
        OnPropertyChanged(nameof(EffectTracks));
        OnPropertyChanged(nameof(EffectTrackTiles));
        OnPropertyChanged(nameof(SelectedEffectTrackTile));
        OnPropertyChanged(nameof(HasEffectTracks));
        OnPropertyChanged(nameof(HasVisibleEffectTrackTiles));
        OnPropertyChanged(nameof(IsEffectTracksEmpty));
        OnPropertyChanged(nameof(IsEffectSearchEmpty));
        OnPropertyChanged(nameof(EffectTrackCountText));
        OnPropertyChanged(nameof(SelectedEffectTrackFileStatus));
    }

    private void NotifyMusicTrackFilterChanged()
    {
        ReconcileMusicSelectionWithFilter();
        OnPropertyChanged(nameof(HasMusicSearchText));
        OnPropertyChanged(nameof(MusicTrackTiles));
        OnPropertyChanged(nameof(SelectedMusicTrackTile));
        OnPropertyChanged(nameof(HasVisibleMusicTrackTiles));
        OnPropertyChanged(nameof(IsMusicSearchEmpty));
    }

    private void PruneTrackTileCache()
    {
        var existingIds = MusicPlaylists.SelectMany(playlist => playlist.Tracks)
            .Concat(EffectPlaylists.SelectMany(playlist => playlist.Effects))
            .Select(track => track.Id)
            .ToHashSet();
        foreach (var staleId in _trackTileCache.Keys.Where(id => !existingIds.Contains(id)).ToArray())
        {
            _trackTileCache.Remove(staleId);
        }
    }

    private void NotifyEffectTrackFilterChanged()
    {
        ReconcileEffectSelectionWithFilter();
        OnPropertyChanged(nameof(HasEffectSearchText));
        OnPropertyChanged(nameof(EffectTrackTiles));
        OnPropertyChanged(nameof(SelectedEffectTrackTile));
        OnPropertyChanged(nameof(HasVisibleEffectTrackTiles));
        OnPropertyChanged(nameof(IsEffectSearchEmpty));
    }

    private void ReconcileMusicSelectionWithFilter()
    {
        var visibleIds = SelectedMusicPlaylist?.Tracks
            .Where(track => TrackMatchesSearch(track, MusicSearchText))
            .Select(track => track.Id)
            .ToHashSet() ?? [];

        PruneSelectedTrackIds(_selectedMusicTrackIds, visibleIds);

        if (SelectedMusicTrack is not null && !visibleIds.Contains(SelectedMusicTrack.Id))
        {
            SelectedMusicTrack = null;
        }

        OnPropertyChanged(nameof(HasMusicTrackDeleteSelection));
        OnPropertyChanged(nameof(HasMusicMultiSelection));
        OnPropertyChanged(nameof(MusicSelectionSummary));
        OnPropertyChanged(nameof(MusicDeleteSelectionToolTip));
        NotifyCommandStates();
    }

    private void ReconcileEffectSelectionWithFilter()
    {
        var visibleIds = SelectedEffectPlaylist?.Effects
            .Where(track => TrackMatchesSearch(track, EffectSearchText))
            .Select(track => track.Id)
            .ToHashSet() ?? [];

        PruneSelectedTrackIds(_selectedEffectTrackIds, visibleIds);

        if (SelectedEffectTrack is not null && !visibleIds.Contains(SelectedEffectTrack.Id))
        {
            SelectedEffectTrack = null;
        }

        OnPropertyChanged(nameof(HasEffectTrackDeleteSelection));
        OnPropertyChanged(nameof(HasEffectMultiSelection));
        OnPropertyChanged(nameof(EffectSelectionSummary));
        OnPropertyChanged(nameof(EffectDeleteSelectionToolTip));
        NotifyCommandStates();
    }

    private static void PruneSelectedTrackIds(HashSet<Guid> selectedIds, IReadOnlySet<Guid> visibleIds)
    {
        selectedIds.RemoveWhere(trackId => !visibleIds.Contains(trackId));
    }

    private void RenamePlaylist(EffectPlaylist playlist, string value, string fallback)
    {
        playlist.Name = Track.NormalizedTitle(value, fallback);
        Save();
        OnPropertyChanged(nameof(SelectedEffectPlaylistName));
    }

    private void RenamePlaylist(Playlist playlist, string value, string fallback)
    {
        playlist.Name = Track.NormalizedTitle(value, fallback);
        Save();
        OnPropertyChanged(nameof(SelectedMusicPlaylistName));
    }

    private void RenameTrack(Track track, string value)
    {
        track.Title = Track.NormalizedTitle(value, track.Title);
        Save();
    }

    private static string NextPlaylistName(string prefix, IEnumerable<string> existingNames)
    {
        var existing = existingNames.ToHashSet(StringComparer.CurrentCultureIgnoreCase);
        var index = 1;
        while (existing.Contains($"{prefix} {index}"))
        {
            index++;
        }

        return $"{prefix} {index}";
    }

    private void NotifyCommandStates()
    {
        DeleteMusicPlaylistCommand?.NotifyCanExecuteChanged();
        DeleteEffectPlaylistCommand?.NotifyCanExecuteChanged();
        RequestDeleteMusicPlaylistCommand?.NotifyCanExecuteChanged();
        RequestDeleteEffectPlaylistCommand?.NotifyCanExecuteChanged();
        MoveMusicPlaylistUpCommand?.NotifyCanExecuteChanged();
        MoveMusicPlaylistDownCommand?.NotifyCanExecuteChanged();
        MoveEffectPlaylistUpCommand?.NotifyCanExecuteChanged();
        MoveEffectPlaylistDownCommand?.NotifyCanExecuteChanged();
        PlaySelectedMusicCommand?.NotifyCanExecuteChanged();
        DeleteMusicTrackCommand?.NotifyCanExecuteChanged();
        DeleteEffectTrackCommand?.NotifyCanExecuteChanged();
        RequestDeleteMusicTrackCommand?.NotifyCanExecuteChanged();
        RequestDeleteEffectTrackCommand?.NotifyCanExecuteChanged();
        MoveMusicTrackUpCommand?.NotifyCanExecuteChanged();
        MoveMusicTrackDownCommand?.NotifyCanExecuteChanged();
        MoveEffectTrackUpCommand?.NotifyCanExecuteChanged();
        MoveEffectTrackDownCommand?.NotifyCanExecuteChanged();
        BindSelectedMusicCommand?.NotifyCanExecuteChanged();
        BindSelectedEffectCommand?.NotifyCanExecuteChanged();
        CancelHotkeyCaptureCommand?.NotifyCanExecuteChanged();
        ClearSelectedMusicBindingCommand?.NotifyCanExecuteChanged();
        ClearSelectedEffectBindingCommand?.NotifyCanExecuteChanged();
    }

    private sealed record PendingTrackDelete(TrackRole Role, Guid PlaylistId, IReadOnlyCollection<Guid> TrackIds, string TrackTitle)
    {
        public PendingTrackDelete(TrackRole role, Guid playlistId, Guid trackId, string trackTitle)
            : this(role, playlistId, [trackId], trackTitle)
        {
        }

        public Guid TrackId => TrackIds.FirstOrDefault();

        public int TrackCount => TrackIds.Count;
    }

    private sealed record PendingPlaylistDelete(TrackRole Role, Guid PlaylistId, string PlaylistName);

    private sealed record PendingPlaylistRename(TrackRole Role, Guid PlaylistId, string PlaylistName);

    private sealed record PendingTrackRename(TrackRole Role, Guid PlaylistId, Guid TrackId, string TrackTitle);

    private sealed record PendingHotkeyConflict(HotkeyAction ExistingAction, HotkeyAction ReplacementAction, Hotkey Hotkey);

    private sealed record LayoutDensityMetrics(
        double PanelPadding,
        Thickness TrackTilePadding,
        double TrackTileMinHeight,
        double TrackTileSpacing,
        double TransportBarPadding,
        Thickness TransportPanelPadding,
        double TransportMinHeight);
}
