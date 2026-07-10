import XCTest
@testable import Dungeon_Soundboard

@MainActor
final class PlayerViewModelPlaybackTests: XCTestCase {
    func testFailedCandidatePlayDoesNotReplaceActiveMusic() throws {
        let firstTrack = makeTestTrack(title: "First")
        let secondTrack = makeTestTrack(title: "Second")
        let playlist = Playlist(name: "Music", tracks: [firstTrack, secondTrack])
        let harness = try PlayerTestHarness(
            musicPlaylists: [playlist],
            selectedMusicPlaylistID: playlist.id
        )
        let firstPlayer = MockAudioPlayer(duration: 90)
        let rejectedPlayer = MockAudioPlayer(duration: 120, playResults: [false])
        let firstLease = makeTestLease(for: firstTrack)
        let rejectedLease = makeTestLease(for: secondTrack)
        harness.factory.enqueue(firstPlayer)
        harness.factory.enqueue(rejectedPlayer)
        harness.resolver.enqueue(firstLease, for: firstTrack.id)
        harness.resolver.enqueue(rejectedLease, for: secondTrack.id)

        let viewModel = harness.makeViewModel()
        defer {
            viewModel.stopAll()
            harness.cleanup()
        }

        XCTAssertTrue(viewModel.playMusicTrack(playlistID: playlist.id, trackID: firstTrack.id))
        XCTAssertTrue(viewModel.isPlaying)
        XCTAssertEqual(viewModel.currentTrackID, firstTrack.id)

        XCTAssertTrue(viewModel.playMusicTrack(playlistID: playlist.id, trackID: secondTrack.id))

        XCTAssertTrue(viewModel.isPlaying)
        XCTAssertEqual(viewModel.currentTrackID, firstTrack.id)
        XCTAssertEqual(viewModel.playbackMusicPlaylistID, playlist.id)
        XCTAssertEqual(firstPlayer.stopCallCount, 0)
        XCTAssertNotNil(firstPlayer.eventDelegate)
        XCTAssertFalse(isTestLeaseClosed(firstLease))

        XCTAssertEqual(rejectedPlayer.prepareCallCount, 1)
        XCTAssertEqual(rejectedPlayer.playCallCount, 1)
        XCTAssertEqual(rejectedPlayer.stopCallCount, 1)
        XCTAssertNil(rejectedPlayer.eventDelegate)
        XCTAssertTrue(isTestLeaseClosed(rejectedLease))
        XCTAssertTrue(viewModel.errorMessage?.contains(secondTrack.title) == true)
    }

    func testStaleCallbacksAreIgnoredButCurrentDecodeErrorStopsMusic() throws {
        let firstTrack = makeTestTrack(title: "First")
        let secondTrack = makeTestTrack(title: "Second")
        let playlist = Playlist(name: "Music", tracks: [firstTrack, secondTrack])
        let harness = try PlayerTestHarness(
            musicPlaylists: [playlist],
            selectedMusicPlaylistID: playlist.id
        )
        let firstPlayer = MockAudioPlayer()
        let secondPlayer = MockAudioPlayer()
        let firstLease = makeTestLease(for: firstTrack)
        let secondLease = makeTestLease(for: secondTrack)
        harness.factory.enqueue(firstPlayer)
        harness.factory.enqueue(secondPlayer)
        harness.resolver.enqueue(firstLease, for: firstTrack.id)
        harness.resolver.enqueue(secondLease, for: secondTrack.id)

        let viewModel = harness.makeViewModel()
        defer {
            viewModel.stopAll()
            harness.cleanup()
        }

        XCTAssertTrue(viewModel.playMusicTrack(playlistID: playlist.id, trackID: firstTrack.id))
        XCTAssertTrue(viewModel.playMusicTrack(playlistID: playlist.id, trackID: secondTrack.id))
        XCTAssertEqual(firstPlayer.stopCallCount, 1)
        XCTAssertTrue(isTestLeaseClosed(firstLease))
        XCTAssertNil(firstPlayer.eventDelegate)

        viewModel.audioPlayerAdapterDidFinish(firstPlayer, successfully: false)
        viewModel.audioPlayerAdapter(firstPlayer, decodeError: PlayerTestFailure.decode)

        XCTAssertTrue(viewModel.isPlaying)
        XCTAssertEqual(viewModel.currentTrackID, secondTrack.id)
        XCTAssertEqual(secondPlayer.stopCallCount, 0)
        XCTAssertFalse(isTestLeaseClosed(secondLease))
        XCTAssertNil(viewModel.errorMessage)

        viewModel.audioPlayerAdapter(secondPlayer, decodeError: PlayerTestFailure.decode)

        XCTAssertFalse(viewModel.isPlaying)
        XCTAssertEqual(secondPlayer.stopCallCount, 1)
        XCTAssertNil(secondPlayer.eventDelegate)
        XCTAssertTrue(isTestLeaseClosed(secondLease))
        XCTAssertTrue(viewModel.errorMessage?.contains(secondTrack.title) == true)
        XCTAssertFalse(harness.telemetry.errorEvents.isEmpty)
    }

