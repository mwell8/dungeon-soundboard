using DungeonSoundboard.App.Services;
using DungeonSoundboard.App.ViewModels;
using DungeonSoundboard.Core.Models;
using DungeonSoundboard.Core.Services;
using Xunit;

namespace DungeonSoundboard.App.Tests;

public sealed class MainWindowViewModelBackgroundTests
{
    [Fact]
    public void BackgroundSettingsClampAndSave()
    {
        var storage = StorageWithImageBackground();
        using var viewModel = CreateViewModel(storage);

        viewModel.BackgroundLayoutMode = BackgroundLayoutMode.Fit;
        viewModel.BackgroundOpacity = 99;
        viewModel.BackgroundDimOverlay = -3;

        Assert.Equal(BackgroundLayoutMode.Fit, storage.State.Theme.Background.LayoutMode);
        Assert.Equal(1, storage.State.Theme.Background.Opacity);
        Assert.Equal(0.15, storage.State.Theme.Background.DimOverlay);
        Assert.True(storage.SaveCount >= 3);
    }

    [Fact]
    public void ClearBackgroundImageReturnsToThemeBackground()
    {
        var storage = StorageWithImageBackground();
        using var viewModel = CreateViewModel(storage);

        viewModel.ClearBackgroundImageCommand.Execute(null);

        Assert.False(viewModel.HasBackgroundImage);
        Assert.Equal(BackgroundMode.None, storage.State.Theme.Background.Mode);
        Assert.Null(storage.State.Theme.Background.ImageOriginalPath);
        Assert.Equal("Theme preset background", viewModel.BackgroundImageStatus);
    }

    [Fact]
    public void ThemePresetChangePreservesSelectedBackgroundImage()
    {
        var storage = StorageWithImageBackground();
        using var viewModel = CreateViewModel(storage);

        viewModel.SelectedThemePreset = viewModel.ThemePresetOptions.First(option => option.Preset == ThemePreset.ForestMist);

        Assert.Equal(ThemePreset.ForestMist, storage.State.Theme.Preset);
        Assert.Equal(BackgroundMode.Image, storage.State.Theme.Background.Mode);
        Assert.Equal("C:\\audio\\background.png", storage.State.Theme.Background.ImageOriginalPath);
        Assert.Equal(BackgroundLayoutMode.Fill, storage.State.Theme.Background.LayoutMode);
    }

    [Fact]
    public void TrackColumnSettingsNormalizeAndChangeTileWidths()
    {
        var storage = StorageWithImageBackground();
        using var viewModel = CreateViewModel(storage);

        viewModel.MusicColumns = 4;
        viewModel.EffectsColumns = 2;

        Assert.Equal(4, storage.State.Preferences.MusicColumns);
        Assert.Equal(2, storage.State.Preferences.EffectsColumns);
        Assert.Equal(204, viewModel.MusicTileWidth);
        Assert.Equal(420, viewModel.EffectTileWidth);

        viewModel.MusicColumns = 99;

        Assert.Equal(3, storage.State.Preferences.MusicColumns);
        Assert.Equal(268, viewModel.MusicTileWidth);
    }

    [Fact]
    public void ColumnOptionsExposeSupportedRange()
    {
        using var viewModel = CreateViewModel(StorageWithImageBackground());

        Assert.Equal([2, 3, 4], viewModel.ColumnCountOptions);
    }

    private static MainWindowViewModel CreateViewModel(FakeStorageService storage)
    {
        return new MainWindowViewModel(storage, new FileImportService(), new FakeAudioService());
    }

    private static FakeStorageService StorageWithImageBackground()
    {
        var musicPlaylist = new Playlist("Music");
        var effectPlaylist = new EffectPlaylist("SFX");
        var state = new AppState
        {
            MusicPlaylists = [musicPlaylist],
            EffectPlaylists = [effectPlaylist],
            Preferences = new PlayerPreferences
            {
                SelectedMusicPlaylistId = musicPlaylist.Id,
                SelectedEffectPlaylistId = effectPlaylist.Id
            },
            Theme = ThemeRenderer.ThemeFor(ThemePreset.ClassicDungeon)
        };
        state.Theme.Background.Mode = BackgroundMode.Image;
        state.Theme.Background.ImageOriginalPath = "C:\\audio\\background.png";
        state.Theme.Background.Opacity = 0.72;
        state.Theme.Background.DimOverlay = 0.30;
        state.Theme.Background.LayoutMode = BackgroundLayoutMode.Fill;

        return new FakeStorageService(state);
    }

    private sealed class FakeStorageService(AppState state) : IStorageService
    {
        public string DataDirectory => "C:\\Temp\\DungeonSoundboardTests";
        public AppState State => state;
        public int SaveCount { get; private set; }

        public AppState Load()
        {
            state.EnsureDefaults();
            return state;
        }

        public void Save(AppState updatedState)
        {
            SaveCount++;
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
