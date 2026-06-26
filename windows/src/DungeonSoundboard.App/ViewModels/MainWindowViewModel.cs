using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DungeonSoundboard.App.Services;
using DungeonSoundboard.Core.Models;
using DungeonSoundboard.Core.Services;

namespace DungeonSoundboard.App.ViewModels;

public sealed class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly IStorageService _storage;
    private readonly IFileImportService _importService;
    private readonly IAudioService _audio;
    private readonly Random _random = new();
    private AppState _state;
    private Playlist? _selectedMusicPlaylist;
    private EffectPlaylist? _selectedEffectPlaylist;
    private Track? _selectedMusicTrack;
    private Track? _selectedEffectTrack;
    private Track? _currentTrack;
    private Playlist? _playbackMusicPlaylist;
    private bool _isPlaying;
    private string? _errorMessage;
    private string _lastImportMessage = "";
    private HotkeyAction? _captureAction;
    private ThemePresetOption? _selectedThemePreset;
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

    public MainWindowViewModel()
        : this(new JsonFileStorageService(), new FileImportService(), new NAudioAudioService())
    {
    }

    public MainWindowViewModel(IStorageService storage, IFileImportService importService, IAudioService audio)
    {
        _storage = storage;
        _importService = importService;
        _audio = audio;
        _audio.MusicFinished += (_, _) => NextTrackFromPlaybackEnd();
        _audio.EffectPlaybackCountChanged += (_, _) => ApplyMusicVolume();

        _state = _storage.Load();
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
            new ThemePresetOption(ThemePreset.ClassicDungeon, "Classic Dungeon"),
            new ThemePresetOption(ThemePreset.TavernEmber, "Tavern Ember"),
            new ThemePresetOption(ThemePreset.MoonlitCrypt, "Moonlit Crypt"),
            new ThemePresetOption(ThemePreset.ForestMist, "Forest Mist")
        ];
        RepeatModeOptions = [RepeatMode.Off, RepeatMode.One, RepeatMode.All];
        ColumnCountOptions = [2, 3, 4];
        BackgroundLayoutModeOptions =
        [
            BackgroundLayoutMode.Fill,
            BackgroundLayoutMode.Fit,
            BackgroundLayoutMode.Center,
            BackgroundLayoutMode.Tile
        ];
        SelectedThemePreset = ThemePresetOptions.FirstOrDefault(option => option.Preset == _state.Theme.Preset)
            ?? ThemePresetOptions[0];
        UpdateThemeBrushes();

        CreateMusicPlaylistCommand = new RelayCommand(CreateMusicPlaylist);
        CreateEffectPlaylistCommand = new RelayCommand(CreateEffectPlaylist);
        DeleteMusicPlaylistCommand = new RelayCommand(DeleteSelectedMusicPlaylist, () => MusicPlaylists.Count > 1 && SelectedMusicPlaylist is not null);
        DeleteEffectPlaylistCommand = new RelayCommand(DeleteSelectedEffectPlaylist, () => EffectPlaylists.Count > 1 && SelectedEffectPlaylist is not null);
        MoveMusicPlaylistUpCommand = new RelayCommand(() => MoveSelectedPlaylist(MusicPlaylists, SelectedMusicPlaylist, -1), () => CanMove(MusicPlaylists, SelectedMusicPlaylist, -1));
        MoveMusicPlaylistDownCommand = new RelayCommand(() => MoveSelectedPlaylist(MusicPlaylists, SelectedMusicPlaylist, 1), () => CanMove(MusicPlaylists, SelectedMusicPlaylist, 1));
        MoveEffectPlaylistUpCommand = new RelayCommand(() => MoveSelectedPlaylist(EffectPlaylists, SelectedEffectPlaylist, -1), () => CanMove(EffectPlaylists, SelectedEffectPlaylist, -1));
        MoveEffectPlaylistDownCommand = new RelayCommand(() => MoveSelectedPlaylist(EffectPlaylists, SelectedEffectPlaylist, 1), () => CanMove(EffectPlaylists, SelectedEffectPlaylist, 1));
        AddMusicFilesCommand = new AsyncRelayCommand(AddMusicFilesAsync);
        AddMusicFolderCommand = new AsyncRelayCommand(AddMusicFolderAsync);
        AddEffectFilesCommand = new AsyncRelayCommand(AddEffectFilesAsync);
        AddEffectFolderCommand = new AsyncRelayCommand(AddEffectFolderAsync);
        PlaySelectedMusicCommand = new RelayCommand(() => PlayMusicTrack(SelectedMusicPlaylist, SelectedMusicTrack), () => SelectedMusicTrack is not null);
        PlayMusicTrackCommand = new RelayCommand<Track>(track => PlayMusicTrack(SelectedMusicPlaylist, track));
        PlayEffectCommand = new RelayCommand<Track>(PlayEffect);
        PlayPauseCommand = new RelayCommand(PlayPause);
        StopAllCommand = new RelayCommand(StopAll);
        StopEffectsCommand = new RelayCommand(StopEffects);
        NextTrackCommand = new RelayCommand(NextTrack);
        PreviousTrackCommand = new RelayCommand(PreviousTrack);
        DeleteMusicTrackCommand = new RelayCommand(DeleteSelectedMusicTrack, () => SelectedMusicTrack is not null);
        DeleteEffectTrackCommand = new RelayCommand(DeleteSelectedEffectTrack, () => SelectedEffectTrack is not null);
        MoveMusicTrackUpCommand = new RelayCommand(() => MoveSelectedTrack(SelectedMusicPlaylist?.Tracks, SelectedMusicTrack, -1, nameof(MusicTracks)), () => CanMove(SelectedMusicPlaylist?.Tracks, SelectedMusicTrack, -1));
        MoveMusicTrackDownCommand = new RelayCommand(() => MoveSelectedTrack(SelectedMusicPlaylist?.Tracks, SelectedMusicTrack, 1, nameof(MusicTracks)), () => CanMove(SelectedMusicPlaylist?.Tracks, SelectedMusicTrack, 1));
        MoveEffectTrackUpCommand = new RelayCommand(() => MoveSelectedTrack(SelectedEffectPlaylist?.Effects, SelectedEffectTrack, -1, nameof(EffectTracks)), () => CanMove(SelectedEffectPlaylist?.Effects, SelectedEffectTrack, -1));
        MoveEffectTrackDownCommand = new RelayCommand(() => MoveSelectedTrack(SelectedEffectPlaylist?.Effects, SelectedEffectTrack, 1, nameof(EffectTracks)), () => CanMove(SelectedEffectPlaylist?.Effects, SelectedEffectTrack, 1));
        DeleteMusicTrackItemCommand = new RelayCommand<Track>(DeleteMusicTrack);
        DeleteEffectTrackItemCommand = new RelayCommand<Track>(DeleteEffectTrack);
        MoveMusicTrackItemUpCommand = new RelayCommand<Track>(track => MoveTrackItem(SelectedMusicPlaylist?.Tracks, track, -1, nameof(MusicTracks)));
        MoveMusicTrackItemDownCommand = new RelayCommand<Track>(track => MoveTrackItem(SelectedMusicPlaylist?.Tracks, track, 1, nameof(MusicTracks)));
        MoveEffectTrackItemUpCommand = new RelayCommand<Track>(track => MoveTrackItem(SelectedEffectPlaylist?.Effects, track, -1, nameof(EffectTracks)));
        MoveEffectTrackItemDownCommand = new RelayCommand<Track>(track => MoveTrackItem(SelectedEffectPlaylist?.Effects, track, 1, nameof(EffectTracks)));
        BindSelectedMusicCommand = new RelayCommand(BeginBindSelectedMusic, () => SelectedMusicPlaylist is not null && SelectedMusicTrack is not null);
        BindSelectedEffectCommand = new RelayCommand(BeginBindSelectedEffect, () => SelectedEffectPlaylist is not null && SelectedEffectTrack is not null);
        BindMusicTrackItemCommand = new RelayCommand<Track>(BeginBindMusicTrack);
        BindEffectTrackItemCommand = new RelayCommand<Track>(BeginBindEffectTrack);
        BindSystemHotkeyCommand = new RelayCommand<HotkeyAction>(BeginBindSystemHotkey);
        ClearSystemHotkeyCommand = new RelayCommand<HotkeyAction>(ClearSystemHotkey);
        CancelHotkeyCaptureCommand = new RelayCommand(CancelHotkeyCapture, () => CaptureAction is not null);
        ClearSelectedMusicBindingCommand = new RelayCommand(ClearSelectedMusicBinding, () => SelectedMusicPlaylist is not null && SelectedMusicTrack is not null);
        ClearSelectedEffectBindingCommand = new RelayCommand(ClearSelectedEffectBinding, () => SelectedEffectPlaylist is not null && SelectedEffectTrack is not null);
        ClearMusicTrackItemBindingCommand = new RelayCommand<Track>(ClearMusicTrackBinding);
        ClearEffectTrackItemBindingCommand = new RelayCommand<Track>(ClearEffectTrackBinding);
        ChooseBackgroundImageCommand = new AsyncRelayCommand(ChooseBackgroundImageAsync);
        ClearBackgroundImageCommand = new RelayCommand(ClearBackgroundImage, () => HasBackgroundImage);
    }

    public IFileDialogService? FileDialogService { get; set; }

    public ObservableCollection<Playlist> MusicPlaylists { get; }
    public ObservableCollection<EffectPlaylist> EffectPlaylists { get; }
    public IReadOnlyList<ThemePresetOption> ThemePresetOptions { get; }
    public IReadOnlyList<RepeatMode> RepeatModeOptions { get; }
    public IReadOnlyList<int> ColumnCountOptions { get; }
    public IReadOnlyList<BackgroundLayoutMode> BackgroundLayoutModeOptions { get; }

    public IRelayCommand CreateMusicPlaylistCommand { get; }
    public IRelayCommand CreateEffectPlaylistCommand { get; }
    public IRelayCommand DeleteMusicPlaylistCommand { get; }
    public IRelayCommand DeleteEffectPlaylistCommand { get; }
    public IRelayCommand MoveMusicPlaylistUpCommand { get; }
    public IRelayCommand MoveMusicPlaylistDownCommand { get; }
    public IRelayCommand MoveEffectPlaylistUpCommand { get; }
    public IRelayCommand MoveEffectPlaylistDownCommand { get; }
    public IAsyncRelayCommand AddMusicFilesCommand { get; }
    public IAsyncRelayCommand AddMusicFolderCommand { get; }
    public IAsyncRelayCommand AddEffectFilesCommand { get; }
    public IAsyncRelayCommand AddEffectFolderCommand { get; }
    public IRelayCommand PlaySelectedMusicCommand { get; }
    public IRelayCommand<Track> PlayMusicTrackCommand { get; }
    public IRelayCommand<Track> PlayEffectCommand { get; }
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
    public IRelayCommand<Track> DeleteMusicTrackItemCommand { get; }
    public IRelayCommand<Track> DeleteEffectTrackItemCommand { get; }
    public IRelayCommand<Track> MoveMusicTrackItemUpCommand { get; }
    public IRelayCommand<Track> MoveMusicTrackItemDownCommand { get; }
    public IRelayCommand<Track> MoveEffectTrackItemUpCommand { get; }
    public IRelayCommand<Track> MoveEffectTrackItemDownCommand { get; }
    public IRelayCommand BindSelectedMusicCommand { get; }
    public IRelayCommand BindSelectedEffectCommand { get; }
    public IRelayCommand<Track> BindMusicTrackItemCommand { get; }
    public IRelayCommand<Track> BindEffectTrackItemCommand { get; }
    public IRelayCommand<HotkeyAction> BindSystemHotkeyCommand { get; }
    public IRelayCommand<HotkeyAction> ClearSystemHotkeyCommand { get; }
    public IRelayCommand CancelHotkeyCaptureCommand { get; }
    public IRelayCommand ClearSelectedMusicBindingCommand { get; }
    public IRelayCommand ClearSelectedEffectBindingCommand { get; }
    public IRelayCommand<Track> ClearMusicTrackItemBindingCommand { get; }
    public IRelayCommand<Track> ClearEffectTrackItemBindingCommand { get; }
    public IAsyncRelayCommand ChooseBackgroundImageCommand { get; }
    public IRelayCommand ClearBackgroundImageCommand { get; }

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
            SelectedMusicTrack = value?.Tracks.FirstOrDefault();
            NotifyMusicTracksChanged();
            OnPropertyChanged(nameof(SelectedMusicPlaylistName));
            Save();
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
            SelectedEffectTrack = value?.Effects.FirstOrDefault();
            NotifyEffectTracksChanged();
            OnPropertyChanged(nameof(SelectedEffectPlaylistName));
            Save();
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
                OnPropertyChanged(nameof(SelectedMusicTrackVolume));
                OnPropertyChanged(nameof(SelectedMusicHotkeyText));
                OnPropertyChanged(nameof(SelectedMusicTrackFileStatus));
                OnPropertyChanged(nameof(SelectedMusicTrackTile));
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
                OnPropertyChanged(nameof(SelectedEffectTrackVolume));
                OnPropertyChanged(nameof(SelectedEffectHotkeyText));
                OnPropertyChanged(nameof(SelectedEffectTrackFileStatus));
                OnPropertyChanged(nameof(SelectedEffectTrackTile));
                NotifyCommandStates();
            }
        }
    }

    public IReadOnlyList<Track> MusicTracks => SelectedMusicPlaylist?.Tracks ?? [];
    public IReadOnlyList<Track> EffectTracks => SelectedEffectPlaylist?.Effects ?? [];
    public IReadOnlyList<TrackTileViewModel> MusicTrackTiles => SelectedMusicPlaylist is null
        ? []
        : SelectedMusicPlaylist.Tracks
            .Select(track => TrackTileFor(track, HotkeyAction.PlayMusicTrack(SelectedMusicPlaylist.Id, track.Id), CurrentTrack?.Id == track.Id))
            .ToList();

    public IReadOnlyList<TrackTileViewModel> EffectTrackTiles => SelectedEffectPlaylist is null
        ? []
        : SelectedEffectPlaylist.Effects
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
    public bool IsMusicTracksEmpty => !HasMusicTracks;
    public bool IsEffectTracksEmpty => !HasEffectTracks;
    public string MusicTrackCountText => TrackCountText(MusicTracks.Count, "track");
    public string EffectTrackCountText => TrackCountText(EffectTracks.Count, "effect");

    public string SelectedMusicPlaylistName
    {
        get => SelectedMusicPlaylist?.Name ?? "";
        set
        {
            if (SelectedMusicPlaylist is null)
            {
                return;
            }

            RenamePlaylist(SelectedMusicPlaylist, value, "Main Playlist");
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

            RenamePlaylist(SelectedEffectPlaylist, value, "SFX Master");
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
            NotifyMusicTracksChanged();
        }
    }

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
            NotifyEffectTracksChanged();
        }
    }

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
            }
        }
    }

    public string CurrentTrackTitle => CurrentTrack?.Title ?? "Nothing is playing";

    public string PlaybackStatus => IsPlaying ? "Playing" : "Paused / stopped";

    public string DataDirectory => _storage.DataDirectory;

    public string BackgroundImagePath => string.IsNullOrWhiteSpace(_state.Theme.Background.ImageOriginalPath)
        ? "No background image selected"
        : _state.Theme.Background.ImageOriginalPath;

    public string BackgroundImageStatus => _state.Theme.Background.Mode == BackgroundMode.Image
        ? "Image background"
        : "Theme preset background";

    public bool HasBackgroundImage => _state.Theme.Background.Mode == BackgroundMode.Image
        && !string.IsNullOrWhiteSpace(_state.Theme.Background.ImageOriginalPath);

    public BackgroundLayoutMode BackgroundLayoutMode
    {
        get => _state.Theme.Background.LayoutMode;
        set
        {
            if (_state.Theme.Background.LayoutMode == value)
            {
                return;
            }

            _state.Theme.Background.LayoutMode = value;
            OnPropertyChanged();
            UpdateThemeBrushes();
            Save();
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

            _state.Theme.Background.Opacity = clamped;
            OnPropertyChanged();
            UpdateThemeBrushes();
            Save();
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

            _state.Theme.Background.DimOverlay = clamped;
            OnPropertyChanged();
            UpdateThemeBrushes();
            Save();
        }
    }

    public string SelectedMusicHotkeyText => SelectedMusicPlaylist is null || SelectedMusicTrack is null
        ? "Select track"
        : HotkeyTextFor(HotkeyAction.PlayMusicTrack(SelectedMusicPlaylist.Id, SelectedMusicTrack.Id));

    public string SelectedEffectHotkeyText => SelectedEffectPlaylist is null || SelectedEffectTrack is null
        ? "Select track"
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

    public bool IsPlaying
    {
        get => _isPlaying;
        private set
        {
            if (SetProperty(ref _isPlaying, value))
            {
                OnPropertyChanged(nameof(PlaybackStatus));
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
            }
        }
    }

    public string StatusMessage => ErrorMessage ?? LastImportMessage;

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
            ApplyMusicVolume();
            Save();
        }
    }

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
            _audio.SetEffectsVolume(clamped);
            Save();
        }
    }

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
            Save();
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
            Save();
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
                NotifyCommandStates();
            }
        }
    }

    public string CaptureStatus => CaptureAction is null
        ? "Hotkeys active: Space, Delete, +/-, Shift +/-"
        : "Press a key to assign it, or Esc to cancel";

    public ThemePresetOption? SelectedThemePreset
    {
        get => _selectedThemePreset;
        set
        {
            if (!SetProperty(ref _selectedThemePreset, value) || value is null)
            {
                return;
            }

            var currentBackground = CloneBackground(_state.Theme.Background);
            _state.Theme = ThemeRenderer.ThemeFor(value.Preset);
            _state.Theme.Background = currentBackground;
            UpdateThemeBrushes();
            Save();
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

    public void ImportDroppedMusic(IEnumerable<string> paths)
    {
        ImportTracks(paths, TrackRole.Music);
    }

    public void ImportDroppedEffects(IEnumerable<string> paths)
    {
        ImportTracks(paths, TrackRole.Effect);
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
            _state.Hotkeys.Assign(hotkey, CaptureAction, resolvingConflicts: true);
            CaptureAction = null;
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

    public void Dispose()
    {
        _audio.Dispose();
    }

    private async Task AddMusicFilesAsync()
    {
        if (FileDialogService is null)
        {
            return;
        }

        ImportTracks(await FileDialogService.OpenAudioFilesAsync(), TrackRole.Music);
    }

    private async Task AddMusicFolderAsync()
    {
        if (FileDialogService is null)
        {
            return;
        }

        ImportTracks(await FileDialogService.OpenAudioFolderAsync(), TrackRole.Music);
    }

    private async Task AddEffectFilesAsync()
    {
        if (FileDialogService is null)
        {
            return;
        }

        ImportTracks(await FileDialogService.OpenAudioFilesAsync(), TrackRole.Effect);
    }

    private async Task AddEffectFolderAsync()
    {
        if (FileDialogService is null)
        {
            return;
        }

        ImportTracks(await FileDialogService.OpenAudioFolderAsync(), TrackRole.Effect);
    }

    private async Task ChooseBackgroundImageAsync()
    {
        if (FileDialogService is null)
        {
            return;
        }

        var path = (await FileDialogService.OpenBackgroundImageAsync()).FirstOrDefault();
        if (path is null)
        {
            return;
        }

        _state.Theme.Background.Mode = BackgroundMode.Image;
        _state.Theme.Background.ImageOriginalPath = path;
        UpdateThemeBrushes();
        Save();
    }

    private void ClearBackgroundImage()
    {
        _state.Theme.Background.Mode = BackgroundMode.None;
        _state.Theme.Background.ImageBookmarkData = null;
        _state.Theme.Background.ImageOriginalPath = null;
        _state.Theme.Background.BlurRadius = 0;
        UpdateThemeBrushes();
        Save();
    }

    private void ImportTracks(IEnumerable<string> paths, TrackRole role)
    {
        ErrorMessage = null;
        if (role == TrackRole.Music)
        {
            if (SelectedMusicPlaylist is null)
            {
                return;
            }

            var result = _importService.BuildUniqueTracks(paths, role, SelectedMusicPlaylist.Tracks, "Music Playlists");
            SelectedMusicPlaylist.Tracks.AddRange(result.AddedTracks);
            if (CurrentTrack is null)
            {
                CurrentTrack = SelectedMusicPlaylist.Tracks.FirstOrDefault();
                _playbackMusicPlaylist = SelectedMusicPlaylist;
            }

            NotifyMusicTracksChanged();
            LastImportMessage = ImportMessage(result);
        }
        else
        {
            if (SelectedEffectPlaylist is null)
            {
                return;
            }

            var result = _importService.BuildUniqueTracks(paths, role, SelectedEffectPlaylist.Effects, "SFX Playlists");
            SelectedEffectPlaylist.Effects.AddRange(result.AddedTracks);
            NotifyEffectTracksChanged();
            LastImportMessage = ImportMessage(result);
        }

        Save();
    }

    private static string ImportMessage(FileImportResult result)
    {
        if (result.ConflictSummary is null)
        {
            return result.AddedTracks.Count == 0 ? "No supported audio files found." : $"Added {result.AddedTracks.Count} file(s).";
        }

        return $"Added {result.ConflictSummary.AddedCount}; skipped duplicates: {result.ConflictSummary.DuplicateCount}.";
    }

    private void CreateMusicPlaylist()
    {
        var playlist = new Playlist(NextPlaylistName("Playlist", MusicPlaylists.Select(item => item.Name)));
        MusicPlaylists.Add(playlist);
        SelectedMusicPlaylist = playlist;
        Save();
        NotifyCommandStates();
    }

    private void CreateEffectPlaylist()
    {
        var playlist = new EffectPlaylist(NextPlaylistName("SFX", EffectPlaylists.Select(item => item.Name)));
        EffectPlaylists.Add(playlist);
        SelectedEffectPlaylist = playlist;
        Save();
        NotifyCommandStates();
    }

    private void DeleteSelectedMusicPlaylist()
    {
        if (SelectedMusicPlaylist is null || MusicPlaylists.Count <= 1)
        {
            return;
        }

        var removed = SelectedMusicPlaylist;
        MusicPlaylists.Remove(removed);
        if (_playbackMusicPlaylist?.Id == removed.Id)
        {
            StopAll();
        }

        SelectedMusicPlaylist = MusicPlaylists.FirstOrDefault();
        Save();
    }

    private void DeleteSelectedEffectPlaylist()
    {
        if (SelectedEffectPlaylist is null || EffectPlaylists.Count <= 1)
        {
            return;
        }

        EffectPlaylists.Remove(SelectedEffectPlaylist);
        SelectedEffectPlaylist = EffectPlaylists.FirstOrDefault();
        Save();
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
        if (SelectedMusicPlaylist is null || track is null)
        {
            return;
        }

        var removed = SelectedMusicPlaylist.Tracks.FirstOrDefault(candidate => candidate.Id == track.Id);
        if (removed is null)
        {
            return;
        }

        SelectedMusicPlaylist.Tracks.Remove(removed);
        if (CurrentTrack?.Id == removed.Id)
        {
            StopAll();
            CurrentTrack = SelectedMusicPlaylist.Tracks.FirstOrDefault();
        }

        if (SelectedMusicTrack?.Id == removed.Id)
        {
            SelectedMusicTrack = SelectedMusicPlaylist.Tracks.FirstOrDefault();
        }

        NotifyMusicTracksChanged();
        Save();
        NotifyCommandStates();
    }

    private void DeleteEffectTrack(Track? track)
    {
        if (SelectedEffectPlaylist is null || track is null)
        {
            return;
        }

        var removed = SelectedEffectPlaylist.Effects.FirstOrDefault(candidate => candidate.Id == track.Id);
        if (removed is null)
        {
            return;
        }

        SelectedEffectPlaylist.Effects.Remove(removed);
        if (SelectedEffectTrack?.Id == removed.Id)
        {
            SelectedEffectTrack = SelectedEffectPlaylist.Effects.FirstOrDefault();
        }

        NotifyEffectTracksChanged();
        Save();
        NotifyCommandStates();
    }

    private void MoveSelectedPlaylist<T>(ObservableCollection<T> collection, T? selected, int delta)
        where T : class
    {
        if (selected is null)
        {
            return;
        }

        var index = collection.IndexOf(selected);
        var targetIndex = index + delta;
        if (index < 0 || targetIndex < 0 || targetIndex >= collection.Count)
        {
            return;
        }

        collection.Move(index, targetIndex);
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

        Save();
        NotifyCommandStates();
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

    private void PlayMusicTrack(Playlist? playlist, Track? track)
    {
        if (playlist is null || track is null)
        {
            return;
        }

        try
        {
            CurrentTrack = track;
            _playbackMusicPlaylist = playlist;
            _audio.PlayMusic(track, CurrentMusicOutputVolume());
            IsPlaying = true;
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            IsPlaying = false;
            ErrorMessage = $"Failed to play file: {track.Title}. {ex.Message}";
        }
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
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to play effect: {track.Title}. {ex.Message}";
        }
    }

    private void PlayPause()
    {
        if (IsPlaying)
        {
            _audio.PauseMusic();
            IsPlaying = false;
            return;
        }

        if (_audio.IsMusicPlaying)
        {
            _audio.ResumeMusic();
            IsPlaying = true;
            return;
        }

        var playlist = _playbackMusicPlaylist ?? SelectedMusicPlaylist;
        var track = CurrentTrack ?? playlist?.Tracks.FirstOrDefault();
        PlayMusicTrack(playlist, track);
    }

    private void StopAll()
    {
        _audio.StopAll();
        IsPlaying = false;
    }

    private void StopEffects()
    {
        _audio.StopEffects();
    }

    private void NextTrackFromPlaybackEnd()
    {
        IsPlaying = false;
        NextTrack();
    }

    private void NextTrack()
    {
        var playlist = _playbackMusicPlaylist ?? SelectedMusicPlaylist;
        if (playlist is null || playlist.Tracks.Count == 0)
        {
            return;
        }

        if (ShuffleEnabled)
        {
            var next = playlist.Tracks[_random.Next(playlist.Tracks.Count)];
            PlayMusicTrack(playlist, next);
            return;
        }

        var currentIndex = CurrentTrack is null ? -1 : playlist.Tracks.FindIndex(track => track.Id == CurrentTrack.Id);
        var nextIndex = currentIndex + 1;
        if (nextIndex < playlist.Tracks.Count)
        {
            PlayMusicTrack(playlist, playlist.Tracks[nextIndex]);
            return;
        }

        switch (RepeatMode)
        {
            case RepeatMode.One:
                PlayMusicTrack(playlist, CurrentTrack);
                break;
            case RepeatMode.All:
                PlayMusicTrack(playlist, playlist.Tracks[0]);
                break;
            default:
                StopAll();
                break;
        }
    }

    private void PreviousTrack()
    {
        var playlist = _playbackMusicPlaylist ?? SelectedMusicPlaylist;
        if (playlist is null || playlist.Tracks.Count == 0)
        {
            return;
        }

        var currentIndex = CurrentTrack is null ? -1 : playlist.Tracks.FindIndex(track => track.Id == CurrentTrack.Id);
        var previousIndex = currentIndex - 1;
        if (previousIndex >= 0)
        {
            PlayMusicTrack(playlist, playlist.Tracks[previousIndex]);
            return;
        }

        if (RepeatMode == RepeatMode.All)
        {
            PlayMusicTrack(playlist, playlist.Tracks[^1]);
        }
        else
        {
            PlayMusicTrack(playlist, playlist.Tracks[0]);
        }
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

    private void CancelHotkeyCapture()
    {
        CaptureAction = null;
    }

    private void ClearSelectedMusicBinding()
    {
        ClearMusicTrackBinding(SelectedMusicTrack);
    }

    private void ClearSelectedEffectBinding()
    {
        ClearEffectTrackBinding(SelectedEffectTrack);
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
        var ducking = _audio.ActiveEffectCount > 0 ? DuckingAmount : 1.0;
        return Track.OutputVolume(
            MusicVolume,
            CurrentTrack?.VolumeMultiplier ?? Track.DefaultVolumeMultiplier,
            ducking);
    }

    private void Save()
    {
        _state.MusicPlaylists = MusicPlaylists.ToList();
        _state.EffectPlaylists = EffectPlaylists.ToList();
        _state.EnsureDefaults();
        _storage.Save(_state);
    }

    private void UpdateThemeBrushes()
    {
        var resolved = ThemeRenderer.Resolve(_state.Theme);
        BackgroundBrush = CreateBackgroundBrush(resolved);
        BackgroundOverlayBrush = CreateBackgroundOverlayBrush();
        PanelBrush = ToBrush(resolved.Panel);
        PanelAltBrush = ToBrush(resolved.PanelAlt);
        CardBrush = ToBrush(resolved.Card);
        CurrentCardBrush = ToBrush(resolved.CardCurrent);
        AccentBrush = ToBrush(resolved.Accent);
        DangerBrush = ToBrush(resolved.Danger);
        TextPrimaryBrush = ToBrush(resolved.TextPrimary);
        TextSecondaryBrush = ToBrush(resolved.TextSecondary);
        NotifyBackgroundChanged();
    }

    private IBrush CreateBackgroundBrush(ResolvedTheme resolved)
    {
        var imagePath = _state.Theme.Background.ImageOriginalPath;
        if (_state.Theme.Background.Mode == BackgroundMode.Image &&
            !string.IsNullOrWhiteSpace(imagePath) &&
            File.Exists(imagePath))
        {
            try
            {
                var brush = new ImageBrush(new Bitmap(imagePath))
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
            catch
            {
                ErrorMessage = $"Failed to load background image: {imagePath}";
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
        return _state.Hotkeys.HotkeyFor(action)?.DisplayText ?? "Unassigned";
    }

    private TrackTileViewModel TrackTileFor(Track track, HotkeyAction action, bool isCurrent)
    {
        return new TrackTileViewModel(
            track,
            _state.Hotkeys.HotkeyFor(action)?.DisplayText,
            isCurrent,
            !File.Exists(track.Path));
    }

    private static string TrackCountText(int count, string singular)
    {
        var suffix = count == 1 ? singular : $"{singular}s";
        return $"{count} {suffix}";
    }

    private static string TrackFileStatus(Track? track)
    {
        if (track is null)
        {
            return "No track selected";
        }

        return File.Exists(track.Path) ? "File available" : "File missing";
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

    private static string SystemActionName(HotkeyAction action)
    {
        return action.Kind switch
        {
            HotkeyActionKind.StopAll => "Stop all playback",
            HotkeyActionKind.StopEffects => "Stop SFX",
            HotkeyActionKind.PlayPause => "Play / pause music",
            HotkeyActionKind.MusicVolumeUp => "Music volume up",
            HotkeyActionKind.MusicVolumeDown => "Music volume down",
            HotkeyActionKind.EffectsVolumeUp => "SFX volume up",
            HotkeyActionKind.EffectsVolumeDown => "SFX volume down",
            _ => "Unknown action"
        };
    }

    private void NotifyHotkeysChanged()
    {
        OnPropertyChanged(nameof(SystemHotkeyRows));
        OnPropertyChanged(nameof(MusicTrackTiles));
        OnPropertyChanged(nameof(EffectTrackTiles));
        OnPropertyChanged(nameof(SelectedMusicTrackTile));
        OnPropertyChanged(nameof(SelectedEffectTrackTile));
        OnPropertyChanged(nameof(SelectedMusicHotkeyText));
        OnPropertyChanged(nameof(SelectedEffectHotkeyText));
    }

    private void NotifyBackgroundChanged()
    {
        OnPropertyChanged(nameof(BackgroundImagePath));
        OnPropertyChanged(nameof(BackgroundImageStatus));
        OnPropertyChanged(nameof(HasBackgroundImage));
        OnPropertyChanged(nameof(BackgroundLayoutMode));
        OnPropertyChanged(nameof(BackgroundOpacity));
        OnPropertyChanged(nameof(BackgroundDimOverlay));
        ClearBackgroundImageCommand?.NotifyCanExecuteChanged();
    }

    private void NotifyMusicTracksChanged()
    {
        OnPropertyChanged(nameof(MusicTracks));
        OnPropertyChanged(nameof(MusicTrackTiles));
        OnPropertyChanged(nameof(SelectedMusicTrackTile));
        OnPropertyChanged(nameof(HasMusicTracks));
        OnPropertyChanged(nameof(IsMusicTracksEmpty));
        OnPropertyChanged(nameof(MusicTrackCountText));
        OnPropertyChanged(nameof(SelectedMusicTrackFileStatus));
    }

    private void NotifyEffectTracksChanged()
    {
        OnPropertyChanged(nameof(EffectTracks));
        OnPropertyChanged(nameof(EffectTrackTiles));
        OnPropertyChanged(nameof(SelectedEffectTrackTile));
        OnPropertyChanged(nameof(HasEffectTracks));
        OnPropertyChanged(nameof(IsEffectTracksEmpty));
        OnPropertyChanged(nameof(EffectTrackCountText));
        OnPropertyChanged(nameof(SelectedEffectTrackFileStatus));
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
        MoveMusicPlaylistUpCommand?.NotifyCanExecuteChanged();
        MoveMusicPlaylistDownCommand?.NotifyCanExecuteChanged();
        MoveEffectPlaylistUpCommand?.NotifyCanExecuteChanged();
        MoveEffectPlaylistDownCommand?.NotifyCanExecuteChanged();
        PlaySelectedMusicCommand?.NotifyCanExecuteChanged();
        DeleteMusicTrackCommand?.NotifyCanExecuteChanged();
        DeleteEffectTrackCommand?.NotifyCanExecuteChanged();
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
}
