using DungeonSoundboard.Core.Models;

namespace DungeonSoundboard.Core.Services;

public enum PlaybackQueueAction
{
    None,
    Play,
    Stop
}

public readonly record struct PlaybackQueueDecision(PlaybackQueueAction Action, Guid? TrackId = null)
{
    public static PlaybackQueueDecision None => new(PlaybackQueueAction.None);

    public static PlaybackQueueDecision Stop => new(PlaybackQueueAction.Stop);

    public static PlaybackQueueDecision Play(Guid trackId) => new(PlaybackQueueAction.Play, trackId);
}

public sealed class PlaybackQueue
{
    private readonly List<Guid> _history = [];
    private readonly List<Guid> _shuffleUpcoming = [];
    private readonly HashSet<Guid> _knownTrackIds = [];
    private Guid? _playlistId;
    private bool _shuffleCycleInitialized;

    public void Reset(Guid? playlistId = null)
    {
        _playlistId = playlistId;
        _history.Clear();
        _shuffleUpcoming.Clear();
        _knownTrackIds.Clear();
        _shuffleCycleInitialized = false;
    }

    public void RecordDirectTransition(
        Guid playlistId,
        IReadOnlyList<Guid> trackIds,
        Guid? previousTrackId,
        Guid nextTrackId)
    {
        EnsureContext(playlistId, trackIds);
        if (previousTrackId is { } previous && previous != nextTrackId)
        {
            PushHistory(previous);
        }

        _shuffleUpcoming.Remove(nextTrackId);
        _shuffleCycleInitialized = false;
    }

    public PlaybackQueueDecision ManualNext(
        Guid playlistId,
        IReadOnlyList<Guid> trackIds,
        Guid? currentTrackId,
        bool shuffle,
        Random random)
    {
        EnsureContext(playlistId, trackIds);
        if (trackIds.Count == 0)
        {
            return PlaybackQueueDecision.None;
        }

        if (currentTrackId is { } current)
        {
            PushHistory(current);
        }

        if (shuffle)
        {
            var next = NextShuffle(trackIds, currentTrackId, random, allowRefill: true);
            return next is { } nextId ? PlaybackQueueDecision.Play(nextId) : PlaybackQueueDecision.None;
        }

        var currentIndex = currentTrackId is null ? -1 : IndexOf(trackIds, currentTrackId.Value);
        var nextIndex = currentIndex < 0 ? 0 : (currentIndex + 1) % trackIds.Count;
        return PlaybackQueueDecision.Play(trackIds[nextIndex]);
    }

    public PlaybackQueueDecision ManualPrevious(
        Guid playlistId,
        IReadOnlyList<Guid> trackIds,
        Guid? currentTrackId,
        bool shuffle,
        Random random)
    {
        EnsureContext(playlistId, trackIds);
        if (trackIds.Count == 0)
        {
            return PlaybackQueueDecision.None;
        }

        if (shuffle)
        {
            while (_history.Count > 0)
            {
                var previousIndex = _history.Count - 1;
                var previous = _history[previousIndex];
                _history.RemoveAt(previousIndex);
                if (!_knownTrackIds.Contains(previous) || previous == currentTrackId)
                {
                    continue;
                }

                if (currentTrackId is { } current && !_shuffleUpcoming.Contains(current))
                {
                    _shuffleUpcoming.Insert(0, current);
                }

                return PlaybackQueueDecision.Play(previous);
            }

            var fallback = NextShuffle(trackIds, currentTrackId, random, allowRefill: true);
            return fallback is { } fallbackId ? PlaybackQueueDecision.Play(fallbackId) : PlaybackQueueDecision.None;
        }

        var currentIndex = currentTrackId is null ? 0 : IndexOf(trackIds, currentTrackId.Value);
        var previousTrackIndex = currentIndex <= 0 ? trackIds.Count - 1 : currentIndex - 1;
        return PlaybackQueueDecision.Play(trackIds[previousTrackIndex]);
    }

    public PlaybackQueueDecision TrackFinished(
        Guid playlistId,
        IReadOnlyList<Guid> trackIds,
        Guid? currentTrackId,
        bool shuffle,
        RepeatMode repeatMode,
        Random random)
    {
        EnsureContext(playlistId, trackIds);
        if (trackIds.Count == 0 || currentTrackId is null)
        {
            return PlaybackQueueDecision.Stop;
        }

        if (repeatMode == RepeatMode.One)
        {
            return PlaybackQueueDecision.Play(currentTrackId.Value);
        }

        if (shuffle)
        {
            PushHistory(currentTrackId.Value);
            var next = NextShuffle(
                trackIds,
                currentTrackId,
                random,
                allowRefill: repeatMode == RepeatMode.All || !_shuffleCycleInitialized);
            return next is { } nextId ? PlaybackQueueDecision.Play(nextId) : PlaybackQueueDecision.Stop;
        }

        var currentIndex = IndexOf(trackIds, currentTrackId.Value);
        if (currentIndex < 0)
        {
            return PlaybackQueueDecision.Play(trackIds[0]);
        }

        if (currentIndex + 1 < trackIds.Count)
        {
            PushHistory(currentTrackId.Value);
            return PlaybackQueueDecision.Play(trackIds[currentIndex + 1]);
        }

        return repeatMode == RepeatMode.All
            ? PlaybackQueueDecision.Play(trackIds[0])
            : PlaybackQueueDecision.Stop;
    }

    private Guid? NextShuffle(
        IReadOnlyList<Guid> trackIds,
        Guid? currentTrackId,
        Random random,
        bool allowRefill)
    {
        _shuffleUpcoming.RemoveAll(id => !_knownTrackIds.Contains(id) || id == currentTrackId);
        if (_shuffleUpcoming.Count == 0 && allowRefill)
        {
            _shuffleUpcoming.AddRange(trackIds.Where(id => id != currentTrackId));
            Shuffle(_shuffleUpcoming, random);
            _shuffleCycleInitialized = true;
        }

        if (_shuffleUpcoming.Count == 0)
        {
            return trackIds.Count == 1 ? trackIds[0] : null;
        }

        var next = _shuffleUpcoming[0];
        _shuffleUpcoming.RemoveAt(0);
        return next;
    }

    private void EnsureContext(Guid playlistId, IReadOnlyList<Guid> trackIds)
    {
        if (_playlistId != playlistId)
        {
            Reset(playlistId);
        }

        var nextIds = trackIds.ToHashSet();
        if (!_knownTrackIds.SetEquals(nextIds))
        {
            _knownTrackIds.Clear();
            _knownTrackIds.UnionWith(nextIds);
            _history.RemoveAll(id => !nextIds.Contains(id));
            _shuffleUpcoming.RemoveAll(id => !nextIds.Contains(id));
            _shuffleCycleInitialized = false;
        }
    }

    private void PushHistory(Guid trackId)
    {
        if (_history.LastOrDefault() != trackId)
        {
            _history.Add(trackId);
        }
    }

    private static int IndexOf(IReadOnlyList<Guid> ids, Guid id)
    {
        for (var index = 0; index < ids.Count; index++)
        {
            if (ids[index] == id)
            {
                return index;
            }
        }

        return -1;
    }

    private static void Shuffle(IList<Guid> values, Random random)
    {
        for (var index = values.Count - 1; index > 0; index--)
        {
            var swapIndex = random.Next(index + 1);
            (values[index], values[swapIndex]) = (values[swapIndex], values[index]);
        }
    }
}
