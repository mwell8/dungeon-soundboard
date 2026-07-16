using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DungeonSoundboard.Core.Models;

namespace DungeonSoundboard.App.ViewModels;

public sealed class TrackTileViewModel : ObservableObject, IEquatable<TrackTileViewModel>
{
    private readonly Action<Track, double>? _setVolume;
    private string _hotkeyText;
    private bool _isCurrent;
    private bool _isFileMissing;
    private string _missingPrefix;
    private string _fileMissingStatus;
    private string _fileAvailableStatus;
    private string _missingBadgeText;

    public TrackTileViewModel(Track track, string? hotkeyText, bool isCurrent, bool isFileMissing,
        string missingPrefix, string fileMissingStatus, string fileAvailableStatus, string missingBadgeText,
        Action<Track, double>? setVolume = null, AppUiStrings? ui = null, bool isEffect = false)
    {
        Track = track;
        _hotkeyText = hotkeyText ?? "";
        _isCurrent = isCurrent;
        _isFileMissing = isFileMissing;
        _missingPrefix = missingPrefix;
        _fileMissingStatus = fileMissingStatus;
        _fileAvailableStatus = fileAvailableStatus;
        _missingBadgeText = missingBadgeText;
        _setVolume = setVolume;
        ResetVolumeCommand = new RelayCommand(() => VolumeMultiplier = Track.DefaultVolumeMultiplier);
        UpdateMenuStrings(ui ?? AppUiStrings.For(AppLanguage.English), isEffect);
    }

    public Track Track { get; }
    public string Title => Track.Title;
    public string Path => Track.Path;
    public string FileDisplayName => string.IsNullOrWhiteSpace(System.IO.Path.GetFileName(Track.Path)) ? Track.Path : System.IO.Path.GetFileName(Track.Path);
    public string FileDisplayDetail => IsFileMissing ? $"{_missingPrefix}: {FileDisplayName}" : FileDisplayName;

    public double VolumeMultiplier
    {
        get => Track.VolumeMultiplier;
        set
        {
            var normalized = Track.NormalizedVolumeMultiplier(value);
            if (Math.Abs(Track.VolumeMultiplier - normalized) < 0.0001) return;
            if (_setVolume is null) Track.VolumeMultiplier = normalized;
            else _setVolume(Track, normalized);
            OnPropertyChanged();
            OnPropertyChanged(nameof(VolumePercentText));
        }
    }

    public string VolumePercentText => $"{VolumeMultiplier * 100:0}%";
    public IRelayCommand ResetVolumeCommand { get; }
    public string PlayMenuText { get; private set; } = "";
    public string RenameMenuText { get; private set; } = "";
    public string OpenFileLocationMenuText { get; private set; } = "";
    public string VolumeMenuText { get; private set; } = "";
    public string BindHotkeyMenuText { get; private set; } = "";
    public string ClearHotkeyMenuText { get; private set; } = "";
    public string DeleteMenuText { get; private set; } = "";
    public string NormalVolumeMenuText { get; private set; } = "";
    public string HotkeyText => _hotkeyText;
    public bool HasHotkey => !string.IsNullOrWhiteSpace(HotkeyText);
    public bool IsCurrent => _isCurrent;
    public bool IsFileMissing => _isFileMissing;
    public string FileStatus => IsFileMissing ? _fileMissingStatus : _fileAvailableStatus;
    public string MissingBadgeText => _missingBadgeText;

    public void Refresh(string? hotkeyText, bool isCurrent, bool isFileMissing,
        string missingPrefix, string fileMissingStatus, string fileAvailableStatus, string missingBadgeText,
        AppUiStrings ui, bool isEffect)
    {
        _hotkeyText = hotkeyText ?? "";
        _isCurrent = isCurrent;
        _isFileMissing = isFileMissing;
        _missingPrefix = missingPrefix;
        _fileMissingStatus = fileMissingStatus;
        _fileAvailableStatus = fileAvailableStatus;
        _missingBadgeText = missingBadgeText;
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Path));
        OnPropertyChanged(nameof(FileDisplayName));
        OnPropertyChanged(nameof(FileDisplayDetail));
        OnPropertyChanged(nameof(VolumeMultiplier));
        OnPropertyChanged(nameof(VolumePercentText));
        OnPropertyChanged(nameof(HotkeyText));
        OnPropertyChanged(nameof(HasHotkey));
        OnPropertyChanged(nameof(IsCurrent));
        OnPropertyChanged(nameof(IsFileMissing));
        OnPropertyChanged(nameof(FileStatus));
        OnPropertyChanged(nameof(MissingBadgeText));
        UpdateMenuStrings(ui, isEffect);
    }

    private void UpdateMenuStrings(AppUiStrings ui, bool isEffect)
    {
        PlayMenuText = isEffect ? ui.PlaySfx : ui.Play;
        RenameMenuText = ui.Rename;
        OpenFileLocationMenuText = ui.OpenFileLocation;
        VolumeMenuText = ui.Volume;
        BindHotkeyMenuText = ui.BindHotkey;
        ClearHotkeyMenuText = ui.ClearHotkey;
        DeleteMenuText = ui.Delete;
        NormalVolumeMenuText = ui.NormalVolume;
        OnPropertyChanged(nameof(PlayMenuText));
        OnPropertyChanged(nameof(RenameMenuText));
        OnPropertyChanged(nameof(OpenFileLocationMenuText));
        OnPropertyChanged(nameof(VolumeMenuText));
        OnPropertyChanged(nameof(BindHotkeyMenuText));
        OnPropertyChanged(nameof(ClearHotkeyMenuText));
        OnPropertyChanged(nameof(DeleteMenuText));
        OnPropertyChanged(nameof(NormalVolumeMenuText));
    }

    public bool Equals(TrackTileViewModel? other) => other is not null && Track.Id == other.Track.Id;
    public override bool Equals(object? obj) => Equals(obj as TrackTileViewModel);
    public override int GetHashCode() => Track.Id.GetHashCode();
}