    func testEffectFinishAndDecodeCleanupDoNotStopMusic() throws {
        let musicTrack = makeTestTrack(title: "Music")
        let firstEffect = makeTestTrack(title: "Thunder", role: .effect)
        let secondEffect = makeTestTrack(title: "Door", role: .effect)
        let musicPlaylist = Playlist(name: "Music", tracks: [musicTrack])
        let effectPlaylist = EffectPlaylist(name: "SFX", effects: [firstEffect, secondEffect])
        let harness = try PlayerTestHarness(
            musicPlaylists: [musicPlaylist],
            effectPlaylists: [effectPlaylist],
            selectedMusicPlaylistID: musicPlaylist.id,
            selectedEffectPlaylistID: effectPlaylist.id
        )
        let musicPlayer = MockAudioPlayer()
        let firstEffectPlayer = MockAudioPlayer()
        let secondEffectPlayer = MockAudioPlayer()
        let musicLease = makeTestLease(for: musicTrack)
        let firstEffectLease = makeTestLease(for: firstEffect)
        let secondEffectLease = makeTestLease(for: secondEffect)
        harness.factory.enqueue(musicPlayer)
        harness.factory.enqueue(firstEffectPlayer)
        harness.factory.enqueue(secondEffectPlayer)
        harness.resolver.enqueue(musicLease, for: musicTrack.id)
        harness.resolver.enqueue(firstEffectLease, for: firstEffect.id)
        harness.resolver.enqueue(secondEffectLease, for: secondEffect.id)

        let viewModel = harness.makeViewModel()
        defer {
            viewModel.stopAll()
            harness.cleanup()
        }

        XCTAssertTrue(viewModel.playMusicTrack(playlistID: musicPlaylist.id, trackID: musicTrack.id))
        XCTAssertTrue(viewModel.playEffect(playlistID: effectPlaylist.id, trackID: firstEffect.id))
        XCTAssertTrue(viewModel.playEffect(playlistID: effectPlaylist.id, trackID: secondEffect.id))
        XCTAssertEqual(viewModel.activeEffectCount, 2)
        XCTAssertEqual(musicPlayer.volume, 0.44, accuracy: 0.001)

        firstEffectPlayer.emitFinish(successfully: true)

        XCTAssertEqual(viewModel.activeEffectCount, 1)
        XCTAssertEqual(firstEffectPlayer.stopCallCount, 1)
        XCTAssertTrue(isTestLeaseClosed(firstEffectLease))
        XCTAssertTrue(viewModel.isPlaying)
        XCTAssertEqual(musicPlayer.stopCallCount, 0)
        XCTAssertFalse(isTestLeaseClosed(musicLease))
        XCTAssertEqual(musicPlayer.volume, 0.44, accuracy: 0.001)

        secondEffectPlayer.emitDecodeError()

        XCTAssertEqual(viewModel.activeEffectCount, 0)
        XCTAssertEqual(secondEffectPlayer.stopCallCount, 1)
        XCTAssertTrue(isTestLeaseClosed(secondEffectLease))
        XCTAssertTrue(viewModel.isPlaying)
        XCTAssertEqual(musicPlayer.stopCallCount, 0)
        XCTAssertFalse(isTestLeaseClosed(musicLease))
        XCTAssertEqual(musicPlayer.volume, 0.8, accuracy: 0.001)
    }

