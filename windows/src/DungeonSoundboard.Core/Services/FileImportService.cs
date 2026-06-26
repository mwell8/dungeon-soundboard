using DungeonSoundboard.Core.Models;

namespace DungeonSoundboard.Core.Services;

public sealed record ImportConflictSummary(
    string Target,
    int AttemptedCount,
    int AddedCount,
    int DuplicateCount,
    IReadOnlyList<string> DuplicateTitles);

public sealed record FileImportResult(
    IReadOnlyList<Track> AddedTracks,
    ImportConflictSummary? ConflictSummary);

public interface IFileImportService
{
    IReadOnlySet<string> SupportedExtensions { get; }

    IReadOnlyList<string> ExpandSupportedFiles(IEnumerable<string> paths);

    FileImportResult BuildUniqueTracks(
        IEnumerable<string> paths,
        TrackRole role,
        IEnumerable<Track> existingTracks,
        string targetName);
}

public sealed class FileImportService : IFileImportService
{
    private static readonly string[] OrderedSupportedExtensions =
    [
        "mp3",
        "wav",
        "aiff",
        "aif",
        "m4a",
        "aac",
        "caf",
        "mp4"
    ];

    public IReadOnlySet<string> SupportedExtensions { get; } =
        new HashSet<string>(OrderedSupportedExtensions, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> ExpandSupportedFiles(IEnumerable<string> paths)
    {
        var result = new List<string>();
        foreach (var path in paths)
        {
            if (File.Exists(path))
            {
                if (IsSupportedFile(path))
                {
                    result.Add(Path.GetFullPath(path));
                }

                continue;
            }

            if (!Directory.Exists(path))
            {
                continue;
            }

            result.AddRange(Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories)
                .Where(IsSupportedFile)
                .OrderBy(file => file, StringComparer.CurrentCultureIgnoreCase)
                .Select(Path.GetFullPath));
        }

        return result;
    }

    public FileImportResult BuildUniqueTracks(
        IEnumerable<string> paths,
        TrackRole role,
        IEnumerable<Track> existingTracks,
        string targetName)
    {
        var expanded = ExpandSupportedFiles(paths);
        var existingKeys = existingTracks.Select(TrackIdentityKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var addedTracks = new List<Track>();
        var duplicates = new List<Track>();

        foreach (var path in expanded)
        {
            var track = new Track(
                title: System.IO.Path.GetFileNameWithoutExtension(path),
                path: path,
                role: role);
            if (existingKeys.Add(TrackIdentityKey(track)))
            {
                addedTracks.Add(track);
            }
            else
            {
                duplicates.Add(track);
            }
        }

        ImportConflictSummary? summary = duplicates.Count == 0
            ? null
            : new ImportConflictSummary(
                targetName,
                expanded.Count,
                addedTracks.Count,
                duplicates.Count,
                duplicates.Take(8).Select(track => track.Title).ToList());

        return new FileImportResult(addedTracks, summary);
    }

    public static string TrackIdentityKey(Track track)
    {
        return Path.GetFullPath(track.Path).Trim().ToLowerInvariant();
    }

    private bool IsSupportedFile(string path)
    {
        var extension = Path.GetExtension(path).TrimStart('.');
        return !string.IsNullOrWhiteSpace(extension) && SupportedExtensions.Contains(extension);
    }
}
