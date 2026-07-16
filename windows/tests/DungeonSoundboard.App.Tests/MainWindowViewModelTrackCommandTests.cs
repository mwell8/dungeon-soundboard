using DungeonSoundboard.App.Services;
using DungeonSoundboard.App.ViewModels;
using DungeonSoundboard.Core.Models;
using DungeonSoundboard.Core.Services;
using Xunit;

namespace DungeonSoundboard.App.Tests;

public sealed class MainWindowViewModelTrackCommandTests
{
    [Fact]
    public void TrackTileKeepsIdentityAcrossBindingRefreshes()
    {
        var track = new Track("Track", "C:\\audio\\track.mp3", TrackRole.Music);
        using var viewModel = CreateViewModel(StorageWith(new Playlist("Music", [track]), new EffectPlaylist("SFX")));
        var first = Assert.Single(viewModel.MusicTrackTiles);

        viewModel.SelectedMusicTrackVolume = 1.25;

        var refreshed = Assert.Single(viewModel.MusicTrackTiles);
        Assert.Same(first, refreshed);
        Assert.Equal(1.25, refreshed.VolumeMultiplier);
    }

    [Fact]
    public void ConstructorAppliesPersistedEffectsVolumeToAudioService()
    {
        var music = new Playlist("Music");
        var effects = new EffectPlaylist("SFX");
        var audio = new FakeAudioService();
        var storage = new FakeStorageService(new AppState
        {
            MusicPlaylists = [music],
            EffectPlaylists = [effects],
            Preferences = new PlayerPreferences
            {
                SelectedMusicPlaylistId = music.Id,
                SelectedEffectPlaylistId = effects.Id,
                EffectsVolume = 0.37
            }
        });

        using var viewModel = CreateViewModel(storage, audio);

        Assert.Equal(0.37, viewModel.EffectsVolume);
        Assert.Equal(0.37, audio.LastSetEffectsVolume);
    }

    [Fact]
    public void TrackTileViewModelsCompareByTrackId()
    {
        var id = Guid.NewGuid();
        var first = new Track("First", "C:\\audio\\first.mp3", TrackRole.Music, id);
        var second = new Track("Second", "C:\\audio\\second.mp3", TrackRole.Music, id);

        var firstTile = new TrackTileViewModel(first, hotkeyText: null, isCurrent: false, isFileMissing: false, "Missing", "Missing file", "File available", "Missing");
        var secondTile = new TrackTileViewModel(second, hotkeyText: "A", isCurrent: true, isFileMissing: true, "Missing", "Missing file", "File available", "Missing");

        Assert.Equal(firstTile, secondTile);
        Assert.Equal(firstTile.GetHashCode(), secondTile.GetHashCode());
    }

    [Fact]
    public void MusicTrackSearchFiltersTilesByTitleOrPathWithoutChangingPlaylistSelection()
    {
        var first = new Track("Rain Ambience", "C:\\audio\\forest_rain.mp3", TrackRole.Music);
        var second = new Track("Battle Drums", "C:\\audio\\combat_drums.mp3", TrackRole.Music);
        var playlist = new Playlist("Music", [first, second]);
        using var viewModel = CreateViewModel(StorageWith(playlist, new EffectPlaylist("SFX")));

        viewModel.MusicSearchText = "rain";

        Assert.Equal(playlist.Id, viewModel.SelectedMusicPlaylist?.Id);
        Assert.False(viewModel.IsMusicSearchEmpty);
        Assert.Equal(first.Id, Assert.Single(viewModel.MusicTrackTiles).Track.Id);

        viewModel.MusicSearchText = "combat";

        Assert.Equal(second.Id, Assert.Single(viewModel.MusicTrackTiles).Track.Id);

        viewModel.MusicSearchText = "missing";

        Assert.Empty(viewModel.MusicTrackTiles);
        Assert.False(viewModel.IsMusicTracksEmpty);
        Assert.True(viewModel.IsMusicSearchEmpty);
    }

    [Fact]
    public void MusicTrackSearchClearsSelectionThatIsNoLongerVisible()
    {
        var first = new Track("Rain Ambience", "C:\\audio\\forest_rain.mp3", TrackRole.Music);
        var second = new Track("Battle Drums", "C:\\audio\\combat_drums.mp3", TrackRole.Music);
        var playlist = new Playlist("Music", [first, second]);
        using var viewModel = CreateViewModel(StorageWith(playlist, new EffectPlaylist("SFX")));
        viewModel.SelectedMusicTrack = second;
        viewModel.SetSelectedMusicTrackTiles(viewModel.MusicTrackTiles.Where(tile => tile.Track.Id == second.Id));

        viewModel.MusicSearchText = "rain";

        Assert.Equal(first.Id, Assert.Single(viewModel.MusicTrackTiles).Track.Id);
        Assert.Null(viewModel.SelectedMusicTrack);
        Assert.False(viewModel.HasSelectedMusicTrack);
        Assert.False(viewModel.HasMusicTrackDeleteSelection);
        Assert.False(viewModel.HasMusicMultiSelection);
    }

    [Fact]
    public void EffectTrackSearchFiltersTilesByTitleOrPath()
    {
        var first = new Track("Door Slam", "C:\\audio\\door.wav", TrackRole.Effect);
        var second = new Track("Crowd Laugh", "C:\\audio\\laugh.wav", TrackRole.Effect);
        var playlist = new EffectPlaylist("SFX", [first, second]);
        using var viewModel = CreateViewModel(StorageWith(new Playlist("Music"), playlist));

        viewModel.EffectSearchText = "laugh";

        Assert.False(viewModel.IsEffectSearchEmpty);
        Assert.Equal(second.Id, Assert.Single(viewModel.EffectTrackTiles).Track.Id);

        viewModel.EffectSearchText = "absent";

        Assert.Empty(viewModel.EffectTrackTiles);
        Assert.False(viewModel.IsEffectTracksEmpty);
        Assert.True(viewModel.IsEffectSearchEmpty);
    }

    [Fact]
    public void EffectTrackSearchClearsSelectionThatIsNoLongerVisible()
    {
        var first = new Track("Door Slam", "C:\\audio\\door.wav", TrackRole.Effect);
        var second = new Track("Crowd Laugh", "C:\\audio\\laugh.wav", TrackRole.Effect);
        var playlist = new EffectPlaylist("SFX", [first, second]);
        using var viewModel = CreateViewModel(StorageWith(new Playlist("Music"), playlist));
        viewModel.SelectedEffectTrack = first;
        viewModel.SetSelectedEffectTrackTiles(viewModel.EffectTrackTiles.Where(tile => tile.Track.Id == first.Id));

        viewModel.EffectSearchText = "laugh";

        Assert.Equal(second.Id, Assert.Single(viewModel.EffectTrackTiles).Track.Id);
        Assert.Null(viewModel.SelectedEffectTrack);
        Assert.False(viewModel.HasSelectedEffectTrack);
        Assert.False(viewModel.HasEffectTrackDeleteSelection);
        Assert.False(viewModel.HasEffectMultiSelection);
    }

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
    public void ConfirmDeleteMusicTracksRemovesRequestedTracksWithOneSave()
    {
        var first = new Track("First", "C:\\audio\\first.mp3", TrackRole.Music);
        var second = new Track("Second", "C:\\audio\\second.mp3", TrackRole.Music);
        var third = new Track("Third", "C:\\audio\\third.mp3", TrackRole.Music);
        var playlist = new Playlist("Music", [first, second, third]);
        var storage = StorageWith(playlist, new EffectPlaylist("SFX"));
        using var viewModel = CreateViewModel(storage);
        var saveCountBeforeDelete = storage.SaveCount;

        viewModel.RequestDeleteMusicTracks([first, third]);

        Assert.True(viewModel.IsDeleteConfirmationVisible);
        Assert.Equal("Delete 2 music tracks?", viewModel.DeleteConfirmationTitle);
        Assert.Contains("Remove 2 tracks", viewModel.DeleteConfirmationMessage);

        viewModel.ConfirmDeleteCommand.Execute(null);

        Assert.False(viewModel.IsDeleteConfirmationVisible);
        Assert.Single(viewModel.MusicTracks);
        Assert.Equal(second.Id, viewModel.MusicTracks[0].Id);
        Assert.Equal(second.Id, viewModel.SelectedMusicTrack?.Id);
        Assert.Equal(saveCountBeforeDelete + 1, storage.SaveCount);
    }

    [Fact]
    public void ConfirmDeleteEffectTracksRemovesRequestedEffectsWithOneSave()
    {
        var first = new Track("First", "C:\\audio\\first.wav", TrackRole.Effect);
        var second = new Track("Second", "C:\\audio\\second.wav", TrackRole.Effect);
        var third = new Track("Third", "C:\\audio\\third.wav", TrackRole.Effect);
        var effects = new EffectPlaylist("SFX", [first, second, third]);
        var storage = StorageWith(new Playlist("Music"), effects);
        using var viewModel = CreateViewModel(storage);
        var saveCountBeforeDelete = storage.SaveCount;

        viewModel.RequestDeleteEffectTracks([first, third]);

        Assert.True(viewModel.IsDeleteConfirmationVisible);
        Assert.Equal("Delete 2 SFX tracks?", viewModel.DeleteConfirmationTitle);
        Assert.Contains("Remove 2 tracks", viewModel.DeleteConfirmationMessage);

        viewModel.ConfirmDeleteCommand.Execute(null);

        Assert.False(viewModel.IsDeleteConfirmationVisible);
        Assert.Single(viewModel.EffectTracks);
        Assert.Equal(second.Id, viewModel.EffectTracks[0].Id);
        Assert.Equal(second.Id, viewModel.SelectedEffectTrack?.Id);
        Assert.Equal(saveCountBeforeDelete + 1, storage.SaveCount);
    }

