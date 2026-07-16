using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace DungeonSoundboard.App.Services;

public interface IFileDialogService
{
    Task<IReadOnlyList<string>> OpenAudioFilesAsync(FileDialogLabels labels);
    Task<IReadOnlyList<string>> OpenAudioFolderAsync(FileDialogLabels labels);
    Task<IReadOnlyList<string>> OpenBackgroundImageAsync(FileDialogLabels labels);
    Task<string?> SaveProfileZipAsync(FileDialogLabels labels);
    Task<string?> OpenProfileZipAsync(FileDialogLabels labels);
}

public sealed record FileDialogLabels(
    string AudioFilesTypeName,
    string ImageFilesTypeName,
    string ProfileZipTypeName,
    string ChooseAudioFilesTitle,
    string ChooseAudioFolderTitle,
    string ChooseBackgroundImageTitle,
    string ExportProfileTitle,
    string RestoreProfileTitle);

public sealed class AvaloniaFileDialogService : IFileDialogService
{
    private static readonly string[] AudioFilePatterns = ["*.mp3", "*.wav", "*.aiff", "*.aif", "*.m4a", "*.aac", "*.caf", "*.mp4"];

    private static readonly string[] ImageFilePatterns = ["*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif", "*.webp"];

    private readonly Window _owner;

    public AvaloniaFileDialogService(Window owner)
    {
        _owner = owner;
    }

    public async Task<IReadOnlyList<string>> OpenAudioFilesAsync(FileDialogLabels labels)
    {
        var provider = _owner.StorageProvider;
        var files = await provider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = labels.ChooseAudioFilesTitle,
            AllowMultiple = true,
            FileTypeFilter = [AudioFiles(labels)]
        });
        return files.Select(ToLocalPath).Where(path => path is not null).Cast<string>().ToList();
    }

    public async Task<string?> SaveProfileZipAsync(FileDialogLabels labels)
    {
        var provider = _owner.StorageProvider;
        var file = await provider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = labels.ExportProfileTitle,
            SuggestedFileName = $"DungeonSoundboard-profile-{DateTime.Now:yyyyMMdd-HHmmss}.zip",
            DefaultExtension = "zip",
            FileTypeChoices = [ProfileZip(labels)]
        });
        return file is null ? null : ToLocalPath(file);
    }

    public async Task<string?> OpenProfileZipAsync(FileDialogLabels labels)
    {
        var provider = _owner.StorageProvider;
        var files = await provider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = labels.RestoreProfileTitle,
            AllowMultiple = false,
            FileTypeFilter = [ProfileZip(labels)]
        });
        return files.Select(ToLocalPath).FirstOrDefault(path => path is not null);
    }

    public async Task<IReadOnlyList<string>> OpenAudioFolderAsync(FileDialogLabels labels)
    {
        var provider = _owner.StorageProvider;
        var folders = await provider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = labels.ChooseAudioFolderTitle,
            AllowMultiple = false
        });
        return folders.Select(ToLocalPath).Where(path => path is not null).Cast<string>().ToList();
    }

    public async Task<IReadOnlyList<string>> OpenBackgroundImageAsync(FileDialogLabels labels)
    {
        var provider = _owner.StorageProvider;
        var files = await provider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = labels.ChooseBackgroundImageTitle,
            AllowMultiple = false,
            FileTypeFilter = [ImageFiles(labels)]
        });
        return files.Select(ToLocalPath).Where(path => path is not null).Cast<string>().ToList();
    }

    private static FilePickerFileType AudioFiles(FileDialogLabels labels)
    {
        return new FilePickerFileType(labels.AudioFilesTypeName)
        {
            Patterns = AudioFilePatterns
        };
    }

    private static FilePickerFileType ImageFiles(FileDialogLabels labels)
    {
        return new FilePickerFileType(labels.ImageFilesTypeName)
        {
            Patterns = ImageFilePatterns
        };
    }

    private static FilePickerFileType ProfileZip(FileDialogLabels labels)
    {
        return new FilePickerFileType(labels.ProfileZipTypeName)
        {
            Patterns = ["*.zip"]
        };
    }

    private static string? ToLocalPath(IStorageItem item)
    {
        return item.Path.IsAbsoluteUri ? item.Path.LocalPath : null;
    }
}
