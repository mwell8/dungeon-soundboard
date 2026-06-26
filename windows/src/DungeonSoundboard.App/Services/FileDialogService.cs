using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace DungeonSoundboard.App.Services;

public interface IFileDialogService
{
    Task<IReadOnlyList<string>> OpenAudioFilesAsync();
    Task<IReadOnlyList<string>> OpenAudioFolderAsync();
    Task<IReadOnlyList<string>> OpenBackgroundImageAsync();
}

public sealed class AvaloniaFileDialogService : IFileDialogService
{
    private static readonly FilePickerFileType AudioFiles = new("Audio files")
    {
        Patterns = ["*.mp3", "*.wav", "*.aiff", "*.aif", "*.m4a", "*.aac", "*.caf", "*.mp4"]
    };

    private static readonly FilePickerFileType ImageFiles = new("Image files")
    {
        Patterns = ["*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif", "*.webp"]
    };

    private readonly Window _owner;

    public AvaloniaFileDialogService(Window owner)
    {
        _owner = owner;
    }

    public async Task<IReadOnlyList<string>> OpenAudioFilesAsync()
    {
        var provider = _owner.StorageProvider;
        var files = await provider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose audio files",
            AllowMultiple = true,
            FileTypeFilter = [AudioFiles]
        });
        return files.Select(ToLocalPath).Where(path => path is not null).Cast<string>().ToList();
    }

    public async Task<IReadOnlyList<string>> OpenAudioFolderAsync()
    {
        var provider = _owner.StorageProvider;
        var folders = await provider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Choose folder with audio",
            AllowMultiple = false
        });
        return folders.Select(ToLocalPath).Where(path => path is not null).Cast<string>().ToList();
    }

    public async Task<IReadOnlyList<string>> OpenBackgroundImageAsync()
    {
        var provider = _owner.StorageProvider;
        var files = await provider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose background image",
            AllowMultiple = false,
            FileTypeFilter = [ImageFiles]
        });
        return files.Select(ToLocalPath).Where(path => path is not null).Cast<string>().ToList();
    }

    private static string? ToLocalPath(IStorageItem item)
    {
        return item.Path.IsAbsoluteUri ? item.Path.LocalPath : null;
    }
}
