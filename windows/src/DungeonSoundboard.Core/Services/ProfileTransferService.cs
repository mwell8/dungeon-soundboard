using System.IO.Compression;
using System.Text.Json;
using DungeonSoundboard.Core.Models;
using DungeonSoundboard.Core.Serialization;

namespace DungeonSoundboard.Core.Services;

public sealed record ProfileManifest(
    int SchemaVersion,
    string App,
    DateTimeOffset CreatedUtc,
    string? AppVersion = null);

public sealed record ProfileExportResult(string ZipPath);

public sealed record ProfileRestoreResult(string BackupDirectory);

public interface IProfileTransferService
{
    ProfileExportResult Export(string dataDirectory, string destinationZipPath, string? appVersion = null);

    ProfileRestoreResult Restore(string dataDirectory, string sourceZipPath);
}

public sealed class ProfileTransferService : IProfileTransferService
{
    public const int CurrentSchemaVersion = 1;
    public const string AppName = "DungeonSoundboard.Windows";
    private const long MaximumEntryBytes = 10 * 1024 * 1024;

    private const string ManifestFileName = "manifest.json";
    private const string PlaylistsFileName = "playlists.json";
    private const string PreferencesFileName = "preferences.json";
    private const string HotkeysFileName = "hotkeys.json";
    private const string ThemeFileName = "theme.json";

    private static readonly string[] StateFileNames =
    [
        PlaylistsFileName,
        PreferencesFileName,
        HotkeysFileName,
        ThemeFileName
    ];

    public ProfileExportResult Export(string dataDirectory, string destinationZipPath, string? appVersion = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationZipPath);

        var destinationDirectory = Path.GetDirectoryName(Path.GetFullPath(destinationZipPath));
        if (!string.IsNullOrWhiteSpace(destinationDirectory))
        {
            Directory.CreateDirectory(destinationDirectory);
        }

        if (File.Exists(destinationZipPath))
        {
            File.Delete(destinationZipPath);
        }

        using var archive = ZipFile.Open(destinationZipPath, ZipArchiveMode.Create);
        WriteEntry(
            archive,
            ManifestFileName,
            new ProfileManifest(CurrentSchemaVersion, AppName, DateTimeOffset.UtcNow, appVersion));

        foreach (var fileName in StateFileNames)
        {
            WriteEntry(archive, fileName, ReadStateJsonOrDefault(dataDirectory, fileName));
        }

