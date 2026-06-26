namespace DungeonSoundboard.Core.Models;

public sealed class EffectPlaylist : IEquatable<EffectPlaylist>
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public List<Track> Effects { get; set; } = [];

    public EffectPlaylist()
    {
    }

    public EffectPlaylist(string name, IEnumerable<Track>? effects = null, Guid? id = null)
    {
        Id = id ?? Guid.NewGuid();
        Name = name;
        Effects = effects?.ToList() ?? [];
    }

    public bool Equals(EffectPlaylist? other)
    {
        if (other is null)
        {
            return false;
        }

        return Id == other.Id && Name == other.Name && Effects.SequenceEqual(other.Effects);
    }

    public override bool Equals(object? obj) => Equals(obj as EffectPlaylist);

    public override int GetHashCode() => HashCode.Combine(Id, Name);
}
