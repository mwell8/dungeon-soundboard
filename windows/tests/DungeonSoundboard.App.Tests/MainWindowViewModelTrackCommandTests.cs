using DungeonSoundboard.App.Services;
using DungeonSoundboard.App.ViewModels;
using DungeonSoundboard.Core.Models;
using DungeonSoundboard.Core.Services;
using Xunit;

namespace DungeonSoundboard.App.Tests;

public sealed class MainWindowViewModelTrackCommandTests
{
    [Fact]
    public void DeleteMusicTrackItemRemovesRequestedTrackWithoutChangingPlaylistSelection()
    {
        var first = new Track("First", "C:\\audio\\first.mp3", TrackRole.Music);
        var second = new Track("Second", "C:\\audio\\second.mp3", TrackRole.Music);
        var playlist = new Playlist("Music", [first, second]);
        var storage = StorageWith(playlist, new EffectPlaylist("SFX"));
        using var viewModel = CreateViewModel(storage);

        viewModel.DeleteMusicTrackItemCommand.Execute(second);

        Assert.Equal(playlist.Id, viewModel.SelectedMusicPlaylist?.Id);
        Assert.Single(viewModel.MusicTracks);
        Assert.Equal(first.Id, viewModel.MusicTracks[0].Id);
        Assert.Equal(first.Id, viewModel.SelectedMusicTrack?.Id);
        Assert.True(storage.SaveCount > 0);
    }

    [Fact]
    public void BindMusicTrackItemCapturesRequestedTrackWithoutChangingSelectedTrack()
    {
        var first = new Track("First", "C:\\audio\\first.mp3", TrackRole.Music);
        var second = new Track("Second", "C:\\audio\\second.mp3", TrackRole.Music);
        var playlist = new Playlist("Music", [first, second]);
        using var viewModel = CreateViewModel(StorageWith(playlist, new EffectPlaylist("SFX")));

        viewModel.BindMusicTrackItemCommand.Execute(second);

        Assert.Equal(first.Id, viewModel.SelectedMusicTrack?.Id);
        Assert.Equal(HotkeyAction.PlayMusicTrack(playlist.Id, second.Id), viewModel.CaptureAction);
    }

    [Fact]
    public void MoveEffectTrackItemDownReordersRequestedEffect()
    {
        var first = new Track("First", "C:\\audio\\first.wav", TrackRole.Effect);
        var second = new Track("Second", "C:\\audio\\second.wav", TrackRole.Effect);
        var third = new Track("Third", "C:\\audio\\third.wav", TrackRole.Effect);
        var effects = new EffectPlaylist("SFX", [first, second, third]);
        var storage = StorageWith(new Playlist("Music"), effects);
        using var viewModel = CreateViewModel(storage);

        viewModel.MoveEffectTrackItemDownCommand.Execute(first);

        Assert.Collection(
            viewModel.EffectTracks,
            track => Assert.Equal(second.Id, track.Id),
            track => Assert.Equal(first.Id, track.Id),
            track => Assert.Equal(third.Id, track.Id));
        Assert.True(storage.SaveCount > 0);
    }

    [Fact]
    public void MusicTrackTilesExposeHotkeyCurrentAndMissingFileStatus()
    {
        var missingTrack = new Track("Missing", "C:\\audio\\missing-file.mp3", TrackRole.Music);
        var currentTrack = new Track("Current", "C:\\audio\\current.mp3", TrackRole.Music);
        var playlist = new Playlist("Music", [missingTrack, currentTrack]);
        using var viewModel = CreateViewModel(StorageWith(playlist, new EffectPlaylist("SFX")));

        viewModel.BindMusicTrackItemCommand.Execute(missingTrack);
        Assert.True(viewModel.HandleHotkey(new Hotkey(0, "A", HotkeyModifier.None)));
        viewModel.PlayMusicTrackCommand.Execute(currentTrack);

        var missingTile = Assert.Single(viewModel.MusicTrackTiles, tile => tile.Track.Id == missingTrack.Id);
        var currentTile = Assert.Single(viewModel.MusicTrackTiles, tile => tile.Track.Id == currentTrack.Id);
        Assert.Equal("A", missingTile.HotkeyText);
        Assert.True(missingTile.HasHotkey);
        Assert.True(missingTile.IsFileMissing);
        Assert.Equal("Missing file", missingTile.FileStatus);
        Assert.True(currentTile.IsCurrent);
    }

    [Fact]
    public void EffectTrackTilesExposeHotkeyAndMissingFileStatus()
    {
        var effect = new Track("Door slam", "C:\\audio\\missing-door.wav", TrackRole.Effect);
        var effects = new EffectPlaylist("SFX", [effect]);
        using var viewModel = CreateViewModel(StorageWith(new Playlist("Music"), effects));

        viewModel.BindEffectTrackItemCommand.Execute(effect);
        Assert.True(viewModel.HandleHotkey(new Hotkey(1, "S", HotkeyModifier.Shift)));

        var tile = Assert.Single(viewModel.EffectTrackTiles);
        Assert.Equal(effect.Id, tile.Track.Id);
        Assert.Equal("Shift+S", tile.HotkeyText);
        Assert.True(tile.HasHotkey);
        Assert.True(tile.IsFileMissing);
        Assert.Equal("Missing file", tile.FileStatus);
    }

    private static MainWindowViewModel CreateViewModel(FakeStorageService storage)
    {
        return new MainWindowViewModel(storage, new FileImportService(), new FakeAudioService());
    }

    private static FakeStorageService StorageWith(Playlist musicPlaylist, EffectPlaylist effectPlaylist)
    {
        var state = new AppState
        {
            MusicPlaylists = [musicPlaylist],
            EffectPlaylists = [effectPlaylist],
            Preferences = new PlayerPreferences
            {
                SelectedMusicPlaylistId = musicPlaylist.Id,
                SelectedEffectPlaylistId = effectPlaylist.Id
            }
        };

        return new FakeStorageService(state);
    }

    private sealed class FakeStorageService(AppState state) : IStorageService
    {
        public string DataDirectory => "C:\\Temp\\DungeonSoundboardTests";
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

        public event EventHandler? EffectPlaybackCountChanged;

        public int ActiveEffectCount { get; private set; }
        public bool IsMusicPlaying { get; private set; }

        public void Dispose()
        {
        }

        public void PlayMusic(Track track, double volume)
        {
            IsMusicPlaying = true;
        }

        public void PauseMusic()
        {
            IsMusicPlaying = false;
        }

        public void ResumeMusic()
        {
            IsMusicPlaying = true;
        }

        public void StopMusic()
        {
            IsMusicPlaying = false;
        }

        public void StopAll()
        {
            IsMusicPlaying = false;
            ActiveEffectCount = 0;
        }

        public void StopEffects()
        {
            ActiveEffectCount = 0;
        }

        public void PlayEffect(Track track, double volume)
        {
            ActiveEffectCount++;
            EffectPlaybackCountChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SetMusicVolume(double volume)
        {
        }

        public void SetEffectsVolume(double masterVolume)
        {
        }
    }
}
