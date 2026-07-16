using DungeonSoundboard.App.Services;
using DungeonSoundboard.App.ViewModels;
using DungeonSoundboard.Core.Models;
using DungeonSoundboard.Core.Services;
using Xunit;

namespace DungeonSoundboard.App.Tests;

public sealed class MainWindowViewModelSystemTests
{
    [Fact]
    public void StorageReadFailureUsesDefaultsAndReportsTheError()
    {
        var storage = new FakeStorageService
        {
            LoadException = new IOException("profile directory is unavailable")
        };
        var errorLog = new RecordingErrorLogService();

        using var viewModel = new MainWindowViewModel(
            storage,
            new FileImportService(),
            new FakeAudioService(),
            new FakeExternalLauncher(),
            errorLog: errorLog);

        Assert.NotEmpty(viewModel.MusicPlaylists);
        Assert.NotEmpty(viewModel.EffectPlaylists);
        Assert.True(viewModel.IsStatusError);
        Assert.Contains("profile directory is unavailable", viewModel.StatusMessage);
        Assert.Equal("storage.load", errorLog.LastCategory);
    }

    [Fact]
    public void StorageWriteFailureIsReportedWithoutCrashingTheApp()
    {
        var storage = new FakeStorageService
        {
            SaveException = new UnauthorizedAccessException("read-only profile")
        };
        var errorLog = new RecordingErrorLogService();
        using var viewModel = new MainWindowViewModel(
            storage,
            new FileImportService(),
            new FakeAudioService(),
            new FakeExternalLauncher(),
            errorLog: errorLog);

        var exception = Record.Exception(() => viewModel.SelectedMusicPlaylistName = "Unsaved name");

        Assert.Null(exception);
        Assert.True(viewModel.IsStatusError);
        Assert.Contains("read-only profile", viewModel.StatusMessage);
        Assert.Equal("storage.save", errorLog.LastCategory);
    }

    [Fact]
    public void AboutMetadataUsesSharedProductVersionAndAuthor()
    {
        using var viewModel = new MainWindowViewModel(
            new FakeStorageService(),
            new FileImportService(),
            new FakeAudioService(),
            new FakeExternalLauncher());

        Assert.Equal("1.2.0", viewModel.AppVersion);
        Assert.Contains("1.2.0", viewModel.AppVersionText);
        Assert.Equal("MWell", viewModel.AppAuthorName);
    }

    [Fact]
    public void OpenAuthorPageCommandLaunchesMWellBoostyPage()
    {
        var launcher = new FakeExternalLauncher();
        using var viewModel = new MainWindowViewModel(
            new FakeStorageService(),
            new FileImportService(),
            new FakeAudioService(),
            launcher);

        viewModel.OpenAuthorPageCommand.Execute(null);

        Assert.Equal("https://boosty.to/mwell", launcher.LastOpenedUrl);
    }

    [Fact]
    public async Task ExportProfileCommandSavesCurrentStateAndExportsSelectedZip()
    {
        var dataDirectory = Path.Combine(Path.GetTempPath(), "DungeonSoundboardTests", Guid.NewGuid().ToString("N"));
        var exportPath = Path.Combine(dataDirectory, "profile.zip");
        var storage = new FakeStorageService(dataDirectory);
        var dialogs = new FakeFileDialogService
        {
            ProfileSavePath = exportPath
        };
        var profileTransfer = new FakeProfileTransferService();
        using var viewModel = new MainWindowViewModel(
            storage,
            new FileImportService(),
            new FakeAudioService(),
            new FakeExternalLauncher(),
            profileTransfer)
        {
            FileDialogService = dialogs
        };

        viewModel.SelectedMusicPlaylistName = "Exported Music";

        await viewModel.ExportProfileCommand.ExecuteAsync(null);

        Assert.Equal(exportPath, dialogs.ProfileSaveLabelsPath);
        Assert.Equal(dataDirectory, profileTransfer.ExportedDataDirectory);
        Assert.Equal(exportPath, profileTransfer.ExportedZipPath);
        Assert.False(viewModel.IsStatusError);
        Assert.Contains(exportPath, viewModel.StatusMessage);
        Assert.True(storage.SaveCount > 0);
    }

