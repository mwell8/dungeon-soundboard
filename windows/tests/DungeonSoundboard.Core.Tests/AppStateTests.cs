using DungeonSoundboard.Core.Models;
using DungeonSoundboard.Core.Services;
using Xunit;

namespace DungeonSoundboard.Core.Tests;

public sealed class AppStateTests
{
    [Fact]
    public void EnsureDefaultsRemovesEmptyDuplicateLegacyPlaylist()
    {
        var currentPlaylist = new Playlist("Main Playlist");
        var state = new AppState
        {
            MusicPlaylists = [new Playlist("Playlist 1"), currentPlaylist],
            Preferences = new PlayerPreferences
            {
                Language = AppLanguage.English
            }
        };

        state.EnsureDefaults();

        Assert.Single(state.MusicPlaylists);
        Assert.Same(currentPlaylist, state.MusicPlaylists[0]);
    }

    [Fact]
    public void EnsureDefaultsPreservesNonEmptyLegacyPlaylist()
    {
        var legacyPlaylist = new Playlist(
            "Playlist 1",
            [new Track("Existing track", "C:\\audio\\existing.mp3", TrackRole.Music)]);
        var state = new AppState
        {
            MusicPlaylists = [legacyPlaylist, new Playlist("Main Playlist")],
            Preferences = new PlayerPreferences
            {
                Language = AppLanguage.English
            }
        };

        state.EnsureDefaults();

        Assert.Contains(legacyPlaylist, state.MusicPlaylists);
    }

    [Fact]
    public void EnsureDefaultsPreservesSoleLegacyPlaylist()
    {
        var legacyPlaylist = new Playlist("Playlist 1");
        var state = new AppState
        {
            MusicPlaylists = [legacyPlaylist],
            Preferences = new PlayerPreferences
            {
                Language = AppLanguage.English
            }
        };

        state.EnsureDefaults();

        Assert.Single(state.MusicPlaylists);
        Assert.Same(legacyPlaylist, state.MusicPlaylists[0]);
    }

    [Fact]
    public void EnsureDefaultsDoesNotRenameUserPlaylistMatchingOldDefaultName()
    {
        var playlist = new Playlist("Main Playlist");
        var state = new AppState
        {
            MusicPlaylists = [playlist],
            Preferences = new PlayerPreferences { Language = AppLanguage.Russian }
        };

        state.EnsureDefaults();

        Assert.Equal("Main Playlist", playlist.Name);
    }

    [Fact]
    public void RemoveMissingMusicTracksRemovesOnlyMissingTracksAndCleansHotkeys()
    {
        var existing = new Track("Existing", "C:\\audio\\existing.mp3", TrackRole.Music);
        var missing = new Track("Missing", "C:\\audio\\missing.mp3", TrackRole.Music);
        var playlist = new Playlist("Battle", [existing, missing]);
        var state = new AppState
        {
            MusicPlaylists = [playlist],
            Preferences = new PlayerPreferences
            {
                Language = AppLanguage.English,
                SelectedMusicPlaylistId = playlist.Id
            },
            Hotkeys = new HotkeyConfiguration(
            [
                new HotkeyBinding(HotkeyAction.PlayMusicTrack(playlist.Id, existing.Id), new Hotkey(1, "A", HotkeyModifier.None)),
                new HotkeyBinding(HotkeyAction.PlayMusicTrack(playlist.Id, missing.Id), new Hotkey(2, "B", HotkeyModifier.None))
            ])
        };

        var removed = state.RemoveMissingMusicTracks(playlist.Id, path => path == existing.Path);

        Assert.Equal(1, removed);
        Assert.Single(playlist.Tracks);
        Assert.Same(existing, playlist.Tracks[0]);
        Assert.NotNull(state.Hotkeys.HotkeyFor(HotkeyAction.PlayMusicTrack(playlist.Id, existing.Id)));
        Assert.Null(state.Hotkeys.HotkeyFor(HotkeyAction.PlayMusicTrack(playlist.Id, missing.Id)));
    }
}
