namespace DungeonSoundboard.App.ViewModels;

public sealed class SettingsWindowViewModel
{
    public SettingsWindowViewModel(MainWindowViewModel main)
    {
        Main = main;
    }

    public MainWindowViewModel Main { get; }

    public string SupportedAudioExtensions => ".mp3, .wav, .aiff, .aif, .m4a, .aac, .caf, .mp4";

    public string StorageFiles => "playlists.json, preferences.json, hotkeys.json, theme.json";
}