    [Fact]
    public void RequestDeleteTracksIgnoresMissingTracks()
    {
        var first = new Track("First", "C:\\audio\\first.mp3", TrackRole.Music);
        var second = new Track("Second", "C:\\audio\\second.mp3", TrackRole.Music);
        var missing = new Track("Missing", "C:\\audio\\missing.mp3", TrackRole.Music);
        var playlist = new Playlist("Music", [first, second]);
        var storage = StorageWith(playlist, new EffectPlaylist("SFX"));
        using var viewModel = CreateViewModel(storage);
        var saveCountBeforeDelete = storage.SaveCount;

        viewModel.RequestDeleteMusicTracks([missing]);

        Assert.False(viewModel.IsDeleteConfirmationVisible);
        Assert.Equal(2, viewModel.MusicTracks.Count);
        Assert.Equal(saveCountBeforeDelete, storage.SaveCount);
    }

    [Fact]
    public void DeleteSelectedMusicTrackCommandUsesMultiSelectionWhenPresent()
    {
        var first = new Track("First", "C:\\audio\\first.mp3", TrackRole.Music);
        var second = new Track("Second", "C:\\audio\\second.mp3", TrackRole.Music);
        var third = new Track("Third", "C:\\audio\\third.mp3", TrackRole.Music);
        var playlist = new Playlist("Music", [first, second, third]);
        var storage = StorageWith(playlist, new EffectPlaylist("SFX"));
        using var viewModel = CreateViewModel(storage);
        var selectedTiles = viewModel.MusicTrackTiles
            .Where(tile => tile.Track.Id == first.Id || tile.Track.Id == third.Id)
            .ToList();

        viewModel.SetSelectedMusicTrackTiles(selectedTiles);
        viewModel.RequestDeleteMusicTrackCommand.Execute(null);

        Assert.True(viewModel.IsDeleteConfirmationVisible);
        Assert.Equal("Delete 2 music tracks?", viewModel.DeleteConfirmationTitle);

        viewModel.ConfirmDeleteCommand.Execute(null);

        Assert.Single(viewModel.MusicTracks);
        Assert.Equal(second.Id, viewModel.MusicTracks[0].Id);
    }

    [Fact]
    public void DeleteSelectedEffectTrackCommandUsesMultiSelectionWhenPresent()
    {
        var first = new Track("First", "C:\\audio\\first.wav", TrackRole.Effect);
        var second = new Track("Second", "C:\\audio\\second.wav", TrackRole.Effect);
        var third = new Track("Third", "C:\\audio\\third.wav", TrackRole.Effect);
        var effects = new EffectPlaylist("SFX", [first, second, third]);
        var storage = StorageWith(new Playlist("Music"), effects);
        using var viewModel = CreateViewModel(storage);
        var selectedTiles = viewModel.EffectTrackTiles
            .Where(tile => tile.Track.Id == first.Id || tile.Track.Id == third.Id)
            .ToList();

        viewModel.SetSelectedEffectTrackTiles(selectedTiles);
        viewModel.RequestDeleteEffectTrackCommand.Execute(null);

        Assert.True(viewModel.IsDeleteConfirmationVisible);
        Assert.Equal("Delete 2 SFX tracks?", viewModel.DeleteConfirmationTitle);

        viewModel.ConfirmDeleteCommand.Execute(null);

        Assert.Single(viewModel.EffectTracks);
        Assert.Equal(second.Id, viewModel.EffectTracks[0].Id);
    }

    [Fact]
    public void RequestDeleteMusicTrackItemUsesMultiSelectionWhenRequestedTrackIsSelected()
    {
        var first = new Track("First", "C:\\audio\\first.mp3", TrackRole.Music);
        var second = new Track("Second", "C:\\audio\\second.mp3", TrackRole.Music);
        var third = new Track("Third", "C:\\audio\\third.mp3", TrackRole.Music);
        var playlist = new Playlist("Music", [first, second, third]);
        var storage = StorageWith(playlist, new EffectPlaylist("SFX"));
        using var viewModel = CreateViewModel(storage);
        var selectedTiles = viewModel.MusicTrackTiles
            .Where(tile => tile.Track.Id == first.Id || tile.Track.Id == third.Id)
            .ToList();

        viewModel.SetSelectedMusicTrackTiles(selectedTiles);
        viewModel.RequestDeleteMusicTrackItemCommand.Execute(third);

        Assert.True(viewModel.IsDeleteConfirmationVisible);
        Assert.Equal("Delete 2 music tracks?", viewModel.DeleteConfirmationTitle);

        viewModel.ConfirmDeleteCommand.Execute(null);

        Assert.Single(viewModel.MusicTracks);
        Assert.Equal(second.Id, viewModel.MusicTracks[0].Id);
    }

    [Fact]
    public void RequestDeleteMusicTrackItemKeepsSingleTargetWhenRequestedTrackIsNotSelected()
    {
        var first = new Track("First", "C:\\audio\\first.mp3", TrackRole.Music);
        var second = new Track("Second", "C:\\audio\\second.mp3", TrackRole.Music);
        var third = new Track("Third", "C:\\audio\\third.mp3", TrackRole.Music);
        var playlist = new Playlist("Music", [first, second, third]);
        var storage = StorageWith(playlist, new EffectPlaylist("SFX"));
        using var viewModel = CreateViewModel(storage);
        var selectedTiles = viewModel.MusicTrackTiles
            .Where(tile => tile.Track.Id == first.Id || tile.Track.Id == third.Id)
            .ToList();

        viewModel.SetSelectedMusicTrackTiles(selectedTiles);
        viewModel.RequestDeleteMusicTrackItemCommand.Execute(second);

        Assert.True(viewModel.IsDeleteConfirmationVisible);
        Assert.Equal("Delete music track?", viewModel.DeleteConfirmationTitle);
        Assert.Contains("Second", viewModel.DeleteConfirmationMessage);

        viewModel.ConfirmDeleteCommand.Execute(null);

        Assert.Equal(2, viewModel.MusicTracks.Count);
        Assert.Contains(viewModel.MusicTracks, track => track.Id == first.Id);
        Assert.Contains(viewModel.MusicTracks, track => track.Id == third.Id);
    }

    [Fact]
    public void RequestDeleteEffectTrackItemUsesMultiSelectionWhenRequestedTrackIsSelected()
    {
        var first = new Track("First", "C:\\audio\\first.wav", TrackRole.Effect);
        var second = new Track("Second", "C:\\audio\\second.wav", TrackRole.Effect);
        var third = new Track("Third", "C:\\audio\\third.wav", TrackRole.Effect);
        var effects = new EffectPlaylist("SFX", [first, second, third]);
        var storage = StorageWith(new Playlist("Music"), effects);
        using var viewModel = CreateViewModel(storage);
        var selectedTiles = viewModel.EffectTrackTiles
            .Where(tile => tile.Track.Id == first.Id || tile.Track.Id == third.Id)
            .ToList();

        viewModel.SetSelectedEffectTrackTiles(selectedTiles);
        viewModel.RequestDeleteEffectTrackItemCommand.Execute(first);

        Assert.True(viewModel.IsDeleteConfirmationVisible);
        Assert.Equal("Delete 2 SFX tracks?", viewModel.DeleteConfirmationTitle);

        viewModel.ConfirmDeleteCommand.Execute(null);

        Assert.Single(viewModel.EffectTracks);
        Assert.Equal(second.Id, viewModel.EffectTracks[0].Id);
    }

    [Fact]
    public void TrackSelectionSummaryAndDeleteTooltipsFollowMultiSelection()
    {
        var first = new Track("First", "C:\\audio\\first.mp3", TrackRole.Music);
        var second = new Track("Second", "C:\\audio\\second.mp3", TrackRole.Music);
        var third = new Track("Third", "C:\\audio\\third.mp3", TrackRole.Music);
        var effectOne = new Track("Hit", "C:\\audio\\hit.wav", TrackRole.Effect);
        var effectTwo = new Track("Crash", "C:\\audio\\crash.wav", TrackRole.Effect);
        using var viewModel = CreateViewModel(
            StorageWith(new Playlist("Music", [first, second, third]), new EffectPlaylist("SFX", [effectOne, effectTwo])));

        viewModel.SetSelectedMusicTrackTiles(viewModel.MusicTrackTiles.Where(tile => tile.Track.Id != second.Id));
        viewModel.SetSelectedEffectTrackTiles(viewModel.EffectTrackTiles);

        Assert.True(viewModel.HasMusicMultiSelection);
        Assert.True(viewModel.HasEffectMultiSelection);
        Assert.Equal("2 selected", viewModel.MusicSelectionSummary);
        Assert.Equal("2 selected", viewModel.EffectSelectionSummary);
        Assert.Equal("Delete 2 selected tracks", viewModel.MusicDeleteSelectionToolTip);
        Assert.Equal("Delete 2 selected SFX", viewModel.EffectDeleteSelectionToolTip);

        viewModel.SetSelectedMusicTrackTiles([]);
        viewModel.SetSelectedEffectTrackTiles([]);

        Assert.False(viewModel.HasMusicMultiSelection);
        Assert.False(viewModel.HasEffectMultiSelection);
        Assert.Equal("", viewModel.MusicSelectionSummary);
        Assert.Equal("", viewModel.EffectSelectionSummary);
        Assert.Equal("Delete selected track", viewModel.MusicDeleteSelectionToolTip);
        Assert.Equal("Delete selected SFX", viewModel.EffectDeleteSelectionToolTip);
    }

