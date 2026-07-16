using DungeonSoundboard.Core.Models;
using DungeonSoundboard.Core.Services;
using Xunit;

namespace DungeonSoundboard.Core.Tests;

public sealed class PlaylistMigrationTests
{
    [Fact]
    public void MigrationSplitsMusicAndEffectsAndPreservesSelectedPlaylistWhenPossible()
    {
        var musicId = Guid.NewGuid();
        var selected = musicId;
        var effectId = Guid.NewGuid();
        var legacy = new List<Playlist>
        {
            new(
                "Scene 1",
                [
                    new Track("Music A", "C:\\audio\\a.mp3", TrackRole.Music),
                    new Track("Thunder", "C:\\audio\\thunder.wav", TrackRole.Effect, id: effectId)
                ],
                musicId),
            new(
                "Scene 2",
                [
                    new Track("Music B", "C:\\audio\\b.mp3", TrackRole.Music),
                    new Track("Thunder Copy", "C:\\audio\\thunder.wav", TrackRole.Effect)
                ])
        };

        var result = PlaylistMigration.MigrateLegacyPlaylists(
            legacy,
            selected,
            "Main Playlist",
            "SFX Master");

        Assert.Equal(2, result.MusicPlaylists.Count);
        Assert.Single(result.MusicPlaylists[0].Tracks);
        Assert.Single(result.MusicPlaylists[1].Tracks);
        Assert.Single(result.EffectPlaylists);
        Assert.Single(result.EffectPlaylists[0].Effects);
        Assert.Equal(selected, result.SelectedMusicPlaylistId);
        Assert.Equal(result.EffectPlaylists[0].Id, result.SelectedEffectPlaylistId);
    }

    [Fact]
    public void MigrationCreatesDefaultsWhenLegacyIsEmpty()
    {
        var result = PlaylistMigration.MigrateLegacyPlaylists(
            [],
            null,
            "Main Playlist",
            "SFX Master");

        Assert.Single(result.MusicPlaylists);
        Assert.Equal("Main Playlist", result.MusicPlaylists[0].Name);
        Assert.Single(result.EffectPlaylists);
        Assert.Equal("SFX Master", result.EffectPlaylists[0].Name);
        Assert.Equal(result.MusicPlaylists[0].Id, result.SelectedMusicPlaylistId);
    }

    [Fact]
    public void MigrationDeduplicatesEffectPathsUsingWindowsPathNormalization()
    {
        var legacy = new List<Playlist>
        {
            new(
                "Scene 1",
                [
                    new Track("Thunder", "C:\\Audio\\Thunder.wav", TrackRole.Effect),
                    new Track("Thunder Copy", "c:/audio/thunder.wav", TrackRole.Effect)
                ])
        };

        var result = PlaylistMigration.MigrateLegacyPlaylists(
            legacy,
            null,
            "Main Playlist",
            "SFX Master");

        Assert.Single(result.EffectPlaylists[0].Effects);
        Assert.Equal("Thunder", result.EffectPlaylists[0].Effects[0].Title);
    }
}
