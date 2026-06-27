using DungeonSoundboard.App.Services;
using DungeonSoundboard.App.ViewModels;
using DungeonSoundboard.Core.Models;
using DungeonSoundboard.Core.Services;
using Xunit;

namespace DungeonSoundboard.App.Tests;

public sealed class MainWindowViewModelSystemTests
{
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

    private sealed class FakeExternalLauncher : IExternalLauncher
    {
        public string? LastOpenedFolder { get; private set; }

        public void OpenFolder(string path)
        {
            LastOpenedFolder = path;
        }
    }

    private sealed class FakeStorageService(string dataDirectory) : IStorageService
    {
        public string DataDirectory { get; } = dataDirectory;

        public AppState Load()
        {
            var state = new AppState();
            state.EnsureDefaults();
            return state;
        }

        public void Save(AppState state)
        {
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
