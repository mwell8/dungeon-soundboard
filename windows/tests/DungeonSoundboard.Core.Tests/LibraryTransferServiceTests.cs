using DungeonSoundboard.Core.Models;
using DungeonSoundboard.Core.Services;
using Xunit;

namespace DungeonSoundboard.Core.Tests;

public sealed class LibraryTransferServiceTests
{
    [Fact]
    public void MoveWithinPlaylistPreservesSourceOrderAndInsertsBeforeTarget()
    {
        var first = Track("First");
        var second = Track("Second");
        var third = Track("Third");
        var fourth = Track("Fourth");
        var tracks = new List<Track> { first, second, third, fourth };

        var result = LibraryTransferService.TransferTracks(
            tracks,
            tracks,
            [second.Id, third.Id],
            first.Id,
            TrackTransferMode.Move);

        Assert.True(result.Changed);
        Assert.Equal([second.Id, third.Id, first.Id, fourth.Id], tracks.Select(track => track.Id));
    }

    [Fact]
    public void MoveBetweenPlaylistsPreservesIdsAndMetadata()
    {
        var first = Track("First");
        first.VolumeMultiplier = 1.7;
        var source = new List<Track> { first };
        var destination = new List<Track>();

        var result = LibraryTransferService.TransferTracks(
            source,
            destination,
            [first.Id],
            null,
            TrackTransferMode.Move);

        Assert.True(result.Changed);
        Assert.Empty(source);
        Assert.Same(first, Assert.Single(destination));
        Assert.Equal(1.7, destination[0].VolumeMultiplier);
    }

    [Fact]
    public void CopyCreatesNewIdsWithoutChangingSource()
    {
        var first = Track("First");
        var source = new List<Track> { first };
        var destination = new List<Track>();

        var result = LibraryTransferService.TransferTracks(
            source,
            destination,
            [first.Id],
            null,
            TrackTransferMode.Copy);

        var copy = Assert.Single(destination);
        Assert.Single(source);
        Assert.NotEqual(first.Id, copy.Id);
        Assert.Equal(first.Title, copy.Title);
        Assert.Equal(copy.Id, result.CopiedTrackIds[first.Id]);
    }

    [Fact]
    public void MovingTrackUpdatesItsPlaylistHotkeyBinding()
    {
        var sourcePlaylistId = Guid.NewGuid();
        var destinationPlaylistId = Guid.NewGuid();
        var track = Track("Bound");
        var hotkey = new Hotkey(1, "B", HotkeyModifier.None);
        var configuration = new HotkeyConfiguration([
            new HotkeyBinding(HotkeyAction.PlayMusicTrack(sourcePlaylistId, track.Id), hotkey)
        ]);

        configuration.MoveTrackBindings(
            TrackRole.Music,
            sourcePlaylistId,
            destinationPlaylistId,
            new HashSet<Guid> { track.Id });

        Assert.Null(configuration.HotkeyFor(HotkeyAction.PlayMusicTrack(sourcePlaylistId, track.Id)));
        Assert.Equal(hotkey, configuration.HotkeyFor(HotkeyAction.PlayMusicTrack(destinationPlaylistId, track.Id)));
    }

    private static Track Track(string title) => new(title, $"C:\\audio\\{title}.wav", TrackRole.Music);
}
