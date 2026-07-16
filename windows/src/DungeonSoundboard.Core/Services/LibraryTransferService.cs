using DungeonSoundboard.Core.Models;

namespace DungeonSoundboard.Core.Services;

public enum TrackTransferMode
{
    Move,
    Copy
}

public sealed record TrackTransferResult(
    bool Changed,
    IReadOnlyList<Guid> ResultTrackIds,
    IReadOnlyDictionary<Guid, Guid> CopiedTrackIds);

public static class LibraryTransferService
{
    public static TrackTransferResult TransferTracks(
        IList<Track> source,
        IList<Track> destination,
        IReadOnlyCollection<Guid> trackIds,
        Guid? targetTrackId,
        TrackTransferMode mode)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(trackIds);

        var requestedIds = trackIds.ToHashSet();
        var orderedSourceTracks = source.Where(track => requestedIds.Contains(track.Id)).ToList();
        if (orderedSourceTracks.Count == 0)
        {
            return new TrackTransferResult(false, [], new Dictionary<Guid, Guid>());
        }

        if (ReferenceEquals(source, destination) && targetTrackId is { } target && requestedIds.Contains(target))
        {
            return new TrackTransferResult(false, orderedSourceTracks.Select(track => track.Id).ToArray(), new Dictionary<Guid, Guid>());
        }

        if (targetTrackId is { } requestedTarget && IndexOf(destination, requestedTarget) < 0)
        {
            return new TrackTransferResult(false, [], new Dictionary<Guid, Guid>());
        }

        var copiedIds = new Dictionary<Guid, Guid>();
        List<Track> transferredTracks;
        if (mode == TrackTransferMode.Copy)
        {
            transferredTracks = orderedSourceTracks.Select(track => CloneTrack(track, copiedIds)).ToList();
        }
        else
        {
            transferredTracks = orderedSourceTracks;
            foreach (var track in orderedSourceTracks)
            {
                source.Remove(track);
            }
        }

        var insertionIndex = targetTrackId is { } targetId
            ? IndexOf(destination, targetId)
            : destination.Count;
        if (insertionIndex < 0)
        {
            insertionIndex = destination.Count;
        }

        foreach (var track in transferredTracks)
        {
            destination.Insert(insertionIndex++, track);
        }

        return new TrackTransferResult(
            true,
            transferredTracks.Select(track => track.Id).ToArray(),
            copiedIds);
    }

    private static Track CloneTrack(Track track, IDictionary<Guid, Guid> copiedIds)
    {
        var clone = new Track(
            track.Title,
            track.Path,
            track.Role,
            bookmarkData: track.BookmarkData?.ToArray(),
            volumeMultiplier: track.VolumeMultiplier);
        copiedIds[track.Id] = clone.Id;
        return clone;
    }

    private static int IndexOf(IEnumerable<Track> tracks, Guid id)
    {
        var index = 0;
        foreach (var track in tracks)
        {
            if (track.Id == id)
            {
                return index;
            }

            index++;
        }

        return -1;
    }
}