    func testFailedEffectPlayCleansCandidateLeaseWithoutDuckingMusic() throws {
        let musicTrack = makeTestTrack(title: "Music")
        let effectTrack = makeTestTrack(title: "Broken Effect", role: .effect)
        let musicPlaylist = Playlist(name: "Music", tracks: [musicTrack])
        let effectPlaylist = EffectPlaylist(name: "SFX", effects: [effectTrack])
        let harness = try PlayerTestHarness(
            musicPlaylists: [musicPlaylist],
            effectPlaylists: [effectPlaylist],
            selectedMusicPlaylistID: musicPlaylist.id,
            selectedEffectPlaylistID: effectPlaylist.id
        )
        let musicPlayer = MockAudioPlayer()
        let rejectedEffectPlayer = MockAudioPlayer(playResults: [false])
        let musicLease = makeTestLease(for: musicTrack)
        let rejectedEffectLease = makeTestLease(for: effectTrack)
        harness.factory.enqueue(musicPlayer)
        harness.factory.enqueue(rejectedEffectPlayer)
        harness.resolver.enqueue(musicLease, for: musicTrack.id)
        harness.resolver.enqueue(rejectedEffectLease, for: effectTrack.id)

        let viewModel = harness.makeViewModel()
        defer {
            viewModel.stopAll()
            harness.cleanup()
        }

        XCTAssertTrue(viewModel.playMusicTrack(playlistID: musicPlaylist.id, trackID: musicTrack.id))
        XCTAssertEqual(musicPlayer.volume, 0.8, accuracy: 0.001)

        // Вызов обработан для существующей пары, даже если underlying player отказал.
        XCTAssertTrue(viewModel.playEffect(playlistID: effectPlaylist.id, trackID: effectTrack.id))

        XCTAssertEqual(viewModel.activeEffectCount, 0)
        XCTAssertEqual(rejectedEffectPlayer.prepareCallCount, 1)
        XCTAssertEqual(rejectedEffectPlayer.playCallCount, 1)
        XCTAssertEqual(rejectedEffectPlayer.stopCallCount, 1)
        XCTAssertNil(rejectedEffectPlayer.eventDelegate)
        XCTAssertTrue(isTestLeaseClosed(rejectedEffectLease))
        XCTAssertTrue(viewModel.isPlaying)
        XCTAssertEqual(musicPlayer.stopCallCount, 0)
        XCTAssertFalse(isTestLeaseClosed(musicLease))
        XCTAssertEqual(musicPlayer.volume, 0.8, accuracy: 0.001)
        XCTAssertTrue(viewModel.errorMessage?.contains(effectTrack.title) == true)
    }

    func testStoppingMusicLeavesEffectVoiceAliveUntilEffectsAreStopped() throws {
        let musicTrack = makeTestTrack(title: "Music")
        let effectTrack = makeTestTrack(title: "Bell", role: .effect)
        let musicPlaylist = Playlist(name: "Music", tracks: [musicTrack])
        let effectPlaylist = EffectPlaylist(name: "SFX", effects: [effectTrack])
        let harness = try PlayerTestHarness(
            musicPlaylists: [musicPlaylist],
            effectPlaylists: [effectPlaylist],
            selectedMusicPlaylistID: musicPlaylist.id,
            selectedEffectPlaylistID: effectPlaylist.id
        )
        let musicPlayer = MockAudioPlayer()
        let effectPlayer = MockAudioPlayer()
        let musicLease = makeTestLease(for: musicTrack)
        let effectLease = makeTestLease(for: effectTrack)
        harness.factory.enqueue(musicPlayer)
        harness.factory.enqueue(effectPlayer)
        harness.resolver.enqueue(musicLease, for: musicTrack.id)
        harness.resolver.enqueue(effectLease, for: effectTrack.id)

        let viewModel = harness.makeViewModel()
        defer {
            viewModel.stopAll()
            harness.cleanup()
        }

        XCTAssertTrue(viewModel.playMusicTrack(playlistID: musicPlaylist.id, trackID: musicTrack.id))
        XCTAssertTrue(viewModel.playEffect(playlistID: effectPlaylist.id, trackID: effectTrack.id))

        viewModel.stopMusic()

        XCTAssertFalse(viewModel.isPlaying)
        XCTAssertEqual(musicPlayer.stopCallCount, 1)
        XCTAssertTrue(isTestLeaseClosed(musicLease))
        XCTAssertEqual(viewModel.activeEffectCount, 1)
        XCTAssertEqual(effectPlayer.stopCallCount, 0)
        XCTAssertFalse(isTestLeaseClosed(effectLease))

        viewModel.stopEffects()

        XCTAssertEqual(viewModel.activeEffectCount, 0)
        XCTAssertEqual(effectPlayer.stopCallCount, 1)
        XCTAssertTrue(isTestLeaseClosed(effectLease))
    }