    [Fact]
    public async Task RestoreProfileCommandReloadsRestoredStateIntoViewModel()
    {
        var dataDirectory = Path.Combine(Path.GetTempPath(), "DungeonSoundboardTests", Guid.NewGuid().ToString("N"));
        var restorePath = Path.Combine(dataDirectory, "profile.zip");
        var backupDirectory = Path.Combine(dataDirectory, "Backups", "profile-restore-test");
        var storage = new FakeStorageService(dataDirectory);
        var restoredMusic = new Playlist("Restored Music");
        var restoredSfx = new EffectPlaylist("Restored SFX");
        var restoredState = new AppState
        {
            MusicPlaylists = [restoredMusic],
            EffectPlaylists = [restoredSfx],
            Preferences = new PlayerPreferences
            {
                Language = AppLanguage.English,
                SelectedMusicPlaylistId = restoredMusic.Id,
                SelectedEffectPlaylistId = restoredSfx.Id
            }
        };
        var dialogs = new FakeFileDialogService
        {
            ProfileOpenPath = restorePath
        };
        var profileTransfer = new FakeProfileTransferService
        {
            RestoreHandler = () =>
            {
                storage.State = restoredState;
                return new ProfileRestoreResult(backupDirectory);
            }
        };
        using var viewModel = new MainWindowViewModel(
            storage,
            new FileImportService(),
            new FakeAudioService(),
            new FakeExternalLauncher(),
            profileTransfer)
        {
            FileDialogService = dialogs
        };

        await viewModel.RestoreProfileCommand.ExecuteAsync(null);

        Assert.Equal(restorePath, dialogs.ProfileOpenLabelsPath);
        Assert.Equal(dataDirectory, profileTransfer.RestoredDataDirectory);
        Assert.Equal(restorePath, profileTransfer.RestoredZipPath);
        Assert.Equal("Restored Music", viewModel.SelectedMusicPlaylistName);
        Assert.Equal("Restored SFX", viewModel.SelectedEffectPlaylistName);
        Assert.False(viewModel.IsStatusError);
        Assert.Contains(backupDirectory, viewModel.StatusMessage);
    }

    [Fact]
    public async Task RestoreProfileCommandKeepsCurrentStateWhenRestoreFails()
    {
        var storage = new FakeStorageService();
        storage.State.MusicPlaylists[0].Name = "Current Music";
        var dialogs = new FakeFileDialogService
        {
            ProfileOpenPath = Path.Combine(storage.DataDirectory, "bad-profile.zip")
        };
        var profileTransfer = new FakeProfileTransferService
        {
            RestoreException = new InvalidDataException("bad profile")
        };
        using var viewModel = new MainWindowViewModel(
            storage,
            new FileImportService(),
            new FakeAudioService(),
            new FakeExternalLauncher(),
            profileTransfer)
        {
            FileDialogService = dialogs
        };

        await viewModel.RestoreProfileCommand.ExecuteAsync(null);

        Assert.Equal("Current Music", viewModel.SelectedMusicPlaylistName);
        Assert.True(viewModel.IsStatusError);
        Assert.Contains("bad profile", viewModel.StatusMessage);
    }

    [Fact]
    public void RemoveMissingMusicFilesCommandRemovesMissingTracksFromSelectedPlaylist()
    {
        var dataDirectory = Path.Combine(Path.GetTempPath(), "DungeonSoundboardTests", Guid.NewGuid().ToString("N"));
        var existingPath = Path.Combine(dataDirectory, "existing.mp3");
        var missingPath = Path.Combine(dataDirectory, "missing.mp3");
        Directory.CreateDirectory(dataDirectory);
        File.WriteAllText(existingPath, "");
        var existing = new Track("Existing", existingPath, TrackRole.Music);
        var missing = new Track("Missing", missingPath, TrackRole.Music);
        var playlist = new Playlist("Battle", [existing, missing]);
        var storage = new FakeStorageService(dataDirectory)
        {
            State = new AppState
            {
                MusicPlaylists = [playlist],
                Preferences = new PlayerPreferences
                {
                    Language = AppLanguage.English,
                    SelectedMusicPlaylistId = playlist.Id
                },
                Hotkeys = new HotkeyConfiguration(
                [
                    new HotkeyBinding(HotkeyAction.PlayMusicTrack(playlist.Id, missing.Id), new Hotkey(1, "A", HotkeyModifier.None))
                ])
            }
        };
        storage.State.EnsureDefaults();
        using var viewModel = new MainWindowViewModel(storage, new FileImportService(), new FakeAudioService(), new FakeExternalLauncher());

        viewModel.RemoveMissingMusicFilesCommand.Execute(null);

        Assert.Single(viewModel.MusicTracks);
        Assert.Equal(existing.Id, viewModel.MusicTracks[0].Id);
        Assert.Null(storage.State.Hotkeys.HotkeyFor(HotkeyAction.PlayMusicTrack(playlist.Id, missing.Id)));
        Assert.False(viewModel.IsStatusError);
        Assert.Contains("Removed missing files: 1", viewModel.StatusMessage);

        Directory.Delete(dataDirectory, recursive: true);
    }