        return new ProfileExportResult(destinationZipPath);
    }

    public ProfileRestoreResult Restore(string dataDirectory, string sourceZipPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceZipPath);

        using var archive = ZipFile.OpenRead(sourceZipPath);
        ValidateManifest(ReadEntry<ProfileManifest>(archive, ManifestFileName));

        var restoredFiles = StateFileNames
            .Select(fileName => new RestoredProfileFile(fileName, ReadRequiredEntryText(archive, fileName)))
            .ToArray();
        foreach (var file in restoredFiles)
        {
            ValidateStateJson(file.FileName, file.Json);
        }

        Directory.CreateDirectory(dataDirectory);
        var backupDirectory = CreateBackupDirectory(dataDirectory);
        foreach (var fileName in StateFileNames)
        {
            var currentPath = Path.Combine(dataDirectory, fileName);
            if (File.Exists(currentPath))
            {
                File.Copy(currentPath, Path.Combine(backupDirectory, fileName), overwrite: false);
            }
        }

        try
        {
            foreach (var file in restoredFiles)
            {
                WriteAtomic(Path.Combine(dataDirectory, file.FileName), file.Json);
            }
        }
        catch
        {
            RollBackRestore(dataDirectory, backupDirectory);
            throw;
        }

        return new ProfileRestoreResult(backupDirectory);
    }

    private static void ValidateManifest(ProfileManifest manifest)
    {
        if (manifest.SchemaVersion != CurrentSchemaVersion || manifest.App != AppName)
        {
            throw new InvalidDataException("The selected profile is not a supported Dungeon Soundboard Windows profile.");
        }
    }

    private static string ReadStateJsonOrDefault(string dataDirectory, string fileName)
    {
        var path = Path.Combine(dataDirectory, fileName);
        if (File.Exists(path))
        {
            var json = File.ReadAllText(path);
            ValidateStateJson(fileName, json);
            return json;
        }

        return fileName switch
        {
            PlaylistsFileName => Serialize(new PlaylistsDocument()),
            PreferencesFileName => Serialize(new PlayerPreferences()),
            HotkeysFileName => Serialize(HotkeyConfiguration.Defaults),
            ThemeFileName => Serialize(new ThemeDocument()),
            _ => throw new InvalidDataException($"Unknown profile file: {fileName}.")
        };
    }

    private static void ValidateStateJson(string fileName, string json)
    {
        try
        {
            switch (fileName)
            {
                case PlaylistsFileName:
                    _ = Deserialize<PlaylistsDocument>(json);
                    break;
                case PreferencesFileName:
                    _ = Deserialize<PlayerPreferences>(json);
                    break;
                case HotkeysFileName:
                    _ = Deserialize<HotkeyConfiguration>(json);
                    break;
                case ThemeFileName:
                    _ = Deserialize<ThemeDocument>(json);
                    break;
                default:
                    throw new InvalidDataException($"Unknown profile file: {fileName}.");
            }
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Profile file is not valid JSON: {fileName}.", ex);
        }
    }

    private static T Deserialize<T>(string json)
    {
        return JsonSerializer.Deserialize<T>(json, JsonDefaults.Options)
            ?? throw new InvalidDataException("Profile file is empty.");
    }

    private static void WriteEntry<T>(ZipArchive archive, string name, T value)
    {
        WriteEntry(archive, name, Serialize(value));
    }

    private static void WriteEntry(ZipArchive archive, string name, string json)
    {
        var entry = archive.CreateEntry(name);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(json);
    }

    private static T ReadEntry<T>(ZipArchive archive, string name)
    {
        return Deserialize<T>(ReadRequiredEntryText(archive, name));
    }

    private static string ReadRequiredEntryText(ZipArchive archive, string name)
    {
        var matchingEntries = archive.Entries.Where(entry => entry.FullName == name).ToArray();
        if (matchingEntries.Length != 1)
        {
            throw new InvalidDataException($"Profile must contain exactly one {name} entry.");
        }

        var entry = matchingEntries[0];
        if (entry.Length > MaximumEntryBytes)
        {
            throw new InvalidDataException($"Profile entry is too large: {name}.");
        }

        using var reader = new StreamReader(entry.Open());
        return reader.ReadToEnd();
    }

    private static string Serialize<T>(T value)
    {
        return JsonSerializer.Serialize(value, JsonDefaults.Options);
    }

    private static string CreateBackupDirectory(string dataDirectory)
    {
        var backupsRoot = Path.Combine(dataDirectory, "Backups");
        Directory.CreateDirectory(backupsRoot);
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var suffix = attempt == 0 ? "" : $"-{attempt}";
            var backupDirectory = Path.Combine(
                backupsRoot,
                $"profile-restore-{DateTime.UtcNow:yyyyMMdd-HHmmss}{suffix}");
            if (Directory.Exists(backupDirectory))
            {
                continue;
            }

            Directory.CreateDirectory(backupDirectory);
            return backupDirectory;
        }

        throw new IOException("Could not create a unique profile backup directory.");
    }

    private static void WriteAtomic(string path, string json)
    {
        var tempPath = $"{path}.tmp";
        File.WriteAllText(tempPath, json);
        if (File.Exists(path))
        {
            File.Replace(tempPath, path, null);
        }
        else
        {
            File.Move(tempPath, path);
        }
    }

    private static void RollBackRestore(string dataDirectory, string backupDirectory)
    {
        foreach (var fileName in StateFileNames)
        {
            var currentPath = Path.Combine(dataDirectory, fileName);
            var backupPath = Path.Combine(backupDirectory, fileName);
            if (File.Exists(backupPath))
            {
                File.Copy(backupPath, currentPath, overwrite: true);
            }
            else if (File.Exists(currentPath))
            {
                File.Delete(currentPath);
            }
        }
    }

    private sealed record RestoredProfileFile(string FileName, string Json);
}