    func testPreviousReturnsAcrossPlaylistHistoryWithoutChangingSidebarSelection() throws {
        let firstTrack = makeTestTrack(title: "First Playlist Track")
        let secondTrack = makeTestTrack(title: "Second Playlist Track")
        let firstPlaylist = Playlist(name: "First", tracks: [firstTrack])
        let secondPlaylist = Playlist(name: "Second", tracks: [secondTrack])
        let harness = try PlayerTestHarness(
            musicPlaylists: [firstPlaylist, secondPlaylist],
            selectedMusicPlaylistID: secondPlaylist.id
        )
        let firstPlayer = MockAudioPlayer()
        let secondPlayer = MockAudioPlayer()
        let returnedFirstPlayer = MockAudioPlayer()
        harness.factory.enqueue(firstPlayer)
        harness.factory.enqueue(secondPlayer)
        harness.factory.enqueue(returnedFirstPlayer)
        harness.resolver.enqueue(makeTestLease(for: firstTrack), for: firstTrack.id)
        harness.resolver.enqueue(makeTestLease(for: secondTrack), for: secondTrack.id)
        harness.resolver.enqueue(makeTestLease(for: firstTrack), for: firstTrack.id)

        let viewModel = harness.makeViewModel()
        defer {
            viewModel.stopAll()
            harness.cleanup()
        }

        XCTAssertTrue(viewModel.playMusicTrack(playlistID: firstPlaylist.id, trackID: firstTrack.id))
        XCTAssertTrue(viewModel.playMusicTrack(playlistID: secondPlaylist.id, trackID: secondTrack.id))
        XCTAssertEqual(viewModel.selectedMusicPlaylistID, secondPlaylist.id)
        XCTAssertEqual(viewModel.playbackMusicPlaylistID, secondPlaylist.id)

        viewModel.previousTrack()

        XCTAssertEqual(viewModel.selectedMusicPlaylistID, secondPlaylist.id)
        XCTAssertEqual(viewModel.playbackMusicPlaylistID, firstPlaylist.id)
        XCTAssertEqual(viewModel.currentTrackID, firstTrack.id)
        XCTAssertTrue(viewModel.isPlaying)
        XCTAssertEqual(secondPlayer.stopCallCount, 1)
        XCTAssertEqual(returnedFirstPlayer.playCallCount, 1)
    }

    func testDeletedCrossPlaylistHistoryIsSkipped() throws {
        let deletedTrack = makeTestTrack(title: "Deleted History")
        let currentTrack = makeTestTrack(title: "Current")
        let deletedPlaylist = Playlist(name: "Deleted", tracks: [deletedTrack])
        let currentPlaylist = Playlist(name: "Current", tracks: [currentTrack])
        let harness = try PlayerTestHarness(
            musicPlaylists: [deletedPlaylist, currentPlaylist],
            selectedMusicPlaylistID: currentPlaylist.id
        )
        let deletedPlayer = MockAudioPlayer()
        let currentPlayer = MockAudioPlayer()
        harness.factory.enqueue(deletedPlayer)
        harness.factory.enqueue(currentPlayer)
        harness.resolver.enqueue(makeTestLease(for: deletedTrack), for: deletedTrack.id)
        harness.resolver.enqueue(makeTestLease(for: currentTrack), for: currentTrack.id)

        let viewModel = harness.makeViewModel()
        defer {
            viewModel.stopAll()
            harness.cleanup()
        }

        XCTAssertTrue(viewModel.playMusicTrack(playlistID: deletedPlaylist.id, trackID: deletedTrack.id))
        XCTAssertTrue(viewModel.playMusicTrack(playlistID: currentPlaylist.id, trackID: currentTrack.id))
        viewModel.deleteMusicPlaylist(deletedPlaylist.id)
        let factoryCallCount = harness.factory.requestedURLs.count

        viewModel.previousTrack()

        XCTAssertEqual(harness.factory.requestedURLs.count, factoryCallCount)
        XCTAssertEqual(viewModel.playbackMusicPlaylistID, currentPlaylist.id)
        XCTAssertEqual(viewModel.currentTrackID, currentTrack.id)
        XCTAssertTrue(viewModel.isPlaying)
    }

