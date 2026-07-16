using DungeonSoundboard.Core.Models;
using DungeonSoundboard.Core.Services;
using Xunit;

namespace DungeonSoundboard.Core.Tests;

public sealed class PlaybackQueueTests
{
    private static readonly Guid PlaylistId = Guid.NewGuid();
    private static readonly Guid First = Guid.NewGuid();
    private static readonly Guid Second = Guid.NewGuid();
    private static readonly Guid Third = Guid.NewGuid();
    private static readonly Guid[] Tracks = [First, Second, Third];

    [Fact]
    public void ManualNavigationWrapsRegardlessOfRepeatMode()
    {
        var queue = new PlaybackQueue();

        Assert.Equal(First, queue.ManualNext(PlaylistId, Tracks, Third, false, new Random(1)).TrackId);
        Assert.Equal(Third, queue.ManualPrevious(PlaylistId, Tracks, First, false, new Random(1)).TrackId);
    }

    [Fact]
    public void NaturalCompletionUsesRepeatModeOnlyAtPlaylistBoundary()
    {
        var queue = new PlaybackQueue();

        Assert.Equal(PlaybackQueueAction.Stop, queue.TrackFinished(PlaylistId, Tracks, Third, false, RepeatMode.Off, new Random(1)).Action);
        Assert.Equal(First, queue.TrackFinished(PlaylistId, Tracks, Third, false, RepeatMode.All, new Random(1)).TrackId);
        Assert.Equal(Second, queue.TrackFinished(PlaylistId, Tracks, Second, false, RepeatMode.One, new Random(1)).TrackId);
    }

    [Fact]
    public void ShufflePlaysEveryOtherTrackBeforeStoppingWithRepeatOff()
    {
        var queue = new PlaybackQueue();
        var random = new Random(7);

        var secondDecision = queue.TrackFinished(PlaylistId, Tracks, First, true, RepeatMode.Off, random);
        var thirdDecision = queue.TrackFinished(PlaylistId, Tracks, secondDecision.TrackId, true, RepeatMode.Off, random);
        var stopped = queue.TrackFinished(PlaylistId, Tracks, thirdDecision.TrackId, true, RepeatMode.Off, random);

        Assert.NotEqual(First, secondDecision.TrackId);
        Assert.NotEqual(secondDecision.TrackId, thirdDecision.TrackId);
        Assert.Equal(PlaybackQueueAction.Stop, stopped.Action);
    }

    [Fact]
    public void ShufflePreviousUsesActualPlaybackHistory()
    {
        var queue = new PlaybackQueue();
        var random = new Random(11);
        var next = queue.ManualNext(PlaylistId, Tracks, First, true, random);

        var previous = queue.ManualPrevious(PlaylistId, Tracks, next.TrackId, true, random);

        Assert.Equal(First, previous.TrackId);
    }
}
