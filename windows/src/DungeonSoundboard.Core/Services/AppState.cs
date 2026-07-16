using System.Globalization;
using DungeonSoundboard.Core.Models;

namespace DungeonSoundboard.Core.Services;

public sealed class AppState
{
    public List<Playlist> MusicPlaylists { get; set; } = [new Playlist(DefaultMusicPlaylistName)];
    public List<EffectPlaylist> EffectPlaylists { get; set; } = [new EffectPlaylist(DefaultSfxPlaylistName)];
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
            MusicPlaylists.Add(new Playlist(DefaultMusicPlaylistName));
        }

        if (EffectPlaylists.Count == 0)
        {
            EffectPlaylists.Add(new EffectPlaylist(DefaultSfxPlaylistName));
        }

        RemoveEmptyLegacyPlaceholderPlaylists();

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
        Hotkeys.RemoveMissingTrackBindings(MusicPlaylists, EffectPlaylists);
        Theme = ThemeRenderer.Sanitize(Theme);
    }

    public int RemoveMissingMusicTracks(Guid playlistId, Func<string, bool> fileExists)
    {
        ArgumentNullException.ThrowIfNull(fileExists);
        var playlist = MusicPlaylists.FirstOrDefault(candidate => candidate.Id == playlistId);
        if (playlist is null)
        {
            return 0;
        }

        var removed = playlist.Tracks.RemoveAll(track => !fileExists(track.Path));
        if (removed > 0)
        {
            EnsureDefaults();
        }

        return removed;
    }

    public int RemoveMissingEffectTracks(Guid playlistId, Func<string, bool> fileExists)
    {
        ArgumentNullException.ThrowIfNull(fileExists);
        var playlist = EffectPlaylists.FirstOrDefault(candidate => candidate.Id == playlistId);
        if (playlist is null)
        {
            return 0;
        }

        var removed = playlist.Effects.RemoveAll(track => !fileExists(track.Path));
        if (removed > 0)
        {
            EnsureDefaults();
        }

        return removed;
    }

    private static string DefaultMusicPlaylistName => DefaultLanguage == AppLanguage.Russian
        ? "Основной плейлист"
        : "Main Playlist";

    private static string DefaultSfxPlaylistName => DefaultLanguage == AppLanguage.Russian
        ? "SFX-мастер"
        : "SFX Master";

    private static AppLanguage DefaultLanguage => PlayerPreferences.DefaultLanguageForCulture(CultureInfo.CurrentUICulture);

    private void RemoveEmptyLegacyPlaceholderPlaylists()
    {
        if (!MusicPlaylists.Any(playlist => !IsEmptyLegacyPlaceholderPlaylist(playlist)))
        {
            return;
        }

        MusicPlaylists.RemoveAll(IsEmptyLegacyPlaceholderPlaylist);
    }

    private static bool IsEmptyLegacyPlaceholderPlaylist(Playlist playlist)
    {
        return playlist.Name == "Playlist 1" && playlist.Tracks.Count == 0;
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
    public double SidebarMusicSectionHeight { get; set; } = 220;
    public double CenterMusicSectionHeight { get; set; } = 260;
    public AppLanguage Language { get; set; } = DefaultLanguageForCulture(CultureInfo.CurrentUICulture);

    public static AppLanguage DefaultLanguageForCulture(CultureInfo culture)
    {
        return string.Equals(culture.TwoLetterISOLanguageName, "ru", StringComparison.OrdinalIgnoreCase)
            ? AppLanguage.Russian
            : AppLanguage.English;
    }

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

        return ClampUnit(value);
    }

    public static int NormalizeColumns(int value) => value is >= 2 and <= 4 ? value : 3;

    public static double NormalizeSectionHeight(double value, double fallback)
    {
        return double.IsFinite(value) ? Math.Clamp(value, 96, 720) : fallback;
    }

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
            && SidebarMusicSectionHeight.Equals(other.SidebarMusicSectionHeight)
            && CenterMusicSectionHeight.Equals(other.CenterMusicSectionHeight)
            && Language == other.Language;
    }

    public override bool Equals(object? obj) => Equals(obj as PlayerPreferences);

    public override int GetHashCode() => HashCode.Combine(Volume, EffectsVolume, RepeatMode, ShuffleEnabled, DuckingAmount, Language);
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
