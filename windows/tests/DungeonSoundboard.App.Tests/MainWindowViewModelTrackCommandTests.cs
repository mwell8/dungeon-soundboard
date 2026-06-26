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
    public void HotkeyCaptureVisibilityFollowsCaptureState()
    {
        var track = new Track("Track", "C:\\audio\\track.mp3", TrackRole.Music);
        var playlist = new Playlist("Music", [track]);
        using var viewModel = CreateViewModel(StorageWith(playlist, new EffectPlaylist("SFX")));

        Assert.False(viewModel.IsCapturingHotkey);

        viewModel.BindMusicTrackItemCommand.Execute(track);

        Assert.True(viewModel.IsCapturingHotkey);

        viewModel.CancelHotkeyCaptureCommand.Execute(null);

        Assert.False(viewModel.IsCapturingHotkey);
    }

    [Fact]
    public void ClearMusicTrackItemBindingClearsRequestedTrackWithoutChangingSelectedTrack()
    {
        var first = new Track("First", "C:\\audio\\first.mp3", TrackRole.Music);
        var second = new Track("Second", "C:\\audio\\second.mp3", TrackRole.Music);
        var playlist = new Playlist("Music", [first, second]);
        var storage = StorageWith(playlist, new EffectPlaylist("SFX"));
        using var viewModel = CreateViewModel(storage);

        viewModel.BindMusicTrackItemCommand.Execute(first);
        Assert.True(viewModel.HandleHotkey(new Hotkey(0, "A", HotkeyModifier.None)));
        viewModel.BindMusicTrackItemCommand.Execute(second);
        Assert.True(viewModel.HandleHotkey(new Hotkey(1, "B", HotkeyModifier.None)));

        viewModel.ClearMusicTrackItemBindingCommand.Execute(second);

        Assert.Equal(first.Id, viewModel.SelectedMusicTrack?.Id);
        Assert.Equal("A", Assert.Single(viewModel.MusicTrackTiles, tile => tile.Track.Id == first.Id).HotkeyText);
        Assert.False(Assert.Single(viewModel.MusicTrackTiles, tile => tile.Track.Id == second.Id).HasHotkey);
        Assert.True(storage.SaveCount > 0);
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

    [Fact]
    public void PlayMusicTrackTileSelectsAndStartsRequestedTrack()
    {
        var first = new Track("First", "C:\\audio\\first.mp3", TrackRole.Music);
        var second = new Track("Second", "C:\\audio\\second.mp3", TrackRole.Music);
        var playlist = new Playlist("Music", [first, second]);
        var audio = new FakeAudioService();
        using var viewModel = CreateViewModel(StorageWith(playlist, new EffectPlaylist("SFX")), audio);

        var tile = Assert.Single(viewModel.MusicTrackTiles, candidate => candidate.Track.Id == second.Id);
        viewModel.PlayMusicTrackTileCommand.Execute(tile);

        Assert.Equal(second.Id, viewModel.SelectedMusicTrack?.Id);
        Assert.Equal(second.Id, viewModel.CurrentTrack?.Id);
        Assert.Equal(second.Id, audio.LastMusicTrack?.Id);
        Assert.True(viewModel.IsPlaying);
    }

    [Fact]
    public void PlayPauseButtonTextReflectsTransportState()
    {
        var track = new Track("Track", "C:\\audio\\track.mp3", TrackRole.Music);
        var playlist = new Playlist("Music", [track]);
        var audio = new FakeAudioService();
        using var viewModel = CreateViewModel(StorageWith(playlist, new EffectPlaylist("SFX")), audio);

        Assert.Equal("Play", viewModel.PlayPauseButtonText);

        viewModel.PlayMusicTrackCommand.Execute(track);

        Assert.Equal("Pause", viewModel.PlayPauseButtonText);
        Assert.Equal(1, audio.PlayMusicCount);

        viewModel.PlayPauseCommand.Execute(null);

        Assert.Equal("Resume", viewModel.PlayPauseButtonText);
        Assert.Equal(1, audio.PauseCount);

        viewModel.PlayPauseCommand.Execute(null);

        Assert.Equal("Pause", viewModel.PlayPauseButtonText);
        Assert.Equal(1, audio.ResumeCount);
        Assert.Equal(1, audio.PlayMusicCount);

        viewModel.StopAllCommand.Execute(null);

        Assert.Equal("Play", viewModel.PlayPauseButtonText);
    }

    [Fact]
    public void PlayEffectTrackTileSelectsAndStartsEffectWithoutStoppingMusic()
    {
        var music = new Track("Music", "C:\\audio\\music.mp3", TrackRole.Music);
        var effect = new Track("Door slam", "C:\\audio\\door.wav", TrackRole.Effect);
        var audio = new FakeAudioService();
        using var viewModel = CreateViewModel(
            StorageWith(new Playlist("Music", [music]), new EffectPlaylist("SFX", [effect])),
            audio);

        viewModel.PlayMusicTrackCommand.Execute(music);
        var tile = Assert.Single(viewModel.EffectTrackTiles);
        viewModel.PlayEffectTrackTileCommand.Execute(tile);

        Assert.Equal(effect.Id, viewModel.SelectedEffectTrack?.Id);
        Assert.Equal(effect.Id, audio.LastEffectTrack?.Id);
        Assert.Equal(music.Id, viewModel.CurrentTrack?.Id);
        Assert.True(audio.IsMusicPlaying);
    }

    [Fact]
    public void PlayMusicPlaylistItemSelectsPlaylistEnablesShuffleAndStartsTrack()
    {
        var firstPlaylist = new Playlist("First", [new Track("First Track", "C:\\audio\\first.mp3", TrackRole.Music)]);
        var targetTrack = new Track("Boss Theme", "C:\\audio\\boss.mp3", TrackRole.Music);
        var secondPlaylist = new Playlist("Boss", [targetTrack]);
        var audio = new FakeAudioService();
        using var viewModel = CreateViewModel(StorageWithMusicPlaylists([firstPlaylist, secondPlaylist]), audio);

        viewModel.PlayMusicPlaylistItemCommand.Execute(secondPlaylist);

        Assert.Equal(secondPlaylist.Id, viewModel.SelectedMusicPlaylist?.Id);
        Assert.Equal(targetTrack.Id, viewModel.SelectedMusicTrack?.Id);
        Assert.Equal(targetTrack.Id, viewModel.CurrentTrack?.Id);
        Assert.Equal(targetTrack.Id, audio.LastMusicTrack?.Id);
        Assert.True(viewModel.ShuffleEnabled);
        Assert.True(viewModel.IsPlaying);
    }

    [Fact]
    public void PlayMusicPlaylistItemWithEmptyPlaylistSelectsPlaylistWithoutStartingAudio()
    {
        var firstPlaylist = new Playlist("First", [new Track("First Track", "C:\\audio\\first.mp3", TrackRole.Music)]);
        var emptyPlaylist = new Playlist("Empty");
        var audio = new FakeAudioService();
        using var viewModel = CreateViewModel(StorageWithMusicPlaylists([firstPlaylist, emptyPlaylist]), audio);

        viewModel.PlayMusicPlaylistItemCommand.Execute(emptyPlaylist);

        Assert.Equal(emptyPlaylist.Id, viewModel.SelectedMusicPlaylist?.Id);
        Assert.True(viewModel.ShuffleEnabled);
        Assert.Null(audio.LastMusicTrack);
        Assert.False(viewModel.IsPlaying);
    }

    [Fact]
    public void DeleteMusicPlaylistItemRemovesRequestedPlaylistWithoutChangingOtherSelection()
    {
        var selectedPlaylist = new Playlist("Selected", [new Track("Selected Track", "C:\\audio\\selected.mp3", TrackRole.Music)]);
        var removedPlaylist = new Playlist("Remove Me", [new Track("Removed Track", "C:\\audio\\removed.mp3", TrackRole.Music)]);
        var storage = StorageWithMusicPlaylists([selectedPlaylist, removedPlaylist]);
        using var viewModel = CreateViewModel(storage);

        viewModel.DeleteMusicPlaylistItemCommand.Execute(removedPlaylist);

        Assert.Single(viewModel.MusicPlaylists);
        Assert.Equal(selectedPlaylist.Id, viewModel.MusicPlaylists[0].Id);
        Assert.Equal(selectedPlaylist.Id, viewModel.SelectedMusicPlaylist?.Id);
        Assert.True(storage.SaveCount > 0);
    }

    [Fact]
    public void DeleteMusicPlaylistItemKeepsLastPlaylist()
    {
        var onlyPlaylist = new Playlist("Only", [new Track("Only Track", "C:\\audio\\only.mp3", TrackRole.Music)]);
        var storage = StorageWithMusicPlaylists([onlyPlaylist]);
        using var viewModel = CreateViewModel(storage);

        viewModel.DeleteMusicPlaylistItemCommand.Execute(onlyPlaylist);

        Assert.Single(viewModel.MusicPlaylists);
        Assert.Equal(onlyPlaylist.Id, viewModel.SelectedMusicPlaylist?.Id);
    }

    [Fact]
    public void MoveEffectPlaylistItemUpReordersRequestedPlaylist()
    {
        var first = new EffectPlaylist("First");
        var second = new EffectPlaylist("Second");
        var third = new EffectPlaylist("Third");
        var storage = StorageWithEffectPlaylists([first, second, third]);
        using var viewModel = CreateViewModel(storage);

        viewModel.MoveEffectPlaylistItemUpCommand.Execute(third);

        Assert.Collection(
            viewModel.EffectPlaylists,
            playlist => Assert.Equal(first.Id, playlist.Id),
            playlist => Assert.Equal(third.Id, playlist.Id),
            playlist => Assert.Equal(second.Id, playlist.Id));
        Assert.Equal(first.Id, viewModel.SelectedEffectPlaylist?.Id);
        Assert.True(storage.SaveCount > 0);
    }

    private static MainWindowViewModel CreateViewModel(FakeStorageService storage)
    {
        return CreateViewModel(storage, new FakeAudioService());
    }

    private static MainWindowViewModel CreateViewModel(FakeStorageService storage, FakeAudioService audio)
    {
        return new MainWindowViewModel(storage, new FileImportService(), audio);
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

    private static FakeStorageService StorageWithMusicPlaylists(IReadOnlyList<Playlist> musicPlaylists)
    {
        var effectPlaylist = new EffectPlaylist("SFX");
        var state = new AppState
        {
            MusicPlaylists = musicPlaylists.ToList(),
            EffectPlaylists = [effectPlaylist],
            Preferences = new PlayerPreferences
            {
                SelectedMusicPlaylistId = musicPlaylists[0].Id,
                SelectedEffectPlaylistId = effectPlaylist.Id
            }
        };

        return new FakeStorageService(state);
    }

    private static FakeStorageService StorageWithEffectPlaylists(IReadOnlyList<EffectPlaylist> effectPlaylists)
    {
        var musicPlaylist = new Playlist("Music");
        var state = new AppState
        {
            MusicPlaylists = [musicPlaylist],
            EffectPlaylists = effectPlaylists.ToList(),
            Preferences = new PlayerPreferences
            {
                SelectedMusicPlaylistId = musicPlaylist.Id,
                SelectedEffectPlaylistId = effectPlaylists[0].Id
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
        public Track? LastMusicTrack { get; private set; }
        public Track? LastEffectTrack { get; private set; }
        public int PlayMusicCount { get; private set; }
        public int PauseCount { get; private set; }
        public int ResumeCount { get; private set; }

        public void Dispose()
        {
        }

        public void PlayMusic(Track track, double volume)
        {
            IsMusicPlaying = true;
            LastMusicTrack = track;
            PlayMusicCount++;
        }

        public void PauseMusic()
        {
            IsMusicPlaying = false;
            PauseCount++;
        }

        public void ResumeMusic()
        {
            IsMusicPlaying = true;
            ResumeCount++;
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
            LastEffectTrack = track;
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
