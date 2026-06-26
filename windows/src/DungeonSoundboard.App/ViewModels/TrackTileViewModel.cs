using DungeonSoundboard.Core.Models;

namespace DungeonSoundboard.App.ViewModels;

public sealed class TrackTileViewModel(
    Track track,
    string? hotkeyText,
    bool isCurrent,
    bool isFileMissing)
{
    public Track Track { get; } = track;
    public string Title => Track.Title;
    public string Path => Track.Path;
    public double VolumeMultiplier => Track.VolumeMultiplier;
    public string HotkeyText { get; } = hotkeyText ?? "";
    public bool HasHotkey => !string.IsNullOrWhiteSpace(HotkeyText);
    public bool IsCurrent { get; } = isCurrent;
    public bool IsFileMissing { get; } = isFileMissing;
    public string FileStatus => IsFileMissing ? "Missing file" : "File available";
}
