using DungeonSoundboard.Core.Models;
using Xunit;

namespace DungeonSoundboard.Core.Tests;

public sealed class PlayerDefaultsKeysTests
{
    [Fact]
    public void MigrationCriticalKeysAreStable()
    {
        Assert.Equal("macos_dungeon_soundboard_playlists", PlayerDefaultsKeys.LegacyPlaylists);
        Assert.Equal("macos_dungeon_soundboard_migration_v2_completed", PlayerDefaultsKeys.MigrationCompleted);
        Assert.Equal("macos_dungeon_soundboard_selected_playlist_id", PlayerDefaultsKeys.LegacySelectedPlaylistId);
    }

    [Fact]
    public void CurrentPlaybackPreferenceKeysAreStable()
    {
        Assert.Equal("macos_dungeon_soundboard_music_playlists", PlayerDefaultsKeys.MusicPlaylists);
        Assert.Equal("macos_dungeon_soundboard_effect_playlists", PlayerDefaultsKeys.EffectPlaylists);
        Assert.Equal("macos_dungeon_soundboard_repeat_mode", PlayerDefaultsKeys.RepeatMode);
        Assert.Equal("macos_dungeon_soundboard_shuffle_enabled", PlayerDefaultsKeys.ShuffleEnabled);
        Assert.Equal("macos_dungeon_soundboard_selected_music_playlist_id", PlayerDefaultsKeys.SelectedMusicPlaylistId);
        Assert.Equal("macos_dungeon_soundboard_selected_effect_playlist_id", PlayerDefaultsKeys.SelectedEffectPlaylistId);
        Assert.Equal("macos_dungeon_soundboard_theme", PlayerDefaultsKeys.Theme);
        Assert.Equal("macos_dungeon_soundboard_custom_theme_presets", PlayerDefaultsKeys.CustomThemePresets);
        Assert.Equal("macos_dungeon_soundboard_hotkey_bindings", PlayerDefaultsKeys.HotkeyBindings);
    }
}
