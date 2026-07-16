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
    ImportConflictSummary? ConflictSummary,
    ImportScanSummary ScanSummary);

public sealed record ImportScanSummary(
    int InputPathCount,
    int SupportedFileCount,
    int UnsupportedFileCount,
    int SkippedPathCount);

public interface IFileImportService
{
    IReadOnlySet<string> SupportedExtensions { get; }

    IReadOnlyList<string> ExpandSupportedFiles(IEnumerable<string> paths);

    FileImportResult BuildUniqueTracks(
        IEnumerable<string> paths,
        TrackRole role,
        IEnumerable<Track> existingTracks,
        string targetName);

    Task<FileImportResult> BuildUniqueTracksAsync(
        IEnumerable<string> paths,
        TrackRole role,
        IEnumerable<Track> existingTracks,
        string targetName,
        CancellationToken cancellationToken = default)
    {
        var inputPaths = paths.ToArray();
        var existing = existingTracks.ToArray();
        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return BuildUniqueTracks(inputPaths, role, existing, targetName);
            },
            cancellationToken);
    }
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
        return Scan(paths).SupportedFiles;
    }

    public FileImportResult BuildUniqueTracks(
        IEnumerable<string> paths,
        TrackRole role,
        IEnumerable<Track> existingTracks,
        string targetName)
    {
        var scan = Scan(paths);
        var expanded = scan.SupportedFiles;
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

        return new FileImportResult(addedTracks, summary, scan.Summary);
    }

    public static string TrackIdentityKey(Track track)
    {
        return NormalizedPathKey(track.Path);
    }

    private bool IsSupportedFile(string path)
    {
        var extension = Path.GetExtension(path).TrimStart('.');
        return !string.IsNullOrWhiteSpace(extension) && SupportedExtensions.Contains(extension);
    }

    private ImportScan Scan(IEnumerable<string> paths)
    {
        var supported = new List<string>();
        var inputPathCount = 0;
        var unsupportedCount = 0;
        var skippedCount = 0;

        foreach (var path in paths)
        {
            inputPathCount++;
            if (!TryGetFullPath(path, out var fullPath))
            {
                skippedCount++;
                continue;
            }

            if (File.Exists(fullPath))
            {
                if (IsSupportedFile(fullPath))
                {
                    supported.Add(fullPath);
                }
                else
                {
                    unsupportedCount++;
                }

                continue;
            }

            if (Directory.Exists(fullPath))
            {
                var directoryScan = ScanDirectory(fullPath);
                supported.AddRange(directoryScan.SupportedFiles);
                unsupportedCount += directoryScan.UnsupportedFileCount;
                skippedCount += directoryScan.SkippedPathCount;
                continue;
            }

            skippedCount++;
        }

        return new ImportScan(
            supported,
            new ImportScanSummary(inputPathCount, supported.Count, unsupportedCount, skippedCount));
    }

    private ImportScan ScanDirectory(string directory)
    {
        try
        {
            var supported = new List<string>();
            var unsupportedCount = 0;
            foreach (var file in Directory.EnumerateFiles(
                         directory,
                         "*.*",
                         new EnumerationOptions
                         {
                             RecurseSubdirectories = true,
                             IgnoreInaccessible = true
                         }))
            {
                var normalized = TryGetFullPath(file, out var fullPath) ? fullPath : file;
                if (IsSupportedFile(normalized))
                {
                    supported.Add(normalized);
                }
                else
                {
                    unsupportedCount++;
                }
            }

            supported.Sort(StringComparer.CurrentCultureIgnoreCase);
            return new ImportScan(supported, new ImportScanSummary(1, supported.Count, unsupportedCount, 0));
        }
        catch (Exception ex) when (IsPathAccessException(ex))
        {
            return new ImportScan([], new ImportScanSummary(1, 0, 0, 1));
        }
    }

    private static string NormalizedPathKey(string path)
    {
        var normalized = TryGetFullPath(path, out var fullPath) ? fullPath : path;
        return normalized.Trim().ToLowerInvariant();
    }

    private static bool TryGetFullPath(string path, out string fullPath)
    {
        fullPath = string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            fullPath = Path.GetFullPath(path);
            return true;
        }
        catch (Exception ex) when (IsPathAccessException(ex))
        {
            return false;
        }
    }

    private static bool IsPathAccessException(Exception ex)
    {
        return ex is ArgumentException
            or IOException
            or NotSupportedException
            or PathTooLongException
            or UnauthorizedAccessException;
    }

    private sealed record ImportScan(IReadOnlyList<string> SupportedFiles, ImportScanSummary Summary)
    {
        public int UnsupportedFileCount => Summary.UnsupportedFileCount;

        public int SkippedPathCount => Summary.SkippedPathCount;
    }
}