    func testMovingHistoricalTrackRelocatesPreviousPlaybackContext() throws {
        let historicalTrack = makeTestTrack(title: "Historical")
        let currentTrack = makeTestTrack(title: "Current")
        let sourcePlaylist = Playlist(name: "Source", tracks: [historicalTrack])
        let currentPlaylist = Playlist(name: "Current", tracks: [currentTrack])
        let destinationPlaylist = Playlist(name: "Destination")
        let harness = try PlayerTestHarness(
            musicPlaylists: [sourcePlaylist, currentPlaylist, destinationPlaylist],
            selectedMusicPlaylistID: currentPlaylist.id
        )
        harness.factory.enqueue(MockAudioPlayer())
        harness.factory.enqueue(MockAudioPlayer())
        let relocatedPlayer = MockAudioPlayer()
        harness.factory.enqueue(relocatedPlayer)
        harness.resolver.enqueue(makeTestLease(for: historicalTrack), for: historicalTrack.id)
        harness.resolver.enqueue(makeTestLease(for: currentTrack), for: currentTrack.id)
        harness.resolver.enqueue(makeTestLease(for: historicalTrack), for: historicalTrack.id)

        let viewModel = harness.makeViewModel()
        defer {
            viewModel.stopAll()
            harness.cleanup()
        }

        XCTAssertTrue(
            viewModel.playMusicTrack(
                playlistID: sourcePlaylist.id,
                trackID: historicalTrack.id
            )
        )
        XCTAssertTrue(
            viewModel.playMusicTrack(
                playlistID: currentPlaylist.id,
                trackID: currentTrack.id
            )
        )
        let relocation = viewModel.transferMusicTracks(
            ids: [historicalTrack.id],
            from: sourcePlaylist.id,
            to: destinationPlaylist.id,
            operation: .move
        )
        XCTAssertEqual(relocation.transferredCount, 1)

        viewModel.previousTrack()

        XCTAssertEqual(viewModel.selectedMusicPlaylistID, currentPlaylist.id)
        XCTAssertEqual(viewModel.playbackMusicPlaylistID, destinationPlaylist.id)
        XCTAssertEqual(viewModel.currentTrackID, historicalTrack.id)
        XCTAssertEqual(relocatedPlayer.playCallCount, 1)
    }

    func testManualNextAdvancesEvenWhenRepeatOneIsEnabled() throws {
        let firstTrack = makeTestTrack(title: "First")
        let secondTrack = makeTestTrack(title: "Second")
        let playlist = Playlist(name: "Music", tracks: [firstTrack, secondTrack])
        let harness = try PlayerTestHarness(
            musicPlaylists: [playlist],
            selectedMusicPlaylistID: playlist.id
        )
        let firstPlayer = MockAudioPlayer()
        let secondPlayer = MockAudioPlayer()
        harness.factory.enqueue(firstPlayer)
        harness.factory.enqueue(secondPlayer)
        harness.resolver.enqueue(makeTestLease(for: firstTrack), for: firstTrack.id)
        harness.resolver.enqueue(makeTestLease(for: secondTrack), for: secondTrack.id)

        let viewModel = harness.makeViewModel()
        defer {
            viewModel.stopAll()
            harness.cleanup()
        }

        XCTAssertTrue(viewModel.playMusicTrack(playlistID: playlist.id, trackID: firstTrack.id))
        viewModel.repeatMode = .one

        viewModel.nextTrack()

        XCTAssertEqual(viewModel.currentTrackID, secondTrack.id)
        XCTAssertTrue(viewModel.isPlaying)
        XCTAssertEqual(firstPlayer.stopCallCount, 1)
        XCTAssertEqual(secondPlayer.playCallCount, 1)
    }

