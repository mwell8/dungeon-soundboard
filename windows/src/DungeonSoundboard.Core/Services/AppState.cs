using DungeonSoundboard.Core.Models;

namespace DungeonSoundboard.Core.Services;

public sealed class AppState
{
    public List<Playlist> MusicPlaylists { get; set; } = [new Playlist("Main Playlist")];
    public List<EffectPlaylist> EffectPlaylists { get; set; } = [new EffectPlaylist("SFX Master")];
    public PlayerPreferences Preferences { get; set; } = new();
    public HotkeyConfiguration Hotkeys { get; set; } = HotkeyConfiguration.Defaults;
    public AppTheme Theme { get; set; } = ThemeRenderer.DefaultTheme;
    public List<CustomThemePreset> CustomThemePresets { get; set; } = [];

    public void EnsureDefaults()
    {
        MusicPlaylists ??= [];
        EffectPlaylists ??= [];
        Preferences ??= new PlayerPreferences();
        Hotkeys ??= HotkeyConfiguration.Defaults;
        Theme ??= ThemeRenderer.DefaultTheme;
        CustomThemePresets ??= [];

        if (MusicPlaylists.Count == 0)
        {
            MusicPlaylists.Add(new Playlist("Main Playlist"));
        }

        if (EffectPlaylists.Count == 0)
        {
            EffectPlaylists.Add(new EffectPlaylist("SFX Master"));
        }

        if (Preferences.SelectedMusicPlaylistId is null
            || MusicPlaylists.All(playlist => playlist.Id != Preferences.SelectedMusicPlaylistId.Value))
        {
            Preferences.SelectedMusicPlaylistId = MusicPlaylists.First().Id;
        }

        if (Preferences.SelectedEffectPlaylistId is null
            || EffectPlaylists.All(playlist => playlist.Id != Preferences.SelectedEffectPlaylistId.Value))
        {
            Preferences.SelectedEffectPlaylistId = EffectPlaylists.First().Id;
        }

        Preferences.Volume = PlayerPreferences.ClampUnit(Preferences.Volume);
        Preferences.EffectsVolume = PlayerPreferences.ClampUnit(Preferences.EffectsVolume);
        Preferences.DuckingAmount = PlayerPreferences.ClampDucking(Preferences.DuckingAmount);
        Preferences.MusicColumns = PlayerPreferences.NormalizeColumns(Preferences.MusicColumns);
        Preferences.EffectsColumns = PlayerPreferences.NormalizeColumns(Preferences.EffectsColumns);
        Hotkeys.MigrateLegacyDeleteStopAllDefault();
        Hotkeys.RemoveMissingTrackBindings(
            MusicPlaylists.Select(playlist => playlist.Id).ToHashSet(),
            MusicPlaylists.SelectMany(playlist => playlist.Tracks).Select(track => track.Id).ToHashSet(),
            EffectPlaylists.Select(playlist => playlist.Id).ToHashSet(),
            EffectPlaylists.SelectMany(playlist => playlist.Effects).Select(track => track.Id).ToHashSet());
        Theme = ThemeRenderer.Sanitize(Theme);
    }
}

public sealed class PlayerPreferences : IEquatable<PlayerPreferences>
{
    public double Volume { get; set; } = 0.8;
    public double EffectsVolume { get; set; } = 0.8;
    public RepeatMode RepeatMode { get; set; } = RepeatMode.Off;
    public bool ShuffleEnabled { get; set; }
    public bool MusicFadeOutOnPauseEnabled { get; set; }
    public Guid? SelectedMusicPlaylistId { get; set; }
    public Guid? SelectedEffectPlaylistId { get; set; }
    public double DuckingAmount { get; set; } = 0.55;
    public int MusicColumns { get; set; } = 3;
    public int EffectsColumns { get; set; } = 3;
    public bool SentryTelemetryEnabled { get; set; }
    public string SentryDsn { get; set; } = "";

    public static double ClampUnit(double value)
    {
        if (!double.IsFinite(value))
        {
            return 0.8;
        }

        return Math.Min(Math.Max(value, 0), 1);
    }

    public static double ClampDucking(double value)
    {
        if (!double.IsFinite(value))
        {
            return 0.55;
        }

        return Math.Min(Math.Max(value, 0.2), 1.0);
    }

    public static int NormalizeColumns(int value) => value is >= 2 and <= 4 ? value : 3;

    public bool Equals(PlayerPreferences? other)
    {
        return other is not null
            && Volume.Equals(other.Volume)
            && EffectsVolume.Equals(other.EffectsVolume)
            && RepeatMode == other.RepeatMode
            && ShuffleEnabled == other.ShuffleEnabled
            && MusicFadeOutOnPauseEnabled == other.MusicFadeOutOnPauseEnabled
            && SelectedMusicPlaylistId == other.SelectedMusicPlaylistId
            && SelectedEffectPlaylistId == other.SelectedEffectPlaylistId
            && DuckingAmount.Equals(other.DuckingAmount)
            && MusicColumns == other.MusicColumns
            && EffectsColumns == other.EffectsColumns
            && SentryTelemetryEnabled == other.SentryTelemetryEnabled
            && SentryDsn == other.SentryDsn;
    }

    public override bool Equals(object? obj) => Equals(obj as PlayerPreferences);

    public override int GetHashCode() => HashCode.Combine(Volume, EffectsVolume, RepeatMode, ShuffleEnabled, DuckingAmount);
}

public sealed class PlaylistsDocument
{
    public List<Playlist> MusicPlaylists { get; set; } = [];
    public List<EffectPlaylist> EffectPlaylists { get; set; } = [];
}

public sealed class ThemeDocument
{
    public AppTheme Theme { get; set; } = ThemeRenderer.DefaultTheme;
    public List<CustomThemePreset> CustomPresets { get; set; } = [];
}
