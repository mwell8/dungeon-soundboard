using DungeonSoundboard.Core.Models;
using DungeonSoundboard.Core.Services;
using Xunit;

namespace DungeonSoundboard.Core.Tests;

public sealed class FileImportServiceTests : IDisposable
{
    private readonly string _root;
    private readonly FileImportService _service = new();

    public FileImportServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "DungeonSoundboardTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void ExpandsSupportedFilesFromFoldersAndSkipsUnsupportedFiles()
    {
        var nested = Path.Combine(_root, "nested");
        Directory.CreateDirectory(nested);
        var rain = Path.Combine(_root, "rain.mp3");
        var thunder = Path.Combine(nested, "thunder.wav");
        var notes = Path.Combine(nested, "notes.txt");
        File.WriteAllText(rain, "");
        File.WriteAllText(thunder, "");
        File.WriteAllText(notes, "");

        var files = _service.ExpandSupportedFiles([_root]);

        Assert.Contains(Path.GetFullPath(rain), files);
        Assert.Contains(Path.GetFullPath(thunder), files);
        Assert.DoesNotContain(Path.GetFullPath(notes), files);
    }

    [Fact]
    public void BuildUniqueTracksReportsDuplicatesByNormalizedPath()
    {
        var rain = Path.Combine(_root, "rain.mp3");
        File.WriteAllText(rain, "");
        var existing = new Track("Rain", Path.GetFullPath(rain), TrackRole.Music);

        var result = _service.BuildUniqueTracks([rain], TrackRole.Music, [existing], "Music Playlists");

        Assert.Empty(result.AddedTracks);
        Assert.NotNull(result.ConflictSummary);
        Assert.Equal(1, result.ConflictSummary.DuplicateCount);
    }

    [Fact]
    public void BuildUniqueTracksIgnoresInvalidExistingTrackPaths()
    {
        var rain = Path.Combine(_root, "rain.mp3");
        File.WriteAllText(rain, "");
        var existing = new Track("Broken legacy path", "bad\0path.mp3", TrackRole.Music);

        var result = _service.BuildUniqueTracks([rain], TrackRole.Music, [existing], "Music Playlists");

        Assert.Single(result.AddedTracks);
        Assert.Null(result.ConflictSummary);
    }

    [Fact]
    public void BuildUniqueTracksReportsUnsupportedAndSkippedInputCounts()
    {
        var rain = Path.Combine(_root, "rain.mp3");
        var notes = Path.Combine(_root, "notes.txt");
        var missing = Path.Combine(_root, "missing.wav");
        File.WriteAllText(rain, "");
        File.WriteAllText(notes, "");
        var existing = new Track("Rain", Path.GetFullPath(rain), TrackRole.Music);

        var result = _service.BuildUniqueTracks([rain, notes, missing], TrackRole.Music, [existing], "Music Playlists");

        Assert.Empty(result.AddedTracks);
        Assert.Equal(1, result.ConflictSummary?.DuplicateCount);
        Assert.Equal(3, result.ScanSummary.InputPathCount);
        Assert.Equal(1, result.ScanSummary.SupportedFileCount);
        Assert.Equal(1, result.ScanSummary.UnsupportedFileCount);
        Assert.Equal(1, result.ScanSummary.SkippedPathCount);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