    [Fact]
    public async Task AddMusicFilesReportsDuplicatesUnsupportedAndSkippedInputs()
    {
        var dataDirectory = Path.Combine(Path.GetTempPath(), "DungeonSoundboardTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dataDirectory);
        var audioPath = Path.Combine(dataDirectory, "rain.mp3");
        var notesPath = Path.Combine(dataDirectory, "notes.txt");
        var missingPath = Path.Combine(dataDirectory, "missing.wav");
        File.WriteAllText(audioPath, "");
        File.WriteAllText(notesPath, "");
        var existing = new Track("Rain", audioPath, TrackRole.Music);
        var playlist = new Playlist("Battle", [existing]);
        var storage = new FakeStorageService(dataDirectory)
        {
            State = new AppState
            {
                MusicPlaylists = [playlist],
                Preferences = new PlayerPreferences
                {
                    Language = AppLanguage.English,
                    SelectedMusicPlaylistId = playlist.Id
                }
            }
        };
        var dialogs = new FakeFileDialogService
        {
            AudioFiles = [audioPath, notesPath, missingPath]
        };
        using var viewModel = new MainWindowViewModel(storage, new FileImportService(), new FakeAudioService(), new FakeExternalLauncher())
        {
            FileDialogService = dialogs
        };

        await viewModel.AddMusicFilesCommand.ExecuteAsync(null);

        Assert.Contains("Added 0", viewModel.StatusMessage);
        Assert.Contains("duplicates: 1", viewModel.StatusMessage);
        Assert.Contains("unsupported: 1", viewModel.StatusMessage);
        Assert.Contains("unavailable: 1", viewModel.StatusMessage);

        Directory.Delete(dataDirectory, recursive: true);
    }

    [Fact]
    public void OpenDataDirectoryCreatesDirectoryAndLaunchesIt()
    {
        var dataDirectory = Path.Combine(Path.GetTempPath(), "DungeonSoundboardTests", Guid.NewGuid().ToString("N"));
        var launcher = new FakeExternalLauncher();
        var storage = new FakeStorageService(dataDirectory);
        using var viewModel = new MainWindowViewModel(storage, new FileImportService(), new FakeAudioService(), launcher);

        viewModel.OpenDataDirectoryCommand.Execute(null);

        Assert.True(Directory.Exists(dataDirectory));
        Assert.Equal(dataDirectory, launcher.LastOpenedFolder);

        Directory.Delete(dataDirectory, recursive: true);
    }

    [Fact]
    public void OpenTrackLocationRevealsExistingAudioFile()
    {
        var dataDirectory = Path.Combine(Path.GetTempPath(), "DungeonSoundboardTests", Guid.NewGuid().ToString("N"));
        var audioDirectory = Path.Combine(dataDirectory, "audio");
        var audioPath = Path.Combine(audioDirectory, "ambience.mp3");
        Directory.CreateDirectory(audioDirectory);
        File.WriteAllText(audioPath, "");

        var launcher = new FakeExternalLauncher();
        var storage = new FakeStorageService(dataDirectory);
        using var viewModel = new MainWindowViewModel(storage, new FileImportService(), new FakeAudioService(), launcher);
        var track = new Track("Ambience", audioPath, TrackRole.Music);

        viewModel.OpenTrackLocationCommand.Execute(track);

        Assert.Equal(audioPath, launcher.LastRevealedFile);
        Assert.Null(launcher.LastOpenedFolder);

        Directory.Delete(dataDirectory, recursive: true);
    }

    [Fact]
    public void OpenTrackLocationOpensParentFolderForMissingAudioFile()
    {
        var dataDirectory = Path.Combine(Path.GetTempPath(), "DungeonSoundboardTests", Guid.NewGuid().ToString("N"));
        var audioDirectory = Path.Combine(dataDirectory, "audio");
        var audioPath = Path.Combine(audioDirectory, "missing.mp3");
        Directory.CreateDirectory(audioDirectory);

        var launcher = new FakeExternalLauncher();
        var storage = new FakeStorageService(dataDirectory);
        storage.State.Preferences.Language = AppLanguage.English;
        using var viewModel = new MainWindowViewModel(storage, new FileImportService(), new FakeAudioService(), launcher);
        var track = new Track("Missing", audioPath, TrackRole.Music);

        viewModel.OpenTrackLocationCommand.Execute(track);

        Assert.Equal(audioDirectory, launcher.LastOpenedFolder);
        Assert.Null(launcher.LastRevealedFile);
        Assert.True(viewModel.IsStatusError);
        Assert.Equal($"Audio file is missing: {audioPath}", viewModel.StatusMessage);

        Directory.Delete(dataDirectory, recursive: true);
    }

    [Fact]
    public void OpenTrackLocationHandlesInvalidTrackPath()
    {
        var storage = new FakeStorageService();
        using var viewModel = new MainWindowViewModel(storage, new FileImportService(), new FakeAudioService(), new FakeExternalLauncher());
        var track = new Track("Broken", "bad\0path.mp3", TrackRole.Music);

        viewModel.OpenTrackLocationCommand.Execute(track);

        Assert.True(viewModel.IsStatusError);
    }

    [Fact]
    public void LanguageOptionsExposeEnglishAndRussian()
    {
        var storage = new FakeStorageService();
        storage.State.Preferences.Language = AppLanguage.English;
        using var viewModel = new MainWindowViewModel(storage, new FileImportService(), new FakeAudioService(), new FakeExternalLauncher());

        Assert.Equal([AppLanguage.English, AppLanguage.Russian], viewModel.LanguageOptions.Select(option => option.Language).ToArray());
        Assert.Equal(AppLanguage.English, viewModel.SelectedLanguage?.Language);
        Assert.Equal("Settings", viewModel.Ui.Settings);
        Assert.Equal("Music Playlists", viewModel.Ui.MusicPlaylists);
        Assert.Equal("Русский", Assert.Single(viewModel.LanguageOptions, option => option.Language == AppLanguage.Russian).Name);
    }

    [Fact]
    public void SelectedLanguageSavesPreferenceAndUpdatesVisibleStrings()
    {
        var storage = new FakeStorageService();
        storage.State.Preferences.Language = AppLanguage.English;
        using var viewModel = new MainWindowViewModel(storage, new FileImportService(), new FakeAudioService(), new FakeExternalLauncher());
        var russian = Assert.Single(viewModel.LanguageOptions, option => option.Language == AppLanguage.Russian);

        viewModel.SelectedLanguage = russian;

        Assert.Equal(AppLanguage.Russian, storage.State.Preferences.Language);
        Assert.Equal("Настройки", viewModel.Ui.Settings);
        Assert.Equal("Музыкальные плейлисты", viewModel.Ui.MusicPlaylists);
        Assert.Equal("Ничего не играет", viewModel.CurrentTrackTitle);
        Assert.Equal("Не назначено", Assert.Single(viewModel.SystemHotkeyRows, row => row.Action.Equals(HotkeyAction.StopAll)).HotkeyText);
        Assert.Equal("Остановить все звуки", Assert.Single(viewModel.SystemHotkeyRows, row => row.Action.Equals(HotkeyAction.StopAll)).Name);
        Assert.Equal("Без повтора", Assert.Single(viewModel.RepeatModeOptions, option => option.Value == RepeatMode.Off).Name);
        Assert.Equal("Без приглушения", viewModel.DuckingStatus);
        Assert.Equal("Обычная", Assert.Single(viewModel.InterfaceDensityOptions, option => option.Value == InterfaceDensity.Normal).Name);
        Assert.Equal("Заполнить", Assert.Single(viewModel.BackgroundLayoutModeOptions, option => option.Value == BackgroundLayoutMode.Fill).Name);
        Assert.True(storage.SaveCount > 0);
    }

    [Fact]
    public void SelectedLanguageLocalizesTrackStateAndDeleteDialog()
    {
        var storage = new FakeStorageService();
        using var viewModel = new MainWindowViewModel(storage, new FileImportService(), new FakeAudioService(), new FakeExternalLauncher());
        var russian = Assert.Single(viewModel.LanguageOptions, option => option.Language == AppLanguage.Russian);
        var missingPath = Path.Combine(Path.GetTempPath(), "DungeonSoundboardTests", Guid.NewGuid().ToString("N"), "missing.mp3");
        var track = new Track("Тема боя", missingPath, TrackRole.Music);

        viewModel.SelectedLanguage = russian;
        viewModel.SelectedMusicPlaylist!.Tracks.Add(track);
        viewModel.SelectedMusicTrack = track;

        Assert.Equal("Файл отсутствует", viewModel.SelectedMusicTrackFileStatus);
        Assert.Equal("Нет файла: missing.mp3", Assert.Single(viewModel.MusicTrackTiles, tile => tile.Track.Id == track.Id).FileDisplayDetail);

        viewModel.RequestDeleteMusicTrackCommand.Execute(null);

        Assert.Equal("Удалить музыкальный трек?", viewModel.DeleteConfirmationTitle);
        Assert.Equal("Удалить «Тема боя» из этого плейлиста? Аудиофайл останется на диске.", viewModel.DeleteConfirmationMessage);
    }

    [Fact]
    public async Task FileDialogCommandsUseSelectedLanguageLabels()
    {
        var storage = new FakeStorageService();
        var dialogs = new FakeFileDialogService();
        using var viewModel = new MainWindowViewModel(storage, new FileImportService(), new FakeAudioService(), new FakeExternalLauncher())
        {
            FileDialogService = dialogs
        };
        viewModel.SelectedLanguage = Assert.Single(viewModel.LanguageOptions, option => option.Language == AppLanguage.Russian);

        await viewModel.AddMusicFilesCommand.ExecuteAsync(null);
        await viewModel.AddMusicFolderCommand.ExecuteAsync(null);
        await viewModel.ChooseBackgroundImageCommand.ExecuteAsync(null);

        Assert.Equal("Выберите аудиофайлы", dialogs.AudioFilesLabels?.ChooseAudioFilesTitle);
        Assert.Equal("Аудиофайлы", dialogs.AudioFilesLabels?.AudioFilesTypeName);
        Assert.Equal("Выберите папку с аудио", dialogs.AudioFolderLabels?.ChooseAudioFolderTitle);
        Assert.Equal("Выбрать фоновое изображение", dialogs.BackgroundImageLabels?.ChooseBackgroundImageTitle);
        Assert.Equal("Изображения", dialogs.BackgroundImageLabels?.ImageFilesTypeName);
    }

    private sealed class FakeExternalLauncher : IExternalLauncher
    {
        public string? LastOpenedFolder { get; private set; }
        public string? LastRevealedFile { get; private set; }
        public string? LastOpenedUrl { get; private set; }

        public void OpenFolder(string path)
        {
            LastOpenedFolder = path;
        }

        public void RevealFile(string path)
        {
            LastRevealedFile = path;
        }

        public void OpenUrl(string url)
        {
            LastOpenedUrl = url;
        }
    }

    private sealed class FakeFileDialogService : IFileDialogService
    {
        public FileDialogLabels? AudioFilesLabels { get; private set; }
        public FileDialogLabels? AudioFolderLabels { get; private set; }
        public FileDialogLabels? BackgroundImageLabels { get; private set; }
        public IReadOnlyList<string> AudioFiles { get; init; } = [];
        public IReadOnlyList<string> AudioFolders { get; init; } = [];
        public IReadOnlyList<string> BackgroundImages { get; init; } = [];
        public string? ProfileSavePath { get; init; }
        public string? ProfileOpenPath { get; init; }
        public string? ProfileSaveLabelsPath { get; private set; }
        public string? ProfileOpenLabelsPath { get; private set; }

        public Task<IReadOnlyList<string>> OpenAudioFilesAsync(FileDialogLabels labels)
        {
            AudioFilesLabels = labels;
            return Task.FromResult(AudioFiles);
        }

        public Task<IReadOnlyList<string>> OpenAudioFolderAsync(FileDialogLabels labels)
        {
            AudioFolderLabels = labels;
            return Task.FromResult(AudioFolders);
        }

        public Task<IReadOnlyList<string>> OpenBackgroundImageAsync(FileDialogLabels labels)
        {
            BackgroundImageLabels = labels;
            return Task.FromResult(BackgroundImages);
        }

        public Task<string?> SaveProfileZipAsync(FileDialogLabels labels)
        {
            ProfileSaveLabelsPath = ProfileSavePath;
            return Task.FromResult(ProfileSavePath);
        }

        public Task<string?> OpenProfileZipAsync(FileDialogLabels labels)
        {
            ProfileOpenLabelsPath = ProfileOpenPath;
            return Task.FromResult(ProfileOpenPath);
        }
    }

    private sealed class FakeStorageService : IStorageService
    {
        public FakeStorageService()
            : this(Path.Combine(Path.GetTempPath(), "DungeonSoundboardTests", Guid.NewGuid().ToString("N")))
        {
        }

        public FakeStorageService(string dataDirectory)
        {
            DataDirectory = dataDirectory;
            State = new AppState();
            State.EnsureDefaults();
        }

        public string DataDirectory { get; }
        public AppState State { get; set; }
        public int SaveCount { get; private set; }
        public Exception? LoadException { get; init; }
        public Exception? SaveException { get; init; }

        public AppState Load()
        {
            if (LoadException is not null)
            {
                throw LoadException;
            }

            State.EnsureDefaults();
            return State;
        }

        public void Save(AppState state)
        {
            if (SaveException is not null)
            {
                throw SaveException;
            }

            SaveCount++;
            State = state;
        }
    }

    private sealed class RecordingErrorLogService : IErrorLogService
    {
        public string LogDirectory => "";
        public string? LastCategory { get; private set; }

        public void Write(string category, string message, Exception? exception = null)
        {
            LastCategory = category;
        }
    }

    private sealed class FakeProfileTransferService : IProfileTransferService
    {
        public string? ExportedDataDirectory { get; private set; }
        public string? ExportedZipPath { get; private set; }
        public string? RestoredDataDirectory { get; private set; }
        public string? RestoredZipPath { get; private set; }
        public InvalidDataException? RestoreException { get; init; }
        public Func<ProfileRestoreResult>? RestoreHandler { get; init; }

        public ProfileExportResult Export(string dataDirectory, string destinationZipPath, string? appVersion = null)
        {
            ExportedDataDirectory = dataDirectory;
            ExportedZipPath = destinationZipPath;
            return new ProfileExportResult(destinationZipPath);
        }

        public ProfileRestoreResult Restore(string dataDirectory, string sourceZipPath)
        {
            RestoredDataDirectory = dataDirectory;
            RestoredZipPath = sourceZipPath;
            if (RestoreException is not null)
            {
                throw RestoreException;
            }

            return RestoreHandler?.Invoke()
                ?? new ProfileRestoreResult(Path.Combine(dataDirectory, "Backups", "profile-restore-test"));
        }
    }

    private sealed class FakeAudioService : IAudioService
    {
        public event EventHandler? MusicFinished
        {
            add { }
            remove { }
        }

        public event EventHandler? EffectPlaybackCountChanged
        {
            add { }
            remove { }
        }

        public int ActiveEffectCount => 0;
        public bool IsMusicPlaying => false;
        public TimeSpan MusicPosition => TimeSpan.Zero;
        public TimeSpan MusicDuration => TimeSpan.Zero;

        public void Dispose()
        {
        }

        public void PlayMusic(Track track, double volume)
        {
        }

        public void PauseMusic()
        {
        }

        public void FadeOutAndPauseMusic(TimeSpan duration, double restoreVolume)
        {
        }

        public void ResumeMusic()
        {
        }

        public void SeekMusic(TimeSpan position)
        {
        }

        public void StopMusic()
        {
        }

        public void StopAll()
        {
        }

        public void StopEffects()
        {
        }

        public void PlayEffect(Track track, double volume)
        {
        }

        public void SetMusicVolume(double volume)
        {
        }

        public void SetEffectsVolume(double masterVolume)
        {
        }
    }
}
