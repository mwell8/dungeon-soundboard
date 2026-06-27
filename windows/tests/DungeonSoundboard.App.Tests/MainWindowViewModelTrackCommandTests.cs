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
    public void RequestDeleteTrackItemShowsConfirmationAndCancelKeepsTrack()
    {
        var first = new Track("First", "C:\\audio\\first.mp3", TrackRole.Music);
        var second = new Track("Second", "C:\\audio\\second.mp3", TrackRole.Music);
        var playlist = new Playlist("Music", [first, second]);
        using var viewModel = CreateViewModel(StorageWith(playlist, new EffectPlaylist("SFX")));

        viewModel.RequestDeleteMusicTrackItemCommand.Execute(second);

        Assert.True(viewModel.IsDeleteConfirmationVisible);
        Assert.Equal("Delete music track?", viewModel.DeleteConfirmationTitle);
        Assert.Contains("Second", viewModel.DeleteConfirmationMessage);

        viewModel.CancelDeleteCommand.Execute(null);

        Assert.False(viewModel.IsDeleteConfirmationVisible);
        Assert.Equal(2, viewModel.MusicTracks.Count);
        Assert.Contains(viewModel.MusicTracks, track => track.Id == second.Id);
    }

    [Fact]
    public void ConfirmDeleteTrackItemRemovesRequestedTrack()
    {
        var first = new Track("First", "C:\\audio\\first.mp3", TrackRole.Music);
        var second = new Track("Second", "C:\\audio\\second.mp3", TrackRole.Music);
        var playlist = new Playlist("Music", [first, second]);
        var storage = StorageWith(playlist, new EffectPlaylist("SFX"));
        using var viewModel = CreateViewModel(storage);

        viewModel.RequestDeleteMusicTrackItemCommand.Execute(second);
        viewModel.ConfirmDeleteCommand.Execute(null);

        Assert.False(viewModel.IsDeleteConfirmationVisible);
        Assert.Single(viewModel.MusicTracks);
        Assert.Equal(first.Id, viewModel.MusicTracks[0].Id);
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
    public void ClearHotkeyCommandsAreEnabledOnlyWhenBindingsExist()
    {
        var music = new Track("Music", "C:\\audio\\music.mp3", TrackRole.Music);
        var effect = new Track("Effect", "C:\\audio\\effect.wav", TrackRole.Effect);
        var playlist = new Playlist("Music", [music]);
        var effects = new EffectPlaylist("SFX", [effect]);
        using var viewModel = CreateViewModel(StorageWith(playlist, effects));

        Assert.True(viewModel.ClearSystemHotkeyCommand.CanExecute(HotkeyAction.PlayPause));
        Assert.False(viewModel.ClearSystemHotkeyCommand.CanExecute(HotkeyAction.StopAll));
        Assert.False(viewModel.ClearSelectedMusicBindingCommand.CanExecute(null));
        Assert.False(viewModel.ClearSelectedEffectBindingCommand.CanExecute(null));

        viewModel.BindSelectedMusicCommand.Execute(null);
        Assert.True(viewModel.HandleHotkey(new Hotkey(0, "A", HotkeyModifier.None)));
        viewModel.BindSelectedEffectCommand.Execute(null);
        Assert.True(viewModel.HandleHotkey(new Hotkey(1, "B", HotkeyModifier.None)));

        Assert.True(viewModel.ClearSelectedMusicBindingCommand.CanExecute(null));
        Assert.True(viewModel.ClearSelectedEffectBindingCommand.CanExecute(null));

        viewModel.ClearSelectedMusicBindingCommand.Execute(null);
        viewModel.ClearSelectedEffectBindingCommand.Execute(null);

        Assert.False(viewModel.ClearSelectedMusicBindingCommand.CanExecute(null));
        Assert.False(viewModel.ClearSelectedEffectBindingCommand.CanExecute(null));

        viewModel.ClearSystemHotkeyCommand.Execute(HotkeyAction.PlayPause);

        Assert.False(viewModel.ClearSystemHotkeyCommand.CanExecute(HotkeyAction.PlayPause));
    }

    [Fact]
    public void SelectedTrackEditorFlagsFollowSelectionAvailability()
    {
        var music = new Track("Music", "C:\\audio\\music.mp3", TrackRole.Music);
        var effect = new Track("Effect", "C:\\audio\\effect.wav", TrackRole.Effect);

        using var emptyViewModel = CreateViewModel(StorageWith(new Playlist("Music"), new EffectPlaylist("SFX")));
        Assert.False(emptyViewModel.HasSelectedMusicTrack);
        Assert.False(emptyViewModel.HasSelectedEffectTrack);

        using var populatedViewModel = CreateViewModel(
            StorageWith(new Playlist("Music", [music]), new EffectPlaylist("SFX", [effect])));

        Assert.True(populatedViewModel.HasSelectedMusicTrack);
        Assert.True(populatedViewModel.HasSelectedEffectTrack);

        populatedViewModel.DeleteMusicTrackItemCommand.Execute(music);
        populatedViewModel.DeleteEffectTrackItemCommand.Execute(effect);

        Assert.False(populatedViewModel.HasSelectedMusicTrack);
        Assert.False(populatedViewModel.HasSelectedEffectTrack);
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
    public void MusicTrackVolumePresetUpdatesRequestedTrackWithoutChangingSelectedTrack()
    {
        var first = new Track("First", "C:\\audio\\first.mp3", TrackRole.Music);
        var second = new Track("Second", "C:\\audio\\second.mp3", TrackRole.Music);
        var playlist = new Playlist("Music", [first, second]);
        var storage = StorageWith(playlist, new EffectPlaylist("SFX"));
        using var viewModel = CreateViewModel(storage);

        viewModel.BoostMusicTrackItemVolumeCommand.Execute(second);

        Assert.Equal(first.Id, viewModel.SelectedMusicTrack?.Id);
        Assert.Equal(1.5, second.VolumeMultiplier);
        Assert.Equal(Track.DefaultVolumeMultiplier, first.VolumeMultiplier);
        Assert.Equal(1.5, Assert.Single(viewModel.MusicTrackTiles, tile => tile.Track.Id == second.Id).VolumeMultiplier);
        Assert.True(storage.SaveCount > 0);
    }

    [Fact]
    public void MutingCurrentMusicTrackAppliesOutputVolumeImmediately()
    {
        var track = new Track("Track", "C:\\audio\\track.mp3", TrackRole.Music);
        var playlist = new Playlist("Music", [track]);
        var audio = new FakeAudioService();
        using var viewModel = CreateViewModel(StorageWith(playlist, new EffectPlaylist("SFX")), audio);

        viewModel.PlayMusicTrackCommand.Execute(track);

        Assert.Equal(viewModel.MusicVolume, audio.LastSetMusicVolume);

        viewModel.MuteMusicTrackItemVolumeCommand.Execute(track);

        Assert.Equal(0, track.VolumeMultiplier);
        Assert.Equal(0, audio.LastSetMusicVolume);
    }

    [Fact]
    public void EffectTrackVolumePresetUpdatesRequestedEffectWithoutChangingSelectedTrack()
    {
        var first = new Track("First", "C:\\audio\\first.wav", TrackRole.Effect);
        var second = new Track("Second", "C:\\audio\\second.wav", TrackRole.Effect);
        var effects = new EffectPlaylist("SFX", [first, second]);
        var storage = StorageWith(new Playlist("Music"), effects);
        using var viewModel = CreateViewModel(storage);

        viewModel.MuteEffectTrackItemVolumeCommand.Execute(second);

        Assert.Equal(first.Id, viewModel.SelectedEffectTrack?.Id);
        Assert.Equal(0, second.VolumeMultiplier);
        Assert.Equal(Track.DefaultVolumeMultiplier, first.VolumeMultiplier);
        Assert.Equal(0, Assert.Single(viewModel.EffectTrackTiles, tile => tile.Track.Id == second.Id).VolumeMultiplier);
        Assert.True(storage.SaveCount > 0);
    }

    [Fact]
    public void PlayPauseButtonTextReflectsTransportState()
    {
        var track = new Track("Track", "C:\\audio\\track.mp3", TrackRole.Music);
        var playlist = new Playlist("Music", [track]);
        var audio = new FakeAudioService();
        using var viewModel = CreateViewModel(StorageWith(playlist, new EffectPlaylist("SFX")), audio);

        Assert.Equal("Play", viewModel.PlayPauseButtonText);
        Assert.Equal("M8,5V19L19,12L8,5Z", viewModel.PlayPauseIconData);

        viewModel.PlayMusicTrackCommand.Execute(track);

        Assert.Equal("Pause", viewModel.PlayPauseButtonText);
        Assert.Equal("M7,5H10V19H7V5M14,5H17V19H14V5Z", viewModel.PlayPauseIconData);
        Assert.Equal(1, audio.PlayMusicCount);

        viewModel.PlayPauseCommand.Execute(null);

        Assert.Equal("Resume", viewModel.PlayPauseButtonText);
        Assert.Equal("M8,5V19L19,12L8,5Z", viewModel.PlayPauseIconData);
        Assert.Equal(1, audio.PauseCount);

        viewModel.PlayPauseCommand.Execute(null);

        Assert.Equal("Pause", viewModel.PlayPauseButtonText);
        Assert.Equal("M7,5H10V19H7V5M14,5H17V19H14V5Z", viewModel.PlayPauseIconData);
        Assert.Equal(1, audio.ResumeCount);
        Assert.Equal(1, audio.PlayMusicCount);

        viewModel.StopAllCommand.Execute(null);

        Assert.Equal("Play", viewModel.PlayPauseButtonText);
        Assert.Equal("M8,5V19L19,12L8,5Z", viewModel.PlayPauseIconData);
    }

    [Fact]
    public void StopAllCommandIsEnabledOnlyWhenPlaybackCanBeStopped()
    {
        var track = new Track("Track", "C:\\audio\\track.mp3", TrackRole.Music);
        var effect = new Track("Effect", "C:\\audio\\effect.wav", TrackRole.Effect);
        var playlist = new Playlist("Music", [track]);
        var effects = new EffectPlaylist("SFX", [effect]);
        using var viewModel = CreateViewModel(StorageWith(playlist, effects));

        Assert.False(viewModel.StopAllCommand.CanExecute(null));

        viewModel.PlayMusicTrackCommand.Execute(track);

        Assert.True(viewModel.StopAllCommand.CanExecute(null));

        viewModel.PlayPauseCommand.Execute(null);

        Assert.True(viewModel.StopAllCommand.CanExecute(null));

        viewModel.StopAllCommand.Execute(null);

        Assert.False(viewModel.StopAllCommand.CanExecute(null));

        viewModel.PlayEffectCommand.Execute(effect);

        Assert.True(viewModel.StopAllCommand.CanExecute(null));

        viewModel.StopAllCommand.Execute(null);

        Assert.False(viewModel.StopAllCommand.CanExecute(null));
    }

    [Fact]
    public void PreviousAndNextCommandsFollowAvailableMusicTracks()
    {
        var emptyPlaylist = new Playlist("Empty");
        var track = new Track("Track", "C:\\audio\\track.mp3", TrackRole.Music);
        var populatedPlaylist = new Playlist("Music", [track]);
        var audio = new FakeAudioService();
        using var viewModel = CreateViewModel(StorageWithMusicPlaylists([emptyPlaylist, populatedPlaylist]), audio);

        Assert.False(viewModel.PreviousTrackCommand.CanExecute(null));
        Assert.False(viewModel.NextTrackCommand.CanExecute(null));

        viewModel.SelectedMusicPlaylist = populatedPlaylist;

        Assert.True(viewModel.PreviousTrackCommand.CanExecute(null));
        Assert.True(viewModel.NextTrackCommand.CanExecute(null));

        viewModel.NextTrackCommand.Execute(null);

        Assert.Equal(track.Id, viewModel.CurrentTrack?.Id);
        Assert.Equal(track.Id, audio.LastMusicTrack?.Id);
        Assert.True(viewModel.IsPlaying);
    }

    [Fact]
    public void PlaybackProgressReflectsAudioPositionAndSupportsSeek()
    {
        var track = new Track("Track", "C:\\audio\\track.mp3", TrackRole.Music);
        var playlist = new Playlist("Music", [track]);
        var audio = new FakeAudioService
        {
            MusicPosition = TimeSpan.FromSeconds(65),
            MusicDuration = TimeSpan.FromSeconds(185)
        };
        using var viewModel = CreateViewModel(StorageWith(playlist, new EffectPlaylist("SFX")), audio);
        var changedProperties = new List<string>();
        viewModel.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName ?? "");

        viewModel.RefreshPlaybackProgress();

        Assert.Equal(65, viewModel.PlaybackPositionSeconds);
        Assert.Equal(185, viewModel.PlaybackDurationSeconds);
        Assert.Equal("01:05 / 03:05", viewModel.PlaybackTimeText);
        Assert.Contains(nameof(MainWindowViewModel.PlaybackPositionSeconds), changedProperties);
        Assert.Contains(nameof(MainWindowViewModel.PlaybackDurationSeconds), changedProperties);
        Assert.Contains(nameof(MainWindowViewModel.PlaybackTimeText), changedProperties);

        viewModel.PlaybackPositionSeconds = 90;

        Assert.Equal(TimeSpan.FromSeconds(90), audio.LastSeekPosition);
    }

    [Fact]
    public void PlaybackFailureMarksStatusAsErrorUntilNextSuccessfulPlayback()
    {
        var track = new Track("Track", "C:\\audio\\track.mp3", TrackRole.Music);
        var playlist = new Playlist("Music", [track]);
        var audio = new FakeAudioService { ThrowOnPlayMusic = true };
        using var viewModel = CreateViewModel(StorageWith(playlist, new EffectPlaylist("SFX")), audio);
        var changedProperties = new List<string>();
        viewModel.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName ?? "");

        Assert.False(viewModel.IsStatusError);

        viewModel.PlayMusicTrackCommand.Execute(track);

        Assert.True(viewModel.IsStatusError);
        Assert.StartsWith("Failed to play file: Track.", viewModel.StatusMessage);
        Assert.Contains(nameof(MainWindowViewModel.IsStatusError), changedProperties);
        Assert.Contains(nameof(MainWindowViewModel.StatusMessageBrush), changedProperties);

        audio.ThrowOnPlayMusic = false;
        changedProperties.Clear();
        viewModel.PlayMusicTrackCommand.Execute(track);

        Assert.False(viewModel.IsStatusError);
        Assert.Empty(viewModel.StatusMessage);
        Assert.Contains(nameof(MainWindowViewModel.IsStatusError), changedProperties);
        Assert.Contains(nameof(MainWindowViewModel.StatusMessageBrush), changedProperties);
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
    public void EffectsPlaybackStatusReflectsActiveEffectsAndDucking()
    {
        var effect = new Track("Door slam", "C:\\audio\\door.wav", TrackRole.Effect);
        var audio = new FakeAudioService();
        using var viewModel = CreateViewModel(
            StorageWith(new Playlist("Music"), new EffectPlaylist("SFX", [effect])),
            audio);
        var changedProperties = new List<string>();
        viewModel.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName ?? "");

        Assert.Equal("SFX idle", viewModel.EffectsPlaybackStatus);
        Assert.Equal("Ducking off", viewModel.DuckingStatus);
        Assert.False(viewModel.StopEffectsCommand.CanExecute(null));

        viewModel.PlayEffectCommand.Execute(effect);

        Assert.Equal("1 SFX active", viewModel.EffectsPlaybackStatus);
        Assert.Equal("Ducking active", viewModel.DuckingStatus);
        Assert.True(viewModel.StopEffectsCommand.CanExecute(null));
        Assert.Contains(nameof(MainWindowViewModel.EffectsPlaybackStatus), changedProperties);
        Assert.Contains(nameof(MainWindowViewModel.DuckingStatus), changedProperties);

        changedProperties.Clear();
        viewModel.StopEffectsCommand.Execute(null);

        Assert.Equal("SFX idle", viewModel.EffectsPlaybackStatus);
        Assert.Equal("Ducking off", viewModel.DuckingStatus);
        Assert.False(viewModel.StopEffectsCommand.CanExecute(null));
        Assert.Contains(nameof(MainWindowViewModel.EffectsPlaybackStatus), changedProperties);
        Assert.Contains(nameof(MainWindowViewModel.DuckingStatus), changedProperties);
    }

    [Fact]
    public void DropTargetStateHighlightsOnlyOneDeckAndClearsAfterDrop()
    {
        using var viewModel = CreateViewModel(StorageWith(new Playlist("Music"), new EffectPlaylist("SFX")));
        var changedProperties = new List<string>();
        viewModel.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName ?? "");

        Assert.False(viewModel.IsMusicDropTargetActive);
        Assert.False(viewModel.IsEffectDropTargetActive);

        viewModel.SetMusicDropTargetActive(true);

        Assert.True(viewModel.IsMusicDropTargetActive);
        Assert.False(viewModel.IsEffectDropTargetActive);
        Assert.Contains(nameof(MainWindowViewModel.IsMusicDropTargetActive), changedProperties);

        viewModel.SetEffectDropTargetActive(true);

        Assert.False(viewModel.IsMusicDropTargetActive);
        Assert.True(viewModel.IsEffectDropTargetActive);
        Assert.Contains(nameof(MainWindowViewModel.IsEffectDropTargetActive), changedProperties);

        viewModel.ImportDroppedEffects([]);

        Assert.False(viewModel.IsMusicDropTargetActive);
        Assert.False(viewModel.IsEffectDropTargetActive);
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
    public void HotkeyStartedMusicShowsPlaybackPlaylistWithoutChangingSelectedPlaylist()
    {
        var selectedPlaylist = new Playlist("Selected", [new Track("Selected Track", "C:\\audio\\selected.mp3", TrackRole.Music)]);
        var targetTrack = new Track("Boss Theme", "C:\\audio\\boss.mp3", TrackRole.Music);
        var hotkeyPlaylist = new Playlist("Boss", [targetTrack]);
        var hotkey = new Hotkey(0, "A", HotkeyModifier.None);
        var storage = StorageWithMusicPlaylists(
            [selectedPlaylist, hotkeyPlaylist],
            new HotkeyConfiguration(
            [
                new HotkeyBinding(HotkeyAction.PlayMusicTrack(hotkeyPlaylist.Id, targetTrack.Id), hotkey)
            ]));
        var audio = new FakeAudioService();
        using var viewModel = CreateViewModel(storage, audio);
        var changedProperties = new List<string>();
        viewModel.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName ?? "");

        Assert.Equal("Selected", viewModel.CurrentPlaybackPlaylistName);

        Assert.True(viewModel.HandleHotkey(hotkey));

        Assert.Equal(selectedPlaylist.Id, viewModel.SelectedMusicPlaylist?.Id);
        Assert.Equal(targetTrack.Id, viewModel.CurrentTrack?.Id);
        Assert.Equal(targetTrack.Id, audio.LastMusicTrack?.Id);
        Assert.Equal("Boss", viewModel.CurrentPlaybackPlaylistName);
        Assert.Contains(nameof(MainWindowViewModel.CurrentPlaybackPlaylistName), changedProperties);
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
    public void RequestDeletePlaylistItemShowsConfirmationAndCancelKeepsPlaylist()
    {
        var selectedPlaylist = new Playlist("Selected", [new Track("Selected Track", "C:\\audio\\selected.mp3", TrackRole.Music)]);
        var removedPlaylist = new Playlist("Remove Me", [new Track("Removed Track", "C:\\audio\\removed.mp3", TrackRole.Music)]);
        var storage = StorageWithMusicPlaylists([selectedPlaylist, removedPlaylist]);
        using var viewModel = CreateViewModel(storage);

        viewModel.RequestDeleteMusicPlaylistItemCommand.Execute(removedPlaylist);

        Assert.True(viewModel.IsDeleteConfirmationVisible);
        Assert.Equal("Delete music playlist?", viewModel.DeleteConfirmationTitle);
        Assert.Contains("Remove Me", viewModel.DeleteConfirmationMessage);

        viewModel.CancelDeleteCommand.Execute(null);

        Assert.False(viewModel.IsDeleteConfirmationVisible);
        Assert.Equal(2, viewModel.MusicPlaylists.Count);
        Assert.Contains(viewModel.MusicPlaylists, playlist => playlist.Id == removedPlaylist.Id);
    }

    [Fact]
    public void ConfirmDeletePlaylistItemRemovesRequestedPlaylistWithoutChangingOtherSelection()
    {
        var selectedPlaylist = new Playlist("Selected", [new Track("Selected Track", "C:\\audio\\selected.mp3", TrackRole.Music)]);
        var removedPlaylist = new Playlist("Remove Me", [new Track("Removed Track", "C:\\audio\\removed.mp3", TrackRole.Music)]);
        var storage = StorageWithMusicPlaylists([selectedPlaylist, removedPlaylist]);
        using var viewModel = CreateViewModel(storage);

        viewModel.RequestDeleteMusicPlaylistItemCommand.Execute(removedPlaylist);
        viewModel.ConfirmDeleteCommand.Execute(null);

        Assert.False(viewModel.IsDeleteConfirmationVisible);
        Assert.Single(viewModel.MusicPlaylists);
        Assert.Equal(selectedPlaylist.Id, viewModel.MusicPlaylists[0].Id);
        Assert.Equal(selectedPlaylist.Id, viewModel.SelectedMusicPlaylist?.Id);
        Assert.True(storage.SaveCount > 0);
    }

    [Fact]
    public void ConfirmDeleteEffectPlaylistItemRemovesRequestedPlaylist()
    {
        var selectedPlaylist = new EffectPlaylist("Selected");
        var removedPlaylist = new EffectPlaylist("Remove Me");
        var storage = StorageWithEffectPlaylists([selectedPlaylist, removedPlaylist]);
        using var viewModel = CreateViewModel(storage);

        viewModel.RequestDeleteEffectPlaylistItemCommand.Execute(removedPlaylist);

        Assert.True(viewModel.IsDeleteConfirmationVisible);
        Assert.Equal("Delete SFX playlist?", viewModel.DeleteConfirmationTitle);

        viewModel.ConfirmDeleteCommand.Execute(null);

        Assert.False(viewModel.IsDeleteConfirmationVisible);
        Assert.Single(viewModel.EffectPlaylists);
        Assert.Equal(selectedPlaylist.Id, viewModel.EffectPlaylists[0].Id);
        Assert.Equal(selectedPlaylist.Id, viewModel.SelectedEffectPlaylist?.Id);
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

    private static FakeStorageService StorageWithMusicPlaylists(
        IReadOnlyList<Playlist> musicPlaylists,
        HotkeyConfiguration? hotkeys = null)
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
            },
            Hotkeys = hotkeys ?? HotkeyConfiguration.Defaults
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
        public bool ThrowOnPlayMusic { get; set; }
        public TimeSpan MusicPosition { get; set; }
        public TimeSpan MusicDuration { get; set; }
        public TimeSpan? LastSeekPosition { get; private set; }
        public double? LastSetMusicVolume { get; private set; }

        public void Dispose()
        {
        }

        public void PlayMusic(Track track, double volume)
        {
            if (ThrowOnPlayMusic)
            {
                throw new InvalidOperationException("Playback failed");
            }

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

        public void SeekMusic(TimeSpan position)
        {
            LastSeekPosition = position;
            MusicPosition = position;
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
            LastSetMusicVolume = volume;
        }

        public void SetEffectsVolume(double masterVolume)
        {
        }
    }
}
