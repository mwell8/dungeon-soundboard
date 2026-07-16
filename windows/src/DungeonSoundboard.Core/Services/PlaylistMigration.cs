using DungeonSoundboard.Core.Models;

namespace DungeonSoundboard.Core.Services;

public sealed record LegacyMigrationResult(
    List<Playlist> MusicPlaylists,
    List<EffectPlaylist> EffectPlaylists,
    Guid? SelectedMusicPlaylistId,
    Guid? SelectedEffectPlaylistId);

public static class PlaylistMigration
{
    public static LegacyMigrationResult MigrateLegacyPlaylists(
        IReadOnlyList<Playlist> legacyPlaylists,
        Guid? legacySelectedId,
        string defaultMusicPlaylistName,
        string defaultSfxPlaylistName)
    {
        var migratedMusicPlaylists = new List<Playlist>();
        var collectedEffects = new List<Track>();

        foreach (var playlist in legacyPlaylists)
        {
            var musicTracks = playlist.Tracks.Where(track => track.Role == TrackRole.Music).ToList();
            var effectTracks = playlist.Tracks.Where(track => track.Role == TrackRole.Effect);
            migratedMusicPlaylists.Add(new Playlist(playlist.Name, musicTracks, playlist.Id));
            collectedEffects.AddRange(effectTracks);
        }

        if (migratedMusicPlaylists.Count == 0)
        {
            migratedMusicPlaylists.Add(new Playlist(defaultMusicPlaylistName));
        }

        var deduplicatedEffects = DeduplicateTracksByPath(collectedEffects);
        var effectPlaylist = new EffectPlaylist(defaultSfxPlaylistName, deduplicatedEffects);
        var selectedMusicPlaylistId = legacySelectedId.HasValue
            && migratedMusicPlaylists.Any(playlist => playlist.Id == legacySelectedId.Value)
                ? legacySelectedId
                : migratedMusicPlaylists.FirstOrDefault()?.Id;

        return new LegacyMigrationResult(
            migratedMusicPlaylists,
            [effectPlaylist],
            selectedMusicPlaylistId,
            effectPlaylist.Id);
    }

    public static List<Track> DeduplicateTracksByPath(IEnumerable<Track> tracks)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<Track>();

        foreach (var track in tracks)
        {
            var normalizedPath = NormalizePathKey(track.Path);
            if (string.IsNullOrWhiteSpace(normalizedPath))
            {
                continue;
            }

            if (seen.Add(normalizedPath))
            {
                result.Add(track);
            }
        }

        return result;
    }

    private static string NormalizePathKey(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "";
        }

        var trimmed = path.Trim();
        try
        {
            trimmed = Path.GetFullPath(trimmed);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException or PathTooLongException or UnauthorizedAccessException)
        {
        }

        return trimmed.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
    }
}
