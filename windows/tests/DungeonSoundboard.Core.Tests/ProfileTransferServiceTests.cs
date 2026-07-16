using System.IO.Compression;
using System.Text.Json;
using DungeonSoundboard.Core.Models;
using DungeonSoundboard.Core.Serialization;
using DungeonSoundboard.Core.Services;
using Xunit;

namespace DungeonSoundboard.Core.Tests;

public sealed class ProfileTransferServiceTests : IDisposable
{
    private readonly string _root;
    private readonly ProfileTransferService _service = new();

    public ProfileTransferServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "DungeonSoundboardProfileTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void ExportCreatesProfileZipWithManifestAndStateFiles()
    {
        var dataDirectory = Path.Combine(_root, "data");
        var exportPath = Path.Combine(_root, "profile.zip");
        SaveState(dataDirectory, StateWith("Battle", "Sword"));

        var result = _service.Export(dataDirectory, exportPath, "1.2.3");

        Assert.Equal(exportPath, result.ZipPath);
        using var archive = ZipFile.OpenRead(exportPath);
        Assert.NotNull(archive.GetEntry("manifest.json"));
        Assert.NotNull(archive.GetEntry("playlists.json"));
        Assert.NotNull(archive.GetEntry("preferences.json"));
        Assert.NotNull(archive.GetEntry("hotkeys.json"));
        Assert.NotNull(archive.GetEntry("theme.json"));

        var manifest = ReadEntry<ProfileManifest>(archive, "manifest.json");
        Assert.Equal(1, manifest.SchemaVersion);
        Assert.Equal("DungeonSoundboard.Windows", manifest.App);
        Assert.Equal("1.2.3", manifest.AppVersion);
    }

    [Fact]
    public void RestoreValidProfileReplacesStateAndCreatesBackup()
    {
        var sourceDirectory = Path.Combine(_root, "source");
        var dataDirectory = Path.Combine(_root, "data");
        var exportPath = Path.Combine(_root, "profile.zip");
        SaveState(sourceDirectory, StateWith("Restored Music", "Restored SFX"));
        SaveState(dataDirectory, StateWith("Current Music", "Current SFX"));
        _service.Export(sourceDirectory, exportPath);

        var result = _service.Restore(dataDirectory, exportPath);

        var loaded = new JsonFileStorageService(dataDirectory).Load();
        Assert.Equal("Restored Music", loaded.MusicPlaylists[0].Name);
        Assert.Equal("Restored SFX", loaded.EffectPlaylists[0].Name);
        Assert.True(Directory.Exists(result.BackupDirectory));

        var backup = new JsonFileStorageService(result.BackupDirectory).Load();
        Assert.Equal("Current Music", backup.MusicPlaylists[0].Name);
        Assert.Equal("Current SFX", backup.EffectPlaylists[0].Name);
    }

    [Fact]
    public void RestoreInvalidProfileLeavesCurrentFilesUntouched()
    {
        var dataDirectory = Path.Combine(_root, "data");
        var exportPath = Path.Combine(_root, "invalid.zip");
        SaveState(dataDirectory, StateWith("Current Music", "Current SFX"));
        using (var archive = ZipFile.Open(exportPath, ZipArchiveMode.Create))
        {
            WriteEntry(archive, "manifest.json", "{ \"schemaVersion\": 1, \"app\": \"DungeonSoundboard.Windows\" }");
            WriteEntry(archive, "playlists.json", "{ broken");
            WriteEntry(archive, "preferences.json", "{}");
            WriteEntry(archive, "hotkeys.json", "{ \"bindings\": [] }");
            WriteEntry(archive, "theme.json", "{}");
        }

        Assert.Throws<InvalidDataException>(() => _service.Restore(dataDirectory, exportPath));

        var loaded = new JsonFileStorageService(dataDirectory).Load();
        Assert.Equal("Current Music", loaded.MusicPlaylists[0].Name);
        Assert.Equal("Current SFX", loaded.EffectPlaylists[0].Name);
        Assert.False(Directory.Exists(Path.Combine(dataDirectory, "Backups")));
    }

    [Fact]
    public void ExportRestoreRoundTripsPreferencesHotkeysAndTheme()
    {
        var sourceDirectory = Path.Combine(_root, "source");
        var dataDirectory = Path.Combine(_root, "data");
        var exportPath = Path.Combine(_root, "profile.zip");
        var state = StateWith("Battle", "Spells");
        state.Preferences.Volume = 0.42;
        state.Preferences.EffectsVolume = 0.64;
        state.Preferences.RepeatMode = RepeatMode.All;
        state.Hotkeys = new HotkeyConfiguration(
        [
            new HotkeyBinding(HotkeyAction.PlayPause, new Hotkey(HotkeyConfiguration.SpaceKeyCode, "Space", HotkeyModifier.None))
        ]);
        state.Theme = ThemeRenderer.ThemeFor(ThemePreset.ForestMist);
        SaveState(sourceDirectory, state);
        SaveState(dataDirectory, StateWith("Current", "Current SFX"));

        _service.Export(sourceDirectory, exportPath);
        _service.Restore(dataDirectory, exportPath);

        var loaded = new JsonFileStorageService(dataDirectory).Load();
        Assert.Equal("Battle", loaded.MusicPlaylists[0].Name);
        Assert.Equal("Spells", loaded.EffectPlaylists[0].Name);
        Assert.Equal(0.42, loaded.Preferences.Volume);
        Assert.Equal(0.64, loaded.Preferences.EffectsVolume);
        Assert.Equal(RepeatMode.All, loaded.Preferences.RepeatMode);
        Assert.Equal(ThemePreset.ForestMist, loaded.Theme.Preset);
        Assert.Equal(HotkeyConfiguration.SpaceKeyCode, loaded.Hotkeys.HotkeyFor(HotkeyAction.PlayPause)?.KeyCode);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static AppState StateWith(string musicName, string sfxName)
    {
        var music = new Playlist(musicName, [new Track("Theme", "C:\\audio\\theme.mp3", TrackRole.Music)]);
        var sfx = new EffectPlaylist(sfxName, [new Track("Hit", "C:\\audio\\hit.wav", TrackRole.Effect)]);
        return new AppState
        {
            MusicPlaylists = [music],
            EffectPlaylists = [sfx],
            Preferences = new PlayerPreferences
            {
                Language = AppLanguage.English,
                SelectedMusicPlaylistId = music.Id,
                SelectedEffectPlaylistId = sfx.Id
            }
        };
    }

    private static void SaveState(string dataDirectory, AppState state)
    {
        new JsonFileStorageService(dataDirectory).Save(state);
    }

    private static T ReadEntry<T>(ZipArchive archive, string name)
    {
        var entry = archive.GetEntry(name) ?? throw new InvalidDataException(name);
        using var stream = entry.Open();
        return JsonSerializer.Deserialize<T>(stream, JsonDefaults.Options)
            ?? throw new InvalidDataException(name);
    }

    private static void WriteEntry(ZipArchive archive, string name, string json)
    {
        var entry = archive.CreateEntry(name);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(json);
    }
}
