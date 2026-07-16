using DungeonSoundboard.Core.Models;
using DungeonSoundboard.Core.Services;
using Xunit;

namespace DungeonSoundboard.Core.Tests;

public sealed class StorageServiceTests : IDisposable
{
    private readonly string _root;

    public StorageServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "DungeonSoundboardStorageTests", Guid.NewGuid().ToString("N"));
    }

    [Fact]
    public void SaveAndLoadRoundTripsState()
    {
        var storage = new JsonFileStorageService(_root);
        var music = new Playlist("Battle", [new Track("Drums", "C:\\audio\\drums.mp3", TrackRole.Music)]);
        var sfx = new EffectPlaylist("Combat SFX", [new Track("Sword", "C:\\audio\\sword.wav", TrackRole.Effect)]);
        var state = new AppState
        {
            MusicPlaylists = [music],
            EffectPlaylists = [sfx],
            Preferences = new PlayerPreferences
            {
                Volume = 0.7,
                EffectsVolume = 0.6,
                RepeatMode = RepeatMode.All,
                ShuffleEnabled = true,
                Language = AppLanguage.Russian,
                SelectedMusicPlaylistId = music.Id,
                SelectedEffectPlaylistId = sfx.Id,
                DuckingAmount = 0.45
            },
            Hotkeys = HotkeyConfiguration.Defaults,
            Theme = ThemeRenderer.ThemeFor(ThemePreset.MoonlitCrypt)
        };

        storage.Save(state);
        var loaded = storage.Load();

        Assert.Equal("Battle", loaded.MusicPlaylists[0].Name);
        Assert.Equal("Combat SFX", loaded.EffectPlaylists[0].Name);
        Assert.Equal(RepeatMode.All, loaded.Preferences.RepeatMode);
        Assert.True(loaded.Preferences.ShuffleEnabled);
        Assert.Equal(AppLanguage.Russian, loaded.Preferences.Language);
        Assert.Equal(ThemePreset.MoonlitCrypt, loaded.Theme.Preset);
    }

    [Fact]
    public void CorruptJsonFallsBackToDefaults()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "playlists.json"), "{ broken");

        var storage = new JsonFileStorageService(_root);
        var state = storage.Load();
        var expectedDefaults = new AppState();
        expectedDefaults.EnsureDefaults();

        Assert.Single(state.MusicPlaylists);
        Assert.Single(state.EffectPlaylists);
        Assert.Equal(expectedDefaults.MusicPlaylists[0].Name, state.MusicPlaylists[0].Name);
        Assert.Single(storage.RecoveryWarnings);
        var recoveryFile = Assert.Single(Directory.GetFiles(Path.Combine(_root, "Recovery"), "playlists.json.*.corrupt"));
        Assert.Equal("{ broken", File.ReadAllText(recoveryFile));
        Assert.False(File.Exists(Path.Combine(_root, "playlists.json")));
    }

    [Fact]
    public void MissingAudioFileStaysInPlaylist()
    {
        var storage = new JsonFileStorageService(_root);
        var missingPath = Path.Combine(_root, "missing.mp3");
        var music = new Playlist("Battle", [new Track("Missing", missingPath, TrackRole.Music)]);
        storage.Save(new AppState { MusicPlaylists = [music] });

        var loaded = storage.Load();

        Assert.False(File.Exists(missingPath));
        Assert.Single(loaded.MusicPlaylists[0].Tracks);
        Assert.Equal("Missing", loaded.MusicPlaylists[0].Tracks[0].Title);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
