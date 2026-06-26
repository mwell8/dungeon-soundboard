namespace DungeonSoundboard.Core.Models;

public sealed class Playlist : IEquatable<Playlist>
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public List<Track> Tracks { get; set; } = [];

    public Playlist()
    {
    }

    public Playlist(string name, IEnumerable<Track>? tracks = null, Guid? id = null)
    {
        Id = id ?? Guid.NewGuid();
        Name = name;
        Tracks = tracks?.ToList() ?? [];
    }

    public bool Equals(Playlist? other)
    {
        if (other is null)
        {
            return false;
        }

        return Id == other.Id && Name == other.Name && Tracks.SequenceEqual(other.Tracks);
    }

    public override bool Equals(object? obj) => Equals(obj as Playlist);

    public override int GetHashCode() => HashCode.Combine(Id, Name);
}