    func testNaturalFinishRepeatOneReplaysCurrentPlayer() throws {
        let track = makeTestTrack(title: "Loop")
        let playlist = Playlist(name: "Music", tracks: [track])
        let harness = try PlayerTestHarness(
            musicPlaylists: [playlist],
            selectedMusicPlaylistID: playlist.id
        )
        let player = MockAudioPlayer(playResults: [true, true])
        let lease = makeTestLease(for: track)
        harness.factory.enqueue(player)
        harness.resolver.enqueue(lease, for: track.id)

        let viewModel = harness.makeViewModel()
        defer {
            viewModel.stopAll()
            harness.cleanup()
        }

        viewModel.repeatMode = .one
        XCTAssertTrue(viewModel.playMusicTrack(playlistID: playlist.id, trackID: track.id))
        player.currentTime = 42

        player.emitFinish(successfully: true)

        XCTAssertTrue(viewModel.isPlaying)
        XCTAssertEqual(viewModel.currentTrackID, track.id)
        XCTAssertEqual(player.currentTime, 0)
        XCTAssertEqual(player.prepareCallCount, 1)
        XCTAssertEqual(player.playCallCount, 2)
        XCTAssertEqual(player.stopCallCount, 0)
        XCTAssertFalse(isTestLeaseClosed(lease))
        XCTAssertEqual(harness.factory.requestedURLs.count, 1)
    }

    func testNaturalFinishRepeatOffStopsAtPlaylistEnd() throws {
        let track = makeTestTrack(title: "End")
        let playlist = Playlist(name: "Music", tracks: [track])
        let harness = try PlayerTestHarness(
            musicPlaylists: [playlist],
            selectedMusicPlaylistID: playlist.id
        )
        let player = MockAudioPlayer()
        let lease = makeTestLease(for: track)
        harness.factory.enqueue(player)
        harness.resolver.enqueue(lease, for: track.id)

        let viewModel = harness.makeViewModel()
        defer {
            viewModel.stopAll()
            harness.cleanup()
        }

        viewModel.repeatMode = .off
        XCTAssertTrue(viewModel.playMusicTrack(playlistID: playlist.id, trackID: track.id))

        player.emitFinish(successfully: true)

        XCTAssertFalse(viewModel.isPlaying)
        XCTAssertEqual(viewModel.currentTrackID, track.id)
        XCTAssertEqual(player.stopCallCount, 1)
        XCTAssertNil(player.eventDelegate)
        XCTAssertTrue(isTestLeaseClosed(lease))
        XCTAssertEqual(harness.factory.requestedURLs.count, 1)
    }

    func testNaturalFinishRepeatAllWrapsToFirstTrack() throws {
        let firstTrack = makeTestTrack(title: "First")
        let lastTrack = makeTestTrack(title: "Last")
        let playlist = Playlist(name: "Music", tracks: [firstTrack, lastTrack])
        let harness = try PlayerTestHarness(
            musicPlaylists: [playlist],
            selectedMusicPlaylistID: playlist.id
        )
        let lastPlayer = MockAudioPlayer()
        let wrappedPlayer = MockAudioPlayer()
        let lastLease = makeTestLease(for: lastTrack)
        let wrappedLease = makeTestLease(for: firstTrack)
        harness.factory.enqueue(lastPlayer)
        harness.factory.enqueue(wrappedPlayer)
        harness.resolver.enqueue(lastLease, for: lastTrack.id)
        harness.resolver.enqueue(wrappedLease, for: firstTrack.id)

        let viewModel = harness.makeViewModel()
        defer {
            viewModel.stopAll()
            harness.cleanup()
        }

        viewModel.repeatMode = .all
        XCTAssertTrue(viewModel.playMusicTrack(playlistID: playlist.id, trackID: lastTrack.id))

        lastPlayer.emitFinish(successfully: true)

        XCTAssertTrue(viewModel.isPlaying)
        XCTAssertEqual(viewModel.currentTrackID, firstTrack.id)
        XCTAssertEqual(viewModel.playbackMusicPlaylistID, playlist.id)
        XCTAssertEqual(lastPlayer.stopCallCount, 1)
        XCTAssertTrue(isTestLeaseClosed(lastLease))
        XCTAssertEqual(wrappedPlayer.playCallCount, 1)
        XCTAssertEqual(wrappedPlayer.stopCallCount, 0)
        XCTAssertFalse(isTestLeaseClosed(wrappedLease))
    }
}
