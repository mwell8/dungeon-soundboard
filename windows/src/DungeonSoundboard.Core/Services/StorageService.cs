using System.Text.Json;
using DungeonSoundboard.Core.Models;
using DungeonSoundboard.Core.Serialization;

namespace DungeonSoundboard.Core.Services;

public interface IStorageService
{
    string DataDirectory { get; }

    AppState Load();

    void Save(AppState state);
}

public sealed class JsonFileStorageService : IStorageService
{
    private const string AppFolderName = "DungeonSoundboard";

    public JsonFileStorageService(string? dataDirectory = null)
    {
        DataDirectory = dataDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            AppFolderName);
    }

    public string DataDirectory { get; }

    public string PlaylistsPath => Path.Combine(DataDirectory, "playlists.json");
    public string PreferencesPath => Path.Combine(DataDirectory, "preferences.json");
    public string HotkeysPath => Path.Combine(DataDirectory, "hotkeys.json");
    public string ThemePath => Path.Combine(DataDirectory, "theme.json");

    public AppState Load()
    {
        Directory.CreateDirectory(DataDirectory);

        var state = new AppState();
        var playlists = ReadOrDefault(PlaylistsPath, new PlaylistsDocument());
        if (playlists.MusicPlaylists.Count > 0)
        {
            state.MusicPlaylists = playlists.MusicPlaylists;
        }

        if (playlists.EffectPlaylists.Count > 0)
        {
            state.EffectPlaylists = playlists.EffectPlaylists;
        }

        state.Preferences = ReadOrDefault(PreferencesPath, new PlayerPreferences());
        state.Hotkeys = ReadOrDefault(HotkeysPath, HotkeyConfiguration.Defaults);

        var themeDocument = ReadOrDefault(ThemePath, new ThemeDocument());
        state.Theme = themeDocument.Theme;
        state.CustomThemePresets = themeDocument.CustomPresets;
        state.EnsureDefaults();
        return state;
    }

    public void Save(AppState state)
    {
        Directory.CreateDirectory(DataDirectory);
        state.EnsureDefaults();

        WriteAtomic(
            PlaylistsPath,
            new PlaylistsDocument
            {
                MusicPlaylists = state.MusicPlaylists,
                EffectPlaylists = state.EffectPlaylists
            });
        WriteAtomic(PreferencesPath, state.Preferences);
        WriteAtomic(HotkeysPath, state.Hotkeys);
        WriteAtomic(
            ThemePath,
            new ThemeDocument
            {
                Theme = state.Theme,
                CustomPresets = state.CustomThemePresets
            });
    }

    private static T ReadOrDefault<T>(string path, T fallback)
    {
        if (!File.Exists(path))
        {
            return fallback;
        }

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<T>(json, JsonDefaults.Options) ?? fallback;
        }
        catch (JsonException)
        {
            return fallback;
        }
        catch (IOException)
        {
            return fallback;
        }
        catch (UnauthorizedAccessException)
        {
            return fallback;
        }
    }

    private static void WriteAtomic<T>(string path, T value)
    {
        var tempPath = $"{path}.tmp";
        var json = JsonSerializer.Serialize(value, JsonDefaults.Options);
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
}