    [Fact]
    public void EscapeCancelsDeleteConfirmationWithoutRemovingTrack()
    {
        var first = new Track("First", "C:\\audio\\first.mp3", TrackRole.Music);
        var second = new Track("Second", "C:\\audio\\second.mp3", TrackRole.Music);
        var playlist = new Playlist("Music", [first, second]);
        var storage = StorageWith(playlist, new EffectPlaylist("SFX"));
        using var viewModel = CreateViewModel(storage);
        var saveCountBeforeDelete = storage.SaveCount;

        viewModel.RequestDeleteMusicTrackItemCommand.Execute(second);

        Assert.True(viewModel.HandleDialogHotkey(new Hotkey(HotkeyConfiguration.EscapeKeyCode, "Esc", HotkeyModifier.None)));

        Assert.False(viewModel.IsDeleteConfirmationVisible);
        Assert.Equal(2, viewModel.MusicTracks.Count);
        Assert.Contains(viewModel.MusicTracks, track => track.Id == second.Id);
        Assert.Equal(saveCountBeforeDelete, storage.SaveCount);
    }

    [Fact]
    public void ReturnConfirmsDeleteConfirmation()
    {
        var first = new Track("First", "C:\\audio\\first.mp3", TrackRole.Music);
        var second = new Track("Second", "C:\\audio\\second.mp3", TrackRole.Music);
        var playlist = new Playlist("Music", [first, second]);
        var storage = StorageWith(playlist, new EffectPlaylist("SFX"));
        using var viewModel = CreateViewModel(storage);

        viewModel.RequestDeleteMusicTrackItemCommand.Execute(second);

        Assert.True(viewModel.HandleDialogHotkey(new Hotkey(HotkeyConfiguration.ReturnKeyCode, "Return", HotkeyModifier.None)));

        Assert.False(viewModel.IsDeleteConfirmationVisible);
        Assert.Single(viewModel.MusicTracks);
        Assert.Equal(first.Id, viewModel.MusicTracks[0].Id);
        Assert.True(storage.SaveCount > 0);
    }

    [Fact]
    public void OpenDialogConsumesNonDialogHotkeysWithoutExecutingPlayback()
    {
        var track = new Track("Track", "C:\\audio\\track.mp3", TrackRole.Music);
        var playlist = new Playlist("Music", [track]);
        var audio = new FakeAudioService();
        using var viewModel = CreateViewModel(StorageWith(playlist, new EffectPlaylist("SFX")), audio);

        viewModel.RequestDeleteMusicTrackItemCommand.Execute(track);

        Assert.True(viewModel.HandleDialogHotkey(new Hotkey(0, "Space", HotkeyModifier.None)));
        Assert.False(viewModel.IsPlaying);
        Assert.Null(audio.LastMusicTrack);
        Assert.True(viewModel.IsDeleteConfirmationVisible);
    }

    [Fact]
    public void RequestRenameTrackClosesOpenDeleteConfirmation()
    {
        var first = new Track("First", "C:\\audio\\first.mp3", TrackRole.Music);
        var second = new Track("Second", "C:\\audio\\second.mp3", TrackRole.Music);
        var playlist = new Playlist("Music", [first, second]);
        using var viewModel = CreateViewModel(StorageWith(playlist, new EffectPlaylist("SFX")));

        viewModel.RequestDeleteMusicTrackItemCommand.Execute(second);
        viewModel.RequestRenameMusicTrackItemCommand.Execute(first);

        Assert.False(viewModel.IsDeleteConfirmationVisible);
        Assert.True(viewModel.IsTrackRenameVisible);
        Assert.Equal("First", viewModel.TrackRenameName);
    }

    [Fact]
    public void RequestDeletePlaylistClosesOpenTrackRenameDialogWithoutSavingRename()
    {
        var keptTrack = new Track("Kept Track", "C:\\audio\\kept.mp3", TrackRole.Music);
        var selectedPlaylist = new Playlist("Selected", [keptTrack]);
        var removedPlaylist = new Playlist("Remove Me", [new Track("Removed Track", "C:\\audio\\removed.mp3", TrackRole.Music)]);
        var storage = StorageWithMusicPlaylists([selectedPlaylist, removedPlaylist]);
        using var viewModel = CreateViewModel(storage);
        var saveCountBeforeDeleteRequest = storage.SaveCount;

        viewModel.RequestRenameMusicTrackItemCommand.Execute(keptTrack);
        viewModel.TrackRenameName = "Changed";
        viewModel.RequestDeleteMusicPlaylistItemCommand.Execute(removedPlaylist);

        Assert.False(viewModel.IsTrackRenameVisible);
        Assert.Equal("", viewModel.TrackRenameName);
        Assert.Equal("Kept Track", keptTrack.Title);
        Assert.Equal(saveCountBeforeDeleteRequest, storage.SaveCount);
        Assert.True(viewModel.IsDeleteConfirmationVisible);
        Assert.Contains("Remove Me", viewModel.DeleteConfirmationMessage);
    }

    [Fact]
    public void DialogHotkeyHandlerIgnoresKeysWhenNoDialogIsOpen()
    {
        var track = new Track("Track", "C:\\audio\\track.mp3", TrackRole.Music);
        var playlist = new Playlist("Music", [track]);
        using var viewModel = CreateViewModel(StorageWith(playlist, new EffectPlaylist("SFX")));

        Assert.False(viewModel.HandleDialogHotkey(new Hotkey(0, "Space", HotkeyModifier.None)));
    }

    [Fact]
    public void RequestRenameTrackItemShowsDialogAndCancelKeepsTrack()
    {
        var first = new Track("First", "C:\\audio\\first.mp3", TrackRole.Music);
        var second = new Track("Second", "C:\\audio\\second.mp3", TrackRole.Music);
        var playlist = new Playlist("Music", [first, second]);
        var storage = StorageWith(playlist, new EffectPlaylist("SFX"));
        using var viewModel = CreateViewModel(storage);
        var saveCountBeforeRename = storage.SaveCount;

        viewModel.RequestRenameMusicTrackItemCommand.Execute(second);

        Assert.True(viewModel.IsTrackRenameVisible);
        Assert.Equal("Rename music track", viewModel.TrackRenameTitle);
        Assert.Equal("Second", viewModel.TrackRenameName);

        viewModel.TrackRenameName = "Boss Theme";
        viewModel.CancelTrackRenameCommand.Execute(null);

        Assert.False(viewModel.IsTrackRenameVisible);
        Assert.Equal("Second", second.Title);
        Assert.Equal(saveCountBeforeRename, storage.SaveCount);
    }

    [Fact]
    public void ConfirmRenameMusicTrackItemRenamesRequestedTrackWithoutChangingSelectionOrStartingAudio()
    {
        var first = new Track("First", "C:\\audio\\first.mp3", TrackRole.Music);
        var second = new Track("Second", "C:\\audio\\second.mp3", TrackRole.Music);
        var playlist = new Playlist("Music", [first, second]);
        var storage = StorageWith(playlist, new EffectPlaylist("SFX"));
        var audio = new FakeAudioService();
        using var viewModel = CreateViewModel(storage, audio);

        viewModel.RequestRenameMusicTrackItemCommand.Execute(second);
        viewModel.TrackRenameName = "Boss Theme";
        viewModel.ConfirmTrackRenameCommand.Execute(null);

        Assert.False(viewModel.IsTrackRenameVisible);
        Assert.Equal(first.Id, viewModel.SelectedMusicTrack?.Id);
        Assert.Equal("Boss Theme", second.Title);
        Assert.Equal("Boss Theme", Assert.Single(viewModel.MusicTrackTiles, tile => tile.Track.Id == second.Id).Title);
        Assert.Null(audio.LastMusicTrack);
        Assert.True(storage.SaveCount > 0);
    }

    [Fact]
    public void ConfirmRenameEffectTrackItemRenamesRequestedEffectWithoutChangingSelection()
    {
        var first = new Track("First", "C:\\audio\\first.wav", TrackRole.Effect);
        var second = new Track("Second", "C:\\audio\\second.wav", TrackRole.Effect);
        var effects = new EffectPlaylist("SFX", [first, second]);
        var storage = StorageWith(new Playlist("Music"), effects);
        using var viewModel = CreateViewModel(storage);

        viewModel.RequestRenameEffectTrackItemCommand.Execute(second);
        viewModel.TrackRenameName = "Door Slam";
        viewModel.ConfirmTrackRenameCommand.Execute(null);

        Assert.False(viewModel.IsTrackRenameVisible);
        Assert.Equal(first.Id, viewModel.SelectedEffectTrack?.Id);
        Assert.Equal("Door Slam", second.Title);
        Assert.Equal("Door Slam", Assert.Single(viewModel.EffectTrackTiles, tile => tile.Track.Id == second.Id).Title);
        Assert.True(storage.SaveCount > 0);
    }

