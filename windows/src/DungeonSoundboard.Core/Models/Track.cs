using System.Text.Json.Serialization;

namespace DungeonSoundboard.Core.Models;

[JsonConverter(typeof(JsonStringEnumConverter<TrackRole>))]
public enum TrackRole
{
    [JsonStringEnumMemberName("music")]
    Music,

    [JsonStringEnumMemberName("effect")]
    Effect
}

public sealed class Track : IEquatable<Track>
{
    public const double DefaultVolumeMultiplier = 1.0;
    public const double MinimumVolumeMultiplier = 0.0;
    public const double MaximumVolumeMultiplier = 2.0;

    private double _volumeMultiplier = DefaultVolumeMultiplier;

    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public string Path { get; set; } = "";
    public TrackRole Role { get; set; } = TrackRole.Music;
    public byte[]? BookmarkData { get; set; }

    public double VolumeMultiplier
    {
        get => _volumeMultiplier;
        set => _volumeMultiplier = NormalizedVolumeMultiplier(value);
    }

    public Track()
    {
    }

    public Track(
        string title,
        string path,
        TrackRole role = TrackRole.Music,
        Guid? id = null,
        byte[]? bookmarkData = null,
        double volumeMultiplier = DefaultVolumeMultiplier)
    {
        Id = id ?? Guid.NewGuid();
        Title = title;
        Path = path;
        Role = role;
        BookmarkData = bookmarkData;
        VolumeMultiplier = volumeMultiplier;
    }

    public static string NormalizedTitle(string value, string fallback)
    {
        var trimmed = value.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? fallback : trimmed;
    }

    public static double NormalizedVolumeMultiplier(double value)
    {
        if (!double.IsFinite(value))
        {
            return DefaultVolumeMultiplier;
        }

        return Math.Min(Math.Max(value, MinimumVolumeMultiplier), MaximumVolumeMultiplier);
    }

    public static double OutputVolume(
        double masterVolume,
        double trackMultiplier,
        double duckingMultiplier = 1.0)
    {
        var normalizedMaster = ClampUnit(masterVolume);
        var normalizedDucking = ClampUnit(duckingMultiplier);
        var output = normalizedMaster * NormalizedVolumeMultiplier(trackMultiplier) * normalizedDucking;
        return ClampUnit(output);
    }

    public double OutputVolume(double masterVolume, double duckingMultiplier = 1.0)
    {
        return OutputVolume(masterVolume, VolumeMultiplier, duckingMultiplier);
    }

    public static bool MoveTrack(IList<Track> tracks, Guid draggedId, Guid targetId)
    {
        if (draggedId == targetId)
        {
            return false;
        }

        var sourceIndex = IndexOf(tracks, draggedId);
        var targetIndex = IndexOf(tracks, targetId);
        if (sourceIndex < 0 || targetIndex < 0)
        {
            return false;
        }

        var track = tracks[sourceIndex];
        tracks.RemoveAt(sourceIndex);
        var destinationIndex = Math.Min(targetIndex, tracks.Count);
        tracks.Insert(destinationIndex, track);
        return true;
    }

    public bool Equals(Track? other)
    {
        if (other is null)
        {
            return false;
        }

        return Id == other.Id
            && Title == other.Title
            && Path == other.Path
            && Role == other.Role
            && VolumeMultiplier.Equals(other.VolumeMultiplier)
            && ByteArraysEqual(BookmarkData, other.BookmarkData);
    }

    public override bool Equals(object? obj) => Equals(obj as Track);

    public override int GetHashCode() => HashCode.Combine(Id, Title, Path, Role, VolumeMultiplier);

    private static double ClampUnit(double value)
    {
        if (!double.IsFinite(value))
        {
            return 0;
        }

        return Math.Min(Math.Max(value, 0), 1);
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

    private static bool ByteArraysEqual(byte[]? left, byte[]? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        return left.SequenceEqual(right);
    }
}