    [Fact]
    public void EscapeCancelsTrackRenameWithoutSaving()
    {
        var first = new Track("First", "C:\\audio\\first.wav", TrackRole.Effect);
        var second = new Track("Second", "C:\\audio\\second.wav", TrackRole.Effect);
        var effects = new EffectPlaylist("SFX", [first, second]);
        var storage = StorageWith(new Playlist("Music"), effects);
        using var viewModel = CreateViewModel(storage);

        viewModel.RequestRenameEffectTrackItemCommand.Execute(second);
        var saveCountBeforeCancel = storage.SaveCount;
        viewModel.TrackRenameName = "Door Slam";

        Assert.True(viewModel.HandleDialogHotkey(new Hotkey(HotkeyConfiguration.EscapeKeyCode, "Esc", HotkeyModifier.None)));

        Assert.False(viewModel.IsTrackRenameVisible);
        Assert.Equal("Second", second.Title);
        Assert.Equal(saveCountBeforeCancel, storage.SaveCount);
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
    public void RestoreDefaultHotkeysRestoresSystemBindingsAndClearsTrackBindings()
    {
        var music = new Track("Music", "C:\\audio\\music.mp3", TrackRole.Music);
        var playlist = new Playlist("Music", [music]);
        var storage = StorageWithMusicPlaylists(
            [playlist],
            hotkeys: new HotkeyConfiguration(
            [
                new HotkeyBinding(HotkeyAction.StopAll, new Hotkey(0, "A", HotkeyModifier.None)),
                new HotkeyBinding(HotkeyAction.PlayMusicTrack(playlist.Id, music.Id), new Hotkey(1, "B", HotkeyModifier.None))
            ]));
        using var viewModel = CreateViewModel(storage);

        viewModel.BindSystemHotkeyCommand.Execute(HotkeyAction.MusicVolumeUp);
        Assert.True(viewModel.IsCapturingHotkey);

        viewModel.RestoreDefaultHotkeysCommand.Execute(null);

        Assert.False(viewModel.IsCapturingHotkey);
        Assert.Equal(HotkeyConfiguration.Defaults, storage.Load().Hotkeys);
        Assert.Equal("Space", Assert.Single(viewModel.SystemHotkeyRows, row => row.Action.Equals(HotkeyAction.PlayPause)).HotkeyText);
        Assert.Equal("Delete", Assert.Single(viewModel.SystemHotkeyRows, row => row.Action.Equals(HotkeyAction.StopEffects)).HotkeyText);
        Assert.Equal("Unassigned", Assert.Single(viewModel.SystemHotkeyRows, row => row.Action.Equals(HotkeyAction.StopAll)).HotkeyText);
        Assert.False(Assert.Single(viewModel.MusicTrackTiles).HasHotkey);
        Assert.True(storage.SaveCount > 0);
    }

    [Fact]
    public void RestoreOneDefaultHotkeyLeavesOtherCustomSystemBindingsUnchanged()
    {
        var customPlayPause = new Hotkey(0, "A", HotkeyModifier.None);
        var customStopEffects = new Hotkey(1, "B", HotkeyModifier.None);
        var storage = StorageWithMusicPlaylists(
            [new Playlist("Music")],
            new HotkeyConfiguration(
            [
                new HotkeyBinding(HotkeyAction.PlayPause, customPlayPause),
                new HotkeyBinding(HotkeyAction.StopEffects, customStopEffects)
            ]));
        using var viewModel = CreateViewModel(storage);

        viewModel.RestoreDefaultSystemHotkeyCommand.Execute(HotkeyAction.PlayPause);

        Assert.Equal("Space", Assert.Single(viewModel.SystemHotkeyRows, row => row.Action.Equals(HotkeyAction.PlayPause)).HotkeyText);
        Assert.Equal("B", Assert.Single(viewModel.SystemHotkeyRows, row => row.Action.Equals(HotkeyAction.StopEffects)).HotkeyText);
        Assert.True(storage.SaveCount > 0);
    }

    [Fact]
    public void AssignedTrackHotkeyRowsExposeTrackBindingsForSettings()
    {
        var music = new Track("Music", "C:\\audio\\music.mp3", TrackRole.Music);
        var effect = new Track("Effect", "C:\\audio\\effect.wav", TrackRole.Effect);
        var playlist = new Playlist("Music", [music]);
        var effects = new EffectPlaylist("SFX", [effect]);
        using var viewModel = CreateViewModel(StorageWith(playlist, effects));

        Assert.Empty(viewModel.AssignedTrackHotkeyRows);
        Assert.Equal("No assigned track hotkeys", viewModel.AssignedTrackHotkeySummary);

        viewModel.BindSelectedMusicCommand.Execute(null);
        Assert.True(viewModel.HandleHotkey(new Hotkey(0, "A", HotkeyModifier.None)));
        viewModel.BindSelectedEffectCommand.Execute(null);
        Assert.True(viewModel.HandleHotkey(new Hotkey(1, "B", HotkeyModifier.Shift)));

        var rows = viewModel.AssignedTrackHotkeyRows;

        Assert.Equal("2 assigned track hotkeys", viewModel.AssignedTrackHotkeySummary);
        Assert.Equal(["Effect (SFX)", "Music (Music)"], rows.Select(row => row.Name).ToArray());
        Assert.Equal("Shift+B", rows[0].HotkeyText);
        Assert.Equal("A", rows[1].HotkeyText);

        viewModel.ClearSystemHotkeyCommand.Execute(rows[0].Action);

        Assert.Equal("1 assigned track hotkey", viewModel.AssignedTrackHotkeySummary);
        Assert.Single(viewModel.AssignedTrackHotkeyRows);
        Assert.Equal("Music (Music)", viewModel.AssignedTrackHotkeyRows[0].Name);
    }

    [Fact]
    public void HotkeyCaptureConflictShowsDialogAndCancelKeepsExistingBinding()
    {
        var track = new Track("Music", "C:\\audio\\music.mp3", TrackRole.Music);
        var playlist = new Playlist("Music", [track]);
        var storage = StorageWith(playlist, new EffectPlaylist("SFX"));
        using var viewModel = CreateViewModel(storage);
        var saveCountBeforeConflict = storage.SaveCount;

        viewModel.BindSelectedMusicCommand.Execute(null);

        Assert.True(viewModel.HandleHotkey(new Hotkey(HotkeyConfiguration.SpaceKeyCode, "Space", HotkeyModifier.None)));

        Assert.False(viewModel.IsCapturingHotkey);
        Assert.True(viewModel.IsHotkeyConflictVisible);
        Assert.Equal("Hotkey already assigned", viewModel.HotkeyConflictTitle);
        Assert.Contains("Space", viewModel.HotkeyConflictMessage);
        Assert.Contains("Play / pause music", viewModel.HotkeyConflictMessage);
        Assert.Contains("Music", viewModel.HotkeyConflictMessage);
        Assert.Equal(saveCountBeforeConflict, storage.SaveCount);
        Assert.False(Assert.Single(viewModel.MusicTrackTiles).HasHotkey);
        Assert.Equal("Space", Assert.Single(viewModel.SystemHotkeyRows, row => row.Action.Equals(HotkeyAction.PlayPause)).HotkeyText);

        viewModel.CancelHotkeyConflictCommand.Execute(null);

        Assert.False(viewModel.IsHotkeyConflictVisible);
        Assert.Equal("", viewModel.HotkeyConflictMessage);
        Assert.Equal(saveCountBeforeConflict, storage.SaveCount);
        Assert.False(Assert.Single(viewModel.MusicTrackTiles).HasHotkey);
        Assert.Equal("Space", Assert.Single(viewModel.SystemHotkeyRows, row => row.Action.Equals(HotkeyAction.PlayPause)).HotkeyText);
    }

    [Fact]
    public void ConfirmHotkeyCaptureConflictReplacesExistingBinding()
    {
        var track = new Track("Music", "C:\\audio\\music.mp3", TrackRole.Music);
        var playlist = new Playlist("Music", [track]);
        var storage = StorageWith(playlist, new EffectPlaylist("SFX"));
        using var viewModel = CreateViewModel(storage);

        viewModel.BindSelectedMusicCommand.Execute(null);
        Assert.True(viewModel.HandleHotkey(new Hotkey(HotkeyConfiguration.SpaceKeyCode, "Space", HotkeyModifier.None)));

        viewModel.ConfirmHotkeyConflictCommand.Execute(null);

        Assert.False(viewModel.IsHotkeyConflictVisible);
        Assert.Equal("Space", Assert.Single(viewModel.MusicTrackTiles).HotkeyText);
        Assert.Equal("Unassigned", Assert.Single(viewModel.SystemHotkeyRows, row => row.Action.Equals(HotkeyAction.PlayPause)).HotkeyText);
        Assert.True(storage.SaveCount > 0);
    }

    [Fact]
    public void RequestDeleteTrackClosesOpenHotkeyConflictWithoutReplacingBinding()
    {
        var track = new Track("Music", "C:\\audio\\music.mp3", TrackRole.Music);
        var playlist = new Playlist("Music", [track]);
        var storage = StorageWith(playlist, new EffectPlaylist("SFX"));
        using var viewModel = CreateViewModel(storage);
        var saveCountBeforeDeleteRequest = storage.SaveCount;

        viewModel.BindSelectedMusicCommand.Execute(null);
        Assert.True(viewModel.HandleHotkey(new Hotkey(HotkeyConfiguration.SpaceKeyCode, "Space", HotkeyModifier.None)));

        viewModel.RequestDeleteMusicTrackItemCommand.Execute(track);

        Assert.False(viewModel.IsHotkeyConflictVisible);
        Assert.True(viewModel.IsDeleteConfirmationVisible);
        Assert.Equal(saveCountBeforeDeleteRequest, storage.SaveCount);
        Assert.False(Assert.Single(viewModel.MusicTrackTiles).HasHotkey);
        Assert.Equal("Space", Assert.Single(viewModel.SystemHotkeyRows, row => row.Action.Equals(HotkeyAction.PlayPause)).HotkeyText);
    }

    [Fact]
    public void ImportConflictClosesOpenHotkeyConflictWithoutReplacingBinding()
    {
        var tempDirectory = Directory.CreateTempSubdirectory("dungeon-soundboard-hotkey-conflict-");
        try
        {
            var duplicatePath = Path.Combine(tempDirectory.FullName, "Boss Theme.mp3");
            File.WriteAllText(duplicatePath, "test");
            var track = new Track("Boss Theme", duplicatePath, TrackRole.Music);
            var playlist = new Playlist("Music", [track]);
            var storage = StorageWith(playlist, new EffectPlaylist("SFX"));
            using var viewModel = CreateViewModel(storage);

            viewModel.BindSelectedMusicCommand.Execute(null);
            Assert.True(viewModel.HandleHotkey(new Hotkey(HotkeyConfiguration.SpaceKeyCode, "Space", HotkeyModifier.None)));

            viewModel.ImportDroppedMusic([duplicatePath]);

            Assert.False(viewModel.IsHotkeyConflictVisible);
            Assert.True(viewModel.IsImportConflictVisible);
            Assert.False(Assert.Single(viewModel.MusicTrackTiles).HasHotkey);
            Assert.Equal("Space", Assert.Single(viewModel.SystemHotkeyRows, row => row.Action.Equals(HotkeyAction.PlayPause)).HotkeyText);
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
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
    public void SelectedTrackDisplayTitlesUsePlaceholdersWhenNothingIsSelected()
    {
        var music = new Track("Music", "C:\\audio\\music.mp3", TrackRole.Music);
        var effect = new Track("Effect", "C:\\audio\\effect.wav", TrackRole.Effect);
        using var viewModel = CreateViewModel(
            StorageWith(new Playlist("Music", [music]), new EffectPlaylist("SFX", [effect])));

        Assert.Equal("Music", viewModel.SelectedMusicTrackDisplayTitle);
        Assert.Equal("Effect", viewModel.SelectedEffectTrackDisplayTitle);

        viewModel.DeleteMusicTrackItemCommand.Execute(music);
        viewModel.DeleteEffectTrackItemCommand.Execute(effect);

        Assert.Equal("No music track selected", viewModel.SelectedMusicTrackDisplayTitle);
        Assert.Equal("No SFX track selected", viewModel.SelectedEffectTrackDisplayTitle);
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
    public void MoveMusicTrackToTargetReordersSelectedPlaylistWithoutChangingSelection()
    {
        var first = new Track("First", "C:\\audio\\first.mp3", TrackRole.Music);
        var second = new Track("Second", "C:\\audio\\second.mp3", TrackRole.Music);
        var third = new Track("Third", "C:\\audio\\third.mp3", TrackRole.Music);
        var playlist = new Playlist("Music", [first, second, third]);
        var storage = StorageWith(playlist, new EffectPlaylist("SFX"));
        using var viewModel = CreateViewModel(storage);

        viewModel.MoveMusicTrackToTarget(first, third);

        Assert.Equal(playlist.Id, viewModel.SelectedMusicPlaylist?.Id);
        Assert.Equal(first.Id, viewModel.SelectedMusicTrack?.Id);
        Assert.Collection(
            viewModel.MusicTracks,
            track => Assert.Equal(second.Id, track.Id),
            track => Assert.Equal(first.Id, track.Id),
            track => Assert.Equal(third.Id, track.Id));
        Assert.Equal(
            [second.Id, first.Id, third.Id],
            viewModel.MusicTrackTiles.Select(tile => tile.Track.Id).ToArray());
        Assert.True(storage.SaveCount > 0);
    }

    [Fact]
    public void MoveEffectTrackToTargetReordersSelectedPlaylistWithoutChangingSelection()
    {
        var first = new Track("First", "C:\\audio\\first.wav", TrackRole.Effect);
        var second = new Track("Second", "C:\\audio\\second.wav", TrackRole.Effect);
        var third = new Track("Third", "C:\\audio\\third.wav", TrackRole.Effect);
        var effects = new EffectPlaylist("SFX", [first, second, third]);
        var storage = StorageWith(new Playlist("Music"), effects);
        using var viewModel = CreateViewModel(storage);

        viewModel.MoveEffectTrackToTarget(third, first);

        Assert.Equal(effects.Id, viewModel.SelectedEffectPlaylist?.Id);
        Assert.Equal(first.Id, viewModel.SelectedEffectTrack?.Id);
        Assert.Collection(
            viewModel.EffectTracks,
            track => Assert.Equal(third.Id, track.Id),
            track => Assert.Equal(first.Id, track.Id),
            track => Assert.Equal(second.Id, track.Id));
        Assert.Equal(
            [third.Id, first.Id, second.Id],
            viewModel.EffectTrackTiles.Select(tile => tile.Track.Id).ToArray());
        Assert.True(storage.SaveCount > 0);
    }

    [Fact]
    public void MoveTrackToTargetNoOpsForInvalidSourceOrTarget()
    {
        var first = new Track("First", "C:\\audio\\first.mp3", TrackRole.Music);
        var second = new Track("Second", "C:\\audio\\second.mp3", TrackRole.Music);
        var playlist = new Playlist("Music", [first, second]);
        var storage = StorageWith(playlist, new EffectPlaylist("SFX"));
        using var viewModel = CreateViewModel(storage);
        var saveCountBeforeMove = storage.SaveCount;

        viewModel.MoveMusicTrackToTarget(first, first);
        viewModel.MoveMusicTrackToTarget(new Track("Missing", "C:\\audio\\missing.mp3", TrackRole.Music), second);
        viewModel.MoveMusicTrackToTarget(first, new Track("Missing Target", "C:\\audio\\missing-target.mp3", TrackRole.Music));
        viewModel.MoveMusicTrackToTarget(null, second);
        viewModel.MoveMusicTrackToTarget(first, null);

        Assert.Equal([first.Id, second.Id], viewModel.MusicTracks.Select(track => track.Id).ToArray());
        Assert.Equal(saveCountBeforeMove, storage.SaveCount);
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
        Assert.Equal("Nothing is playing", viewModel.CurrentTrackTitle);
        Assert.False(Assert.Single(viewModel.MusicTrackTiles).IsCurrent);

        viewModel.PlayMusicTrackCommand.Execute(track);

        Assert.Equal("Pause", viewModel.PlayPauseButtonText);
        Assert.Equal("M7,5H10V19H7V5M14,5H17V19H14V5Z", viewModel.PlayPauseIconData);
        Assert.Equal("Track", viewModel.CurrentTrackTitle);
        Assert.True(Assert.Single(viewModel.MusicTrackTiles).IsCurrent);
        Assert.Equal(1, audio.PlayMusicCount);

        viewModel.PlayPauseCommand.Execute(null);

        Assert.Equal("Resume", viewModel.PlayPauseButtonText);
        Assert.Equal("M8,5V19L19,12L8,5Z", viewModel.PlayPauseIconData);
        Assert.Equal("Track", viewModel.CurrentTrackTitle);
        Assert.True(Assert.Single(viewModel.MusicTrackTiles).IsCurrent);
        Assert.Equal(1, audio.PauseCount);

        viewModel.PlayPauseCommand.Execute(null);

        Assert.Equal("Pause", viewModel.PlayPauseButtonText);
        Assert.Equal("M7,5H10V19H7V5M14,5H17V19H14V5Z", viewModel.PlayPauseIconData);
        Assert.Equal(1, audio.ResumeCount);
        Assert.Equal(1, audio.PlayMusicCount);

        viewModel.StopAllCommand.Execute(null);

        Assert.Equal("Play", viewModel.PlayPauseButtonText);
        Assert.Equal("M8,5V19L19,12L8,5Z", viewModel.PlayPauseIconData);
        Assert.Equal("Nothing is playing", viewModel.CurrentTrackTitle);
        Assert.False(Assert.Single(viewModel.MusicTrackTiles).IsCurrent);
    }

    [Fact]
    public void PlayPauseUsesFadeOutWhenPreferenceIsEnabled()
    {
        var track = new Track("Track", "C:\\audio\\track.mp3", TrackRole.Music);
        var playlist = new Playlist("Music", [track]);
        var audio = new FakeAudioService();
        using var viewModel = CreateViewModel(StorageWith(playlist, new EffectPlaylist("SFX")), audio);

        viewModel.MusicFadeOutOnPauseEnabled = true;
        viewModel.PlayMusicTrackCommand.Execute(track);
        viewModel.PlayPauseCommand.Execute(null);

        Assert.Equal("Resume", viewModel.PlayPauseButtonText);
        Assert.Equal(0, audio.PauseCount);
        Assert.Equal(1, audio.FadeOutAndPauseMusicCount);
        Assert.Equal(TimeSpan.FromMilliseconds(1200), audio.LastFadeDuration);
        Assert.Equal(viewModel.MusicVolume, audio.LastFadeRestoreVolume);

        viewModel.PlayPauseCommand.Execute(null);

        Assert.Equal(1, audio.ResumeCount);
        Assert.Equal(viewModel.MusicVolume, audio.LastSetMusicVolume);
    }

    [Fact]
    public void MusicFinishedEventAdvancesToNextTrack()
    {
        var first = new Track("First", "C:\\audio\\first.mp3", TrackRole.Music);
        var second = new Track("Second", "C:\\audio\\second.mp3", TrackRole.Music);
        var playlist = new Playlist("Music", [first, second]);
        var audio = new FakeAudioService();
        using var viewModel = CreateViewModel(StorageWith(playlist, new EffectPlaylist("SFX")), audio);

        viewModel.PlayMusicTrackCommand.Execute(first);
        audio.RaiseMusicFinished();

        Assert.Equal(second.Id, viewModel.CurrentTrack?.Id);
        Assert.Equal(second.Id, audio.LastMusicTrack?.Id);
        Assert.True(viewModel.IsPlaying);
    }

    [Fact]
    public void MusicFinishedEventRepeatsCurrentTrackWhenRepeatOneIsEnabled()
    {
        var first = new Track("First", "C:\\audio\\first.mp3", TrackRole.Music);
        var second = new Track("Second", "C:\\audio\\second.mp3", TrackRole.Music);
        var playlist = new Playlist("Music", [first, second]);
        var audio = new FakeAudioService();
        using var viewModel = CreateViewModel(StorageWith(playlist, new EffectPlaylist("SFX")), audio);

        viewModel.RepeatMode = RepeatMode.One;
        viewModel.PlayMusicTrackCommand.Execute(first);
        audio.RaiseMusicFinished();

        Assert.Equal(first.Id, viewModel.CurrentTrack?.Id);
        Assert.Equal(first.Id, audio.LastMusicTrack?.Id);
        Assert.True(viewModel.IsPlaying);
    }

    [Fact]
    public void MusicFinishedEventStopsAtPlaylistEndWhenRepeatIsOff()
    {
        var first = new Track("First", "C:\\audio\\first.mp3", TrackRole.Music);
        var last = new Track("Last", "C:\\audio\\last.mp3", TrackRole.Music);
        var playlist = new Playlist("Music", [first, last]);
        var audio = new FakeAudioService();
        using var viewModel = CreateViewModel(StorageWith(playlist, new EffectPlaylist("SFX")), audio);

        viewModel.PlayMusicTrackCommand.Execute(last);
        audio.RaiseMusicFinished();

        Assert.Equal(last.Id, viewModel.CurrentTrack?.Id);
        Assert.Equal(last.Id, audio.LastMusicTrack?.Id);
        Assert.False(viewModel.IsPlaying);
    }

    [Fact]
    public void DisposedViewModelIgnoresLateAudioEvents()
    {
        var first = new Track("First", "C:\\audio\\first.mp3", TrackRole.Music);
        var second = new Track("Second", "C:\\audio\\second.mp3", TrackRole.Music);
        var playlist = new Playlist("Music", [first, second]);
        var audio = new FakeAudioService();
        var viewModel = CreateViewModel(StorageWith(playlist, new EffectPlaylist("SFX")), audio);

        viewModel.PlayMusicTrackCommand.Execute(first);
        var musicVolumeBeforeDispose = audio.LastSetMusicVolume;
        viewModel.Dispose();
        audio.RaiseMusicFinished();
        audio.RaiseEffectPlaybackCountChanged();

        Assert.True(audio.IsDisposed);
        Assert.Equal(first.Id, audio.LastMusicTrack?.Id);
        Assert.Equal(1, audio.PlayMusicCount);
        Assert.Equal(musicVolumeBeforeDispose, audio.LastSetMusicVolume);
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
    public void NextCommandWrapsAtPlaylistEndEvenWhenRepeatOneIsEnabled()
    {
        var first = new Track("First", "C:\\audio\\first.mp3", TrackRole.Music);
        var last = new Track("Last", "C:\\audio\\last.mp3", TrackRole.Music);
        var playlist = new Playlist("Music", [first, last]);
        var audio = new FakeAudioService();
        using var viewModel = CreateViewModel(StorageWith(playlist, new EffectPlaylist("SFX")), audio);

        viewModel.RepeatMode = RepeatMode.One;
        viewModel.PlayMusicTrackCommand.Execute(last);
        viewModel.NextTrackCommand.Execute(null);

        Assert.Equal(first.Id, viewModel.CurrentTrack?.Id);
        Assert.Equal(first.Id, audio.LastMusicTrack?.Id);
    }

    [Fact]
    public void ShuffleNextNeverSelectsTheCurrentTrackWhenAnotherTrackExists()
    {
        var first = new Track("First", "C:\\audio\\first.mp3", TrackRole.Music);
        var second = new Track("Second", "C:\\audio\\second.mp3", TrackRole.Music);
        var playlist = new Playlist("Music", [first, second]);
        var audio = new FakeAudioService();
        using var viewModel = CreateViewModel(
            StorageWith(playlist, new EffectPlaylist("SFX")),
            audio,
            new FixedRandom(0));

        viewModel.PlayMusicTrackCommand.Execute(first);
        viewModel.ShuffleEnabled = true;
        viewModel.NextTrackCommand.Execute(null);

        Assert.Equal(second.Id, viewModel.CurrentTrack?.Id);
    }

    [Fact]
    public void ShufflePreviousReturnsToThePreviouslyPlayedTrack()
    {
        var first = new Track("First", "C:\\audio\\first.mp3", TrackRole.Music);
        var second = new Track("Second", "C:\\audio\\second.mp3", TrackRole.Music);
        var third = new Track("Third", "C:\\audio\\third.mp3", TrackRole.Music);
        var playlist = new Playlist("Music", [first, second, third]);
        var audio = new FakeAudioService();
        using var viewModel = CreateViewModel(
            StorageWith(playlist, new EffectPlaylist("SFX")),
            audio,
            new FixedRandom(0));

        viewModel.PlayMusicTrackCommand.Execute(third);
        viewModel.ShuffleEnabled = true;
        viewModel.NextTrackCommand.Execute(null);
        Assert.NotEqual(third.Id, viewModel.CurrentTrack?.Id);

        viewModel.PreviousTrackCommand.Execute(null);

        Assert.Equal(third.Id, viewModel.CurrentTrack?.Id);
    }

    [Theory]
    [InlineData(0, 0.8)]
    [InlineData(1, 0)]
    public void DuckingAmountRepresentsThePercentageOfMusicRemoved(double duckingAmount, double expectedOutput)
    {
        var music = new Track("Music", "C:\\audio\\music.mp3", TrackRole.Music);
        var effect = new Track("Effect", "C:\\audio\\effect.wav", TrackRole.Effect);
        var audio = new FakeAudioService();
        using var viewModel = CreateViewModel(
            StorageWith(new Playlist("Music", [music]), new EffectPlaylist("SFX", [effect])),
            audio);

        viewModel.MusicVolume = 0.8;
        viewModel.DuckingAmount = duckingAmount;
        viewModel.PlayMusicTrackCommand.Execute(music);
        viewModel.PlayEffectCommand.Execute(effect);

        Assert.NotNull(audio.LastSetMusicVolume);
        Assert.Equal(expectedOutput, audio.LastSetMusicVolume.Value, precision: 6);
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
        Assert.False(viewModel.HasStatusMessage);

        viewModel.PlayMusicTrackCommand.Execute(track);

        Assert.True(viewModel.IsStatusError);
        Assert.True(viewModel.HasStatusMessage);
        Assert.StartsWith("Failed to play file: Track.", viewModel.StatusMessage);
        Assert.Contains(nameof(MainWindowViewModel.HasStatusMessage), changedProperties);
        Assert.Contains(nameof(MainWindowViewModel.IsStatusError), changedProperties);
        Assert.Contains(nameof(MainWindowViewModel.StatusMessageBrush), changedProperties);

        audio.ThrowOnPlayMusic = false;
        changedProperties.Clear();
        viewModel.PlayMusicTrackCommand.Execute(track);

        Assert.False(viewModel.IsStatusError);
        Assert.False(viewModel.HasStatusMessage);
        Assert.Empty(viewModel.StatusMessage);
        Assert.Contains(nameof(MainWindowViewModel.HasStatusMessage), changedProperties);
        Assert.Contains(nameof(MainWindowViewModel.IsStatusError), changedProperties);
        Assert.Contains(nameof(MainWindowViewModel.StatusMessageBrush), changedProperties);
    }

    [Fact]
    public void PlaybackFailureUsesSelectedLanguage()
    {
        var track = new Track("Track", "C:\\audio\\track.mp3", TrackRole.Music);
        var playlist = new Playlist("Music", [track]);
        var audio = new FakeAudioService { ThrowOnPlayMusic = true };
        using var viewModel = CreateViewModel(StorageWith(playlist, new EffectPlaylist("SFX")), audio);
        viewModel.SelectedLanguage = Assert.Single(viewModel.LanguageOptions, option => option.Language == AppLanguage.Russian);

        viewModel.PlayMusicTrackCommand.Execute(track);

        Assert.True(viewModel.IsStatusError);
        Assert.StartsWith("Не удалось воспроизвести файл «Track».", viewModel.StatusMessage);
    }

    [Fact]
    public void MissingMusicFilePlaybackUsesSelectedLanguage()
    {
        var track = new Track("Track", "C:\\audio\\missing.mp3", TrackRole.Music);
        var playlist = new Playlist("Music", [track]);
        var audio = new FakeAudioService
        {
            PlayMusicException = new FileNotFoundException("Audio file was not found.", track.Path)
        };
        using var viewModel = CreateViewModel(StorageWith(playlist, new EffectPlaylist("SFX")), audio);
        viewModel.SelectedLanguage = Assert.Single(viewModel.LanguageOptions, option => option.Language == AppLanguage.Russian);

        viewModel.PlayMusicTrackCommand.Execute(track);

        Assert.True(viewModel.IsStatusError);
        Assert.Equal("Аудиофайл отсутствует: C:\\audio\\missing.mp3", viewModel.StatusMessage);
    }

    [Fact]
    public void UnsupportedCodecPlaybackShowsCodecGuidance()
    {
        var track = new Track("Broken", "C:\\audio\\broken.m4a", TrackRole.Music);
        var playlist = new Playlist("Music", [track]);
        var audio = new FakeAudioService
        {
            PlayMusicException = new AudioPlaybackException(
                AudioPlaybackFailureKind.UnsupportedCodec,
                "decoder failed")
        };
        using var viewModel = CreateViewModel(StorageWith(playlist, new EffectPlaylist("SFX")), audio);

        viewModel.PlayMusicTrackCommand.Execute(track);

        Assert.True(viewModel.IsStatusError);
        Assert.Equal("Failed to play file: Broken. File found, but Windows/NAudio could not decode this audio format.", viewModel.StatusMessage);
    }

    [Fact]
    public void OutputDevicePlaybackShowsDeviceGuidance()
    {
        var effect = new Track("Door", "C:\\audio\\door.wav", TrackRole.Effect);
        var audio = new FakeAudioService
        {
            PlayEffectException = new AudioPlaybackException(
                AudioPlaybackFailureKind.OutputDevice,
                "device is unavailable")
        };
        using var viewModel = CreateViewModel(StorageWith(new Playlist("Music"), new EffectPlaylist("SFX", [effect])), audio);

        viewModel.PlayEffectCommand.Execute(effect);

        Assert.True(viewModel.IsStatusError);
        Assert.Equal("Failed to play effect: Door. Could not open audio output device.", viewModel.StatusMessage);
    }

    [Fact]
    public void MissingEffectFilePlaybackUsesSelectedLanguage()
    {
        var effect = new Track("Door", "C:\\audio\\missing.wav", TrackRole.Effect);
        var audio = new FakeAudioService
        {
            PlayEffectException = new FileNotFoundException("Effect file was not found.", effect.Path)
        };
        using var viewModel = CreateViewModel(StorageWith(new Playlist("Music"), new EffectPlaylist("SFX", [effect])), audio);
        viewModel.SelectedLanguage = Assert.Single(viewModel.LanguageOptions, option => option.Language == AppLanguage.Russian);

        viewModel.PlayEffectCommand.Execute(effect);

        Assert.True(viewModel.IsStatusError);
        Assert.Equal("Аудиофайл отсутствует: C:\\audio\\missing.wav", viewModel.StatusMessage);
    }

    [Fact]
    public void MissingMusicFilePlaybackKeepsCurrentMusicState()
    {
        var playing = new Track("Battle Loop", "C:\\audio\\battle.mp3", TrackRole.Music);
        var missing = new Track("Missing Track", "C:\\audio\\missing.mp3", TrackRole.Music);
        var audio = new FakeAudioService();
        using var viewModel = CreateViewModel(StorageWith(new Playlist("Music", [playing, missing]), new EffectPlaylist("SFX")), audio);

        viewModel.PlayMusicTrackCommand.Execute(playing);
        audio.PlayMusicException = new FileNotFoundException("Audio file was not found.", missing.Path);

        viewModel.PlayMusicTrackCommand.Execute(missing);

        Assert.True(viewModel.IsStatusError);
        Assert.Equal("Audio file is missing: C:\\audio\\missing.mp3", viewModel.StatusMessage);
        Assert.True(viewModel.IsPlaying);
        Assert.True(audio.IsMusicPlaying);
        Assert.Equal(playing.Id, viewModel.CurrentTrack?.Id);
        Assert.Equal(playing.Id, audio.LastMusicTrack?.Id);
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
    public void EffectPlaybackFailureShowsErrorWithoutStoppingMusic()
    {
        var music = new Track("Music", "C:\\audio\\music.mp3", TrackRole.Music);
        var effect = new Track("Broken SFX", "C:\\audio\\broken.wav", TrackRole.Effect);
        var audio = new FakeAudioService { ThrowOnPlayEffect = true };
        using var viewModel = CreateViewModel(
            StorageWith(new Playlist("Music", [music]), new EffectPlaylist("SFX", [effect])),
            audio);

        viewModel.PlayMusicTrackCommand.Execute(music);
        viewModel.PlayEffectCommand.Execute(effect);

        Assert.True(viewModel.IsStatusError);
        Assert.StartsWith("Failed to play effect: Broken SFX.", viewModel.StatusMessage);
        Assert.Equal(music.Id, viewModel.CurrentTrack?.Id);
        Assert.True(audio.IsMusicPlaying);
        Assert.Null(audio.LastEffectTrack);
        Assert.Equal(0, audio.ActiveEffectCount);
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
    public void ImportDroppedDuplicateMusicShowsConflictDialogAndDismissesIt()
    {
        var tempDirectory = Directory.CreateTempSubdirectory("dungeon-soundboard-import-");
        try
        {
            var duplicatePath = Path.Combine(tempDirectory.FullName, "Boss Theme.mp3");
            File.WriteAllText(duplicatePath, "test");
            var existing = new Track("Boss Theme", duplicatePath, TrackRole.Music);
            var playlist = new Playlist("Music", [existing]);
            using var viewModel = CreateViewModel(StorageWith(playlist, new EffectPlaylist("SFX")));

            viewModel.ImportDroppedMusic([duplicatePath]);

            Assert.True(viewModel.IsImportConflictVisible);
            Assert.Equal("Import duplicates skipped", viewModel.ImportConflictTitle);
            Assert.Contains("Music", viewModel.ImportConflictMessage);
            Assert.Contains("skipped 1 duplicate", viewModel.ImportConflictMessage);
            Assert.Contains("Boss Theme", viewModel.ImportConflictDuplicateText);
            Assert.Equal("Added 0; duplicates: 1; unsupported: 0; unavailable: 0.", viewModel.LastImportMessage);

            viewModel.DismissImportConflictCommand.Execute(null);

            Assert.False(viewModel.IsImportConflictVisible);
            Assert.Equal("", viewModel.ImportConflictMessage);
            Assert.Equal("Added 0; duplicates: 1; unsupported: 0; unavailable: 0.", viewModel.LastImportMessage);
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public void EmptyImportSelectionDoesNotCreateAStatusMessage()
    {
        using var viewModel = CreateViewModel(StorageWith(new Playlist("Music"), new EffectPlaylist("SFX")));

        viewModel.ImportDroppedMusic([]);

        Assert.False(viewModel.HasStatusMessage);
        Assert.Empty(viewModel.LastImportMessage);
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
    public void PlayEffectPlaylistItemSelectsPlaylistAndStartsRandomEffect()
    {
        var firstPlaylist = new EffectPlaylist("First", [new Track("First SFX", "C:\\audio\\first.wav", TrackRole.Effect)]);
        var firstTarget = new Track("Sword", "C:\\audio\\sword.wav", TrackRole.Effect);
        var secondTarget = new Track("Shield", "C:\\audio\\shield.wav", TrackRole.Effect);
        var targetPlaylist = new EffectPlaylist("Combat", [firstTarget, secondTarget]);
        var audio = new FakeAudioService();
        var storage = StorageWithEffectPlaylists([firstPlaylist, targetPlaylist]);
        using var viewModel = CreateViewModel(storage, audio, new FixedRandom(1));

        viewModel.PlayEffectPlaylistItemCommand.Execute(targetPlaylist);

        Assert.Equal(targetPlaylist.Id, viewModel.SelectedEffectPlaylist?.Id);
        Assert.Equal(secondTarget.Id, viewModel.SelectedEffectTrack?.Id);
        Assert.Equal(secondTarget.Id, audio.LastEffectTrack?.Id);
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
    public void DeletingHotkeyPlaybackPlaylistFallsBackToSelectedPlaylist()
    {
        var selectedTrack = new Track("Selected Track", "C:\\audio\\selected.mp3", TrackRole.Music);
        var selectedPlaylist = new Playlist("Selected", [selectedTrack]);
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

        Assert.True(viewModel.HandleHotkey(hotkey));
        Assert.Equal("Boss", viewModel.CurrentPlaybackPlaylistName);

        viewModel.DeleteMusicPlaylistItemCommand.Execute(hotkeyPlaylist);

        Assert.Single(viewModel.MusicPlaylists);
        Assert.Equal(selectedPlaylist.Id, viewModel.SelectedMusicPlaylist?.Id);
        Assert.Equal("Selected", viewModel.CurrentPlaybackPlaylistName);
        Assert.False(viewModel.IsPlaying);
        Assert.False(audio.IsMusicPlaying);

        viewModel.PlayPauseCommand.Execute(null);

        Assert.Equal(selectedTrack.Id, viewModel.CurrentTrack?.Id);
        Assert.Equal(selectedTrack.Id, audio.LastMusicTrack?.Id);
        Assert.DoesNotContain(viewModel.MusicPlaylists, playlist => playlist.Id == hotkeyPlaylist.Id);
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
    public void RequestRenamePlaylistItemShowsDialogAndCancelKeepsPlaylist()
    {
        var selectedPlaylist = new Playlist("Selected", [new Track("Selected Track", "C:\\audio\\selected.mp3", TrackRole.Music)]);
        var renamedPlaylist = new Playlist("Rename Me", [new Track("Track", "C:\\audio\\track.mp3", TrackRole.Music)]);
        var storage = StorageWithMusicPlaylists([selectedPlaylist, renamedPlaylist]);
        using var viewModel = CreateViewModel(storage);
        var saveCountBeforeRename = storage.SaveCount;

        viewModel.RequestRenameMusicPlaylistItemCommand.Execute(renamedPlaylist);

        Assert.True(viewModel.IsPlaylistRenameVisible);
        Assert.Equal("Rename music playlist", viewModel.PlaylistRenameTitle);
        Assert.Equal("Rename Me", viewModel.PlaylistRenameName);

        viewModel.PlaylistRenameName = "Boss Music";
        viewModel.CancelPlaylistRenameCommand.Execute(null);

        Assert.False(viewModel.IsPlaylistRenameVisible);
        Assert.Equal("Rename Me", renamedPlaylist.Name);
        Assert.Equal(saveCountBeforeRename, storage.SaveCount);
    }

    [Fact]
    public void ConfirmRenameMusicPlaylistItemRenamesRequestedPlaylistWithoutChangingSelection()
    {
        var selectedPlaylist = new Playlist("Selected", [new Track("Selected Track", "C:\\audio\\selected.mp3", TrackRole.Music)]);
        var renamedPlaylist = new Playlist("Rename Me", [new Track("Track", "C:\\audio\\track.mp3", TrackRole.Music)]);
        var storage = StorageWithMusicPlaylists([selectedPlaylist, renamedPlaylist]);
        using var viewModel = CreateViewModel(storage);

        viewModel.RequestRenameMusicPlaylistItemCommand.Execute(renamedPlaylist);
        viewModel.PlaylistRenameName = "Boss Music";
        viewModel.ConfirmPlaylistRenameCommand.Execute(null);

        Assert.False(viewModel.IsPlaylistRenameVisible);
        Assert.Equal(selectedPlaylist.Id, viewModel.SelectedMusicPlaylist?.Id);
        Assert.Equal("Boss Music", renamedPlaylist.Name);
        Assert.True(storage.SaveCount > 0);
    }

    [Fact]
    public void ReturnConfirmsPlaylistRename()
    {
        var selectedPlaylist = new Playlist("Selected", [new Track("Selected Track", "C:\\audio\\selected.mp3", TrackRole.Music)]);
        var renamedPlaylist = new Playlist("Rename Me", [new Track("Track", "C:\\audio\\track.mp3", TrackRole.Music)]);
        var storage = StorageWithMusicPlaylists([selectedPlaylist, renamedPlaylist]);
        using var viewModel = CreateViewModel(storage);

        viewModel.RequestRenameMusicPlaylistItemCommand.Execute(renamedPlaylist);
        viewModel.PlaylistRenameName = "Boss Music";

        Assert.True(viewModel.HandleDialogHotkey(new Hotkey(HotkeyConfiguration.ReturnKeyCode, "Return", HotkeyModifier.None)));

        Assert.False(viewModel.IsPlaylistRenameVisible);
        Assert.Equal("Boss Music", renamedPlaylist.Name);
        Assert.True(storage.SaveCount > 0);
    }

    [Fact]
    public void ConfirmRenameEffectPlaylistItemRenamesRequestedPlaylistWithoutChangingSelection()
    {
        var selectedPlaylist = new EffectPlaylist("Selected");
        var renamedPlaylist = new EffectPlaylist("Rename Me");
        var storage = StorageWithEffectPlaylists([selectedPlaylist, renamedPlaylist]);
        using var viewModel = CreateViewModel(storage);

        viewModel.RequestRenameEffectPlaylistItemCommand.Execute(renamedPlaylist);
        viewModel.PlaylistRenameName = "Battle SFX";
        viewModel.ConfirmPlaylistRenameCommand.Execute(null);

        Assert.False(viewModel.IsPlaylistRenameVisible);
        Assert.Equal(selectedPlaylist.Id, viewModel.SelectedEffectPlaylist?.Id);
        Assert.Equal("Battle SFX", renamedPlaylist.Name);
        Assert.True(storage.SaveCount > 0);
    }

    [Fact]
    public void InlinePlaylistRenameUsesSelectedLanguageFallbackNames()
    {
        var musicPlaylist = new Playlist("Music");
        var effectPlaylist = new EffectPlaylist("SFX");
        using var viewModel = CreateViewModel(StorageWith(musicPlaylist, effectPlaylist));
        viewModel.SelectedLanguage = Assert.Single(viewModel.LanguageOptions, option => option.Language == AppLanguage.Russian);

        viewModel.SelectedMusicPlaylistName = "   ";
        viewModel.SelectedEffectPlaylistName = "   ";

        Assert.Equal("Основной плейлист", musicPlaylist.Name);
        Assert.Equal("SFX-мастер", effectPlaylist.Name);
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

    [Fact]
    public void MoveMusicPlaylistToTargetReordersWithoutChangingSelection()
    {
        var first = new Playlist("First");
        var second = new Playlist("Second");
        var third = new Playlist("Third");
        var storage = StorageWithMusicPlaylists([first, second, third]);
        using var viewModel = CreateViewModel(storage);

        viewModel.MoveMusicPlaylistToTarget(first, third);

        Assert.Collection(
            viewModel.MusicPlaylists,
            playlist => Assert.Equal(second.Id, playlist.Id),
            playlist => Assert.Equal(third.Id, playlist.Id),
            playlist => Assert.Equal(first.Id, playlist.Id));
        Assert.Equal(first.Id, viewModel.SelectedMusicPlaylist?.Id);
        Assert.True(storage.SaveCount > 0);
    }

    [Fact]
    public void MoveEffectPlaylistToTargetReordersWithoutChangingSelection()
    {
        var first = new EffectPlaylist("First");
        var second = new EffectPlaylist("Second");
        var third = new EffectPlaylist("Third");
        var storage = StorageWithEffectPlaylists([first, second, third]);
        using var viewModel = CreateViewModel(storage);

        viewModel.MoveEffectPlaylistToTarget(third, first);

        Assert.Collection(
            viewModel.EffectPlaylists,
            playlist => Assert.Equal(third.Id, playlist.Id),
            playlist => Assert.Equal(first.Id, playlist.Id),
            playlist => Assert.Equal(second.Id, playlist.Id));
        Assert.Equal(first.Id, viewModel.SelectedEffectPlaylist?.Id);
        Assert.True(storage.SaveCount > 0);
    }

    [Fact]
    public void MovePlaylistToTargetNoOpsForInvalidSourceOrTarget()
    {
        var first = new Playlist("First");
        var second = new Playlist("Second");
        var storage = StorageWithMusicPlaylists([first, second]);
        using var viewModel = CreateViewModel(storage);
        var saveCountBeforeMove = storage.SaveCount;

        viewModel.MoveMusicPlaylistToTarget(first, first);
        viewModel.MoveMusicPlaylistToTarget(new Playlist("Missing"), second);
        viewModel.MoveMusicPlaylistToTarget(first, new Playlist("Missing Target"));
        viewModel.MoveMusicPlaylistToTarget(null, second);
        viewModel.MoveMusicPlaylistToTarget(first, null);

        Assert.Equal([first.Id, second.Id], viewModel.MusicPlaylists.Select(playlist => playlist.Id).ToArray());
        Assert.Equal(saveCountBeforeMove, storage.SaveCount);
    }

    private static MainWindowViewModel CreateViewModel(FakeStorageService storage)
    {
        return CreateViewModel(storage, new FakeAudioService());
    }

    private static MainWindowViewModel CreateViewModel(FakeStorageService storage, FakeAudioService audio)
    {
        return new MainWindowViewModel(storage, new FileImportService(), audio);
    }

    private static MainWindowViewModel CreateViewModel(FakeStorageService storage, FakeAudioService audio, Random random)
    {
        return new MainWindowViewModel(storage, new FileImportService(), audio, random: random);
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
            state.Preferences.Language = AppLanguage.English;
            return state;
        }

        public void Save(AppState updatedState)
        {
            SaveCount++;
        }
    }

    private sealed class FakeAudioService : IAudioService
    {
        public event EventHandler? MusicFinished;
        public event EventHandler? EffectPlaybackCountChanged;

        public int ActiveEffectCount { get; private set; }
        public bool IsMusicPlaying { get; private set; }
        public Track? LastMusicTrack { get; private set; }
        public Track? LastEffectTrack { get; private set; }
        public int PlayMusicCount { get; private set; }
        public int PauseCount { get; private set; }
        public int FadeOutAndPauseMusicCount { get; private set; }
        public int ResumeCount { get; private set; }
        public bool ThrowOnPlayMusic { get; set; }
        public bool ThrowOnPlayEffect { get; set; }
        public Exception? PlayMusicException { get; set; }
        public Exception? PlayEffectException { get; set; }
        public TimeSpan MusicPosition { get; set; }
        public TimeSpan MusicDuration { get; set; }
        public TimeSpan? LastSeekPosition { get; private set; }
        public TimeSpan? LastFadeDuration { get; private set; }
        public double? LastFadeRestoreVolume { get; private set; }
        public double? LastSetMusicVolume { get; private set; }
        public double? LastSetEffectsVolume { get; private set; }
        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            IsDisposed = true;
        }

        public void RaiseMusicFinished()
        {
            IsMusicPlaying = false;
            MusicFinished?.Invoke(this, EventArgs.Empty);
        }

        public void RaiseEffectPlaybackCountChanged()
        {
            EffectPlaybackCountChanged?.Invoke(this, EventArgs.Empty);
        }

        public void PlayMusic(Track track, double volume)
        {
            if (PlayMusicException is not null)
            {
                throw PlayMusicException;
            }

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

        public void FadeOutAndPauseMusic(TimeSpan duration, double restoreVolume)
        {
            IsMusicPlaying = false;
            FadeOutAndPauseMusicCount++;
            LastFadeDuration = duration;
            LastFadeRestoreVolume = restoreVolume;
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
            if (PlayEffectException is not null)
            {
                throw PlayEffectException;
            }

            if (ThrowOnPlayEffect)
            {
                throw new InvalidOperationException("SFX playback failed");
            }

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
            LastSetEffectsVolume = masterVolume;
        }
    }

    private sealed class FixedRandom(int value) : Random
    {
        public override int Next(int maxValue) => Math.Min(value, maxValue - 1);
    }
}
