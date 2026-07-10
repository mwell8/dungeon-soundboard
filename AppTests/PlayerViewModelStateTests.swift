import Foundation
import UniformTypeIdentifiers
import XCTest
@testable import Dungeon_Soundboard

@MainActor
final class PlayerViewModelStateTests: XCTestCase {
    func testLegacyMigrationAndPreferenceNormalizationArePersisted() throws {
        let legacyPlaylistID = UUID()
        let musicTrack = makeTestTrack(title: "Legacy Music")
        let effectTrack = makeTestTrack(title: "Legacy Effect", role: .effect)
        let legacyPlaylist = Playlist(
            id: legacyPlaylistID,
            name: "Legacy",
            tracks: [musicTrack, effectTrack]
        )
        let harness = try PlayerTestHarness(migrationCompleted: false)
        harness.defaults.set(
            try JSONEncoder().encode([legacyPlaylist]),
            forKey: PlayerDefaultsKeys.legacyPlaylists
        )
        harness.defaults.set(
            legacyPlaylistID.uuidString,
            forKey: PlayerDefaultsKeys.legacySelectedPlaylistID
        )
        harness.defaults.set(1.8, forKey: PlayerDefaultsKeys.volume)
        harness.defaults.set(-0.5, forKey: PlayerDefaultsKeys.effectsVolume)
        harness.defaults.set("Повтор трека", forKey: PlayerDefaultsKeys.repeatMode)
        harness.defaults.set(true, forKey: PlayerDefaultsKeys.shuffleEnabled)
        harness.defaults.set(true, forKey: PlayerDefaultsKeys.musicFadeOutOnPauseEnabled)
        harness.defaults.set(9.0, forKey: PlayerDefaultsKeys.duckingAmount)
        harness.defaults.set(7, forKey: PlayerDefaultsKeys.musicColumns)
        harness.defaults.set(1, forKey: PlayerDefaultsKeys.effectsColumns)

        let viewModel = harness.makeViewModel()
        defer {
            viewModel.stopAll()
            harness.cleanup()
        }

        XCTAssertTrue(harness.defaults.bool(forKey: PlayerDefaultsKeys.migrationCompleted))
        XCTAssertEqual(viewModel.musicPlaylists.count, 1)
        XCTAssertEqual(viewModel.musicPlaylists[0].id, legacyPlaylistID)
        XCTAssertEqual(viewModel.musicPlaylists[0].tracks.map(\.id), [musicTrack.id])
        XCTAssertEqual(viewModel.effectPlaylists.count, 1)
        XCTAssertEqual(viewModel.effectPlaylists[0].effects.map(\.id), [effectTrack.id])
        XCTAssertEqual(viewModel.selectedMusicPlaylistID, legacyPlaylistID)

        let persistedMusic = try harness.persistedMusicPlaylists()
        let persistedEffects = try harness.persistedEffectPlaylists()
        XCTAssertEqual(persistedMusic, viewModel.musicPlaylists)
        XCTAssertEqual(persistedEffects, viewModel.effectPlaylists)

        XCTAssertEqual(viewModel.volume, 1, accuracy: 0.0001)
        XCTAssertEqual(viewModel.effectsVolume, 0, accuracy: 0.0001)
        XCTAssertEqual(viewModel.repeatMode, .one)
        XCTAssertTrue(viewModel.isShuffleEnabled)
        XCTAssertTrue(viewModel.isMusicFadeOutOnPauseEnabled)
        XCTAssertEqual(viewModel.duckingAmount, 1, accuracy: 0.0001)
        XCTAssertEqual(viewModel.musicColumnsCount, 3)
        XCTAssertEqual(viewModel.effectsColumnsCount, 3)
        XCTAssertEqual(harness.defaults.double(forKey: PlayerDefaultsKeys.volume), 1, accuracy: 0.0001)
        XCTAssertEqual(harness.defaults.double(forKey: PlayerDefaultsKeys.effectsVolume), 0, accuracy: 0.0001)
        XCTAssertEqual(harness.defaults.string(forKey: PlayerDefaultsKeys.repeatMode), RepeatMode.one.rawValue)
        XCTAssertTrue(harness.defaults.bool(forKey: PlayerDefaultsKeys.shuffleEnabled))
        XCTAssertTrue(harness.defaults.bool(forKey: PlayerDefaultsKeys.musicFadeOutOnPauseEnabled))
        XCTAssertEqual(harness.defaults.double(forKey: PlayerDefaultsKeys.duckingAmount), 1, accuracy: 0.0001)
        XCTAssertEqual(harness.defaults.integer(forKey: PlayerDefaultsKeys.musicColumns), 3)
        XCTAssertEqual(harness.defaults.integer(forKey: PlayerDefaultsKeys.effectsColumns), 3)
    }

    func testMovingPlayingTrackRelocatesPlaybackWithoutRestartingPlayer() throws {
        let playingTrack = makeTestTrack(title: "Playing")
        let sourcePlaylist = Playlist(name: "Source", tracks: [playingTrack])
        let destinationPlaylist = Playlist(name: "Destination")
        let harness = try PlayerTestHarness(
            musicPlaylists: [sourcePlaylist, destinationPlaylist],
            selectedMusicPlaylistID: sourcePlaylist.id
        )
        let player = MockAudioPlayer()
        let lease = makeTestLease(for: playingTrack)
        harness.factory.enqueue(player)
        harness.resolver.enqueue(lease, for: playingTrack.id)

        let viewModel = harness.makeViewModel()
        defer {
            viewModel.stopAll()
            harness.cleanup()
        }

        XCTAssertTrue(
            viewModel.playMusicTrack(
                playlistID: sourcePlaylist.id,
                trackID: playingTrack.id
            )
        )

        let result = viewModel.transferMusicTracks(
            ids: [playingTrack.id],
            from: sourcePlaylist.id,
            to: destinationPlaylist.id,
            operation: .move
        )

        XCTAssertEqual(result.transferredCount, 1)
        XCTAssertEqual(viewModel.selectedMusicPlaylistID, sourcePlaylist.id)
        XCTAssertEqual(viewModel.playbackMusicPlaylistID, destinationPlaylist.id)
        XCTAssertEqual(viewModel.currentTrackID, playingTrack.id)
        XCTAssertEqual(viewModel.currentTrack?.id, playingTrack.id)
        XCTAssertTrue(viewModel.isPlaying)
        XCTAssertEqual(player.stopCallCount, 0)
        XCTAssertNotNil(player.eventDelegate)
        XCTAssertFalse(isTestLeaseClosed(lease))
        XCTAssertTrue(
            viewModel.musicPlaylists
                .first { $0.id == sourcePlaylist.id }?
                .tracks
                .isEmpty == true
        )
        XCTAssertEqual(
            viewModel.musicPlaylists
                .first { $0.id == destinationPlaylist.id }?
                .tracks
                .map(\.id),
            [playingTrack.id]
        )

        let persisted = try harness.persistedMusicPlaylists()
        XCTAssertEqual(
            persisted.first { $0.id == destinationPlaylist.id }?.tracks.map(\.id),
            [playingTrack.id]
        )
    }

    func testMovingActiveEffectKeepsVoiceAndLeaseAlive() throws {
        let effect = makeTestTrack(title: "Active Effect", role: .effect)
        let sourcePlaylist = EffectPlaylist(name: "Source", effects: [effect])
        let destinationPlaylist = EffectPlaylist(name: "Destination")
        let harness = try PlayerTestHarness(
            effectPlaylists: [sourcePlaylist, destinationPlaylist],
            selectedEffectPlaylistID: sourcePlaylist.id
        )
        let player = MockAudioPlayer()
        let lease = makeTestLease(for: effect)
        harness.factory.enqueue(player)
        harness.resolver.enqueue(lease, for: effect.id)

        let viewModel = harness.makeViewModel()
        defer {
            viewModel.stopAll()
            harness.cleanup()
        }

        XCTAssertTrue(viewModel.playEffect(playlistID: sourcePlaylist.id, trackID: effect.id))

        let result = viewModel.transferEffectTracks(
            ids: [effect.id],
            from: sourcePlaylist.id,
            to: destinationPlaylist.id,
            operation: .move
        )

        XCTAssertEqual(result.transferredCount, 1)
        XCTAssertEqual(viewModel.selectedEffectPlaylistID, sourcePlaylist.id)
        XCTAssertEqual(viewModel.activeEffectCount, 1)
        XCTAssertEqual(player.stopCallCount, 0)
        XCTAssertNotNil(player.eventDelegate)
        XCTAssertFalse(isTestLeaseClosed(lease))
        XCTAssertTrue(
            viewModel.effectPlaylists
                .first { $0.id == sourcePlaylist.id }?
                .effects
                .isEmpty == true
        )
        XCTAssertEqual(
            viewModel.effectPlaylists
                .first { $0.id == destinationPlaylist.id }?
                .effects
                .map(\.id),
            [effect.id]
        )

        let persisted = try harness.persistedEffectPlaylists()
        XCTAssertEqual(
            persisted.first { $0.id == destinationPlaylist.id }?.effects.map(\.id),
            [effect.id]
        )

        player.emitFinish(successfully: true)
        XCTAssertEqual(viewModel.activeEffectCount, 0)
        XCTAssertEqual(player.stopCallCount, 1)
        XCTAssertTrue(isTestLeaseClosed(lease))
    }

    func testDeletingSelectedPlaylistDoesNotStopDifferentPlaybackContext() throws {
        let selectedTrack = makeTestTrack(title: "Selected")
        let playingTrack = makeTestTrack(title: "Playing Elsewhere")
        let selectedPlaylist = Playlist(name: "Selected", tracks: [selectedTrack])
        let playbackPlaylist = Playlist(name: "Playback", tracks: [playingTrack])
        let harness = try PlayerTestHarness(
            musicPlaylists: [selectedPlaylist, playbackPlaylist],
            selectedMusicPlaylistID: selectedPlaylist.id
        )
        let player = MockAudioPlayer()
        let lease = makeTestLease(for: playingTrack)
        harness.factory.enqueue(player)
        harness.resolver.enqueue(lease, for: playingTrack.id)

        let viewModel = harness.makeViewModel()
        defer {
            viewModel.stopAll()
            harness.cleanup()
        }

        XCTAssertTrue(
            viewModel.playMusicTrack(
                playlistID: playbackPlaylist.id,
                trackID: playingTrack.id
            )
        )
        viewModel.deleteMusicPlaylist(selectedPlaylist.id)

        XCTAssertTrue(viewModel.isPlaying)
        XCTAssertEqual(viewModel.playbackMusicPlaylistID, playbackPlaylist.id)
        XCTAssertEqual(viewModel.currentTrackID, playingTrack.id)
        XCTAssertEqual(player.stopCallCount, 0)
        XCTAssertFalse(lease.isClosed)
    }

    func testDeletingPlayingTrackStopsOnlyMusicAndSelectsNearestWithoutAutoplay() throws {
        let first = makeTestTrack(title: "First")
        let playing = makeTestTrack(title: "Playing")
        let nearest = makeTestTrack(title: "Nearest")
        let playlist = Playlist(name: "Music", tracks: [first, playing, nearest])
        let harness = try PlayerTestHarness(
            musicPlaylists: [playlist],
            selectedMusicPlaylistID: playlist.id
        )
        let player = MockAudioPlayer()
        let lease = makeTestLease(for: playing)
        harness.factory.enqueue(player)
        harness.resolver.enqueue(lease, for: playing.id)

        let viewModel = harness.makeViewModel()
        defer {
            viewModel.stopAll()
            harness.cleanup()
        }

        XCTAssertTrue(viewModel.playMusicTrack(playlistID: playlist.id, trackID: playing.id))
        let factoryCallsBeforeDelete = harness.factory.requestedURLs.count
        viewModel.removeMusicTrack(playing)

        XCTAssertFalse(viewModel.isPlaying)
        XCTAssertEqual(viewModel.currentTrackID, nearest.id)
        XCTAssertEqual(harness.factory.requestedURLs.count, factoryCallsBeforeDelete)
        XCTAssertEqual(player.stopCallCount, 1)
        XCTAssertTrue(lease.isClosed)
    }

    func testTargetedDroppedImportUsesExplicitPlaylistInsteadOfCurrentSelection() async throws {
        let selectedPlaylist = Playlist(name: "Selected")
        let targetPlaylist = Playlist(name: "Target")
        let harness = try PlayerTestHarness(
            musicPlaylists: [selectedPlaylist, targetPlaylist],
            selectedMusicPlaylistID: selectedPlaylist.id
        )
        let bookmarkData = Data([0x01, 0x02, 0x03])
        harness.resolver.bookmarkDataToReturn = bookmarkData

        let viewModel = harness.makeViewModel()
        defer {
            viewModel.stopAll()
            harness.cleanup()
        }

        let directoryURL = FileManager.default.temporaryDirectory
            .appendingPathComponent("DungeonSoundboardTargetImport-\(UUID().uuidString)", isDirectory: true)
        try FileManager.default.createDirectory(at: directoryURL, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: directoryURL) }
        let audioURL = directoryURL.appendingPathComponent("Explicit Target.mp3")
        try Data([0]).write(to: audioURL)

        // Повтор в одной пачке проверяет, что `seen` пополняется по мере импорта.
        viewModel.importDroppedMusicURLs([audioURL, audioURL], to: targetPlaylist.id)

        let imported = await waitUntil {
            viewModel.musicPlaylists
                .first { $0.id == targetPlaylist.id }?
                .tracks
                .count == 1
        }

        XCTAssertTrue(imported)
        XCTAssertEqual(viewModel.selectedMusicPlaylistID, selectedPlaylist.id)
        XCTAssertTrue(
            viewModel.musicPlaylists
                .first { $0.id == selectedPlaylist.id }?
                .tracks
                .isEmpty == true
        )
        let importedTrack = try XCTUnwrap(
            viewModel.musicPlaylists
                .first { $0.id == targetPlaylist.id }?
                .tracks
                .first
        )
        XCTAssertEqual(importedTrack.title, "Explicit Target")
        XCTAssertEqual(importedTrack.path, audioURL.standardizedFileURL.path)
        XCTAssertEqual(importedTrack.role, .music)
        XCTAssertEqual(importedTrack.bookmarkData, bookmarkData)
        XCTAssertEqual(
            harness.resolver.bookmarkRequests,
            [audioURL.standardizedFileURL, audioURL.standardizedFileURL]
        )
        XCTAssertEqual(viewModel.importConflictSummary?.attemptedCount, 2)
        XCTAssertEqual(viewModel.importConflictSummary?.addedCount, 1)
        XCTAssertEqual(viewModel.importConflictSummary?.duplicateCount, 1)

        let persisted = try harness.persistedMusicPlaylists()
        XCTAssertEqual(
            persisted.first { $0.id == targetPlaylist.id }?.tracks.first?.id,
            importedTrack.id
        )
    }

    func testFinderItemProviderCollectorResolvesFileURL() async throws {
        let directoryURL = FileManager.default.temporaryDirectory
            .appendingPathComponent("DungeonSoundboardFinderProvider-\(UUID().uuidString)", isDirectory: true)
        try FileManager.default.createDirectory(at: directoryURL, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: directoryURL) }

        let audioURL = directoryURL.appendingPathComponent("Finder Drop.mp3")
        try Data([0x01]).write(to: audioURL)
        let provider = try XCTUnwrap(NSItemProvider(contentsOf: audioURL))
        XCTAssertTrue(provider.hasItemConformingToTypeIdentifier(UTType.fileURL.identifier))

        var collectedURLs: [URL]?
        let didStartLoading = OrderedFileURLDropCollector.collect(from: [provider]) { urls in
            collectedURLs = urls
        }

        XCTAssertTrue(didStartLoading)
        let didCollectURL = await waitUntil { collectedURLs != nil }
        XCTAssertTrue(didCollectURL)
        XCTAssertEqual(
            collectedURLs?.map(\.standardizedFileURL),
            [audioURL.standardizedFileURL]
        )
    }

    func testDroppedEffectFolderImportsRecursivelyIntoExplicitTarget() async throws {
        let selectedPlaylist = EffectPlaylist(name: "Selected")
        let targetPlaylist = EffectPlaylist(name: "Target")
        let harness = try PlayerTestHarness(
            effectPlaylists: [selectedPlaylist, targetPlaylist],
            selectedEffectPlaylistID: selectedPlaylist.id
        )
        let bookmarkData = Data([0x0A, 0x0B])
        harness.resolver.bookmarkDataToReturn = bookmarkData

        let viewModel = harness.makeViewModel()
        defer {
            viewModel.stopAll()
            harness.cleanup()
        }

        let directoryURL = FileManager.default.temporaryDirectory
            .appendingPathComponent("DungeonSoundboardRecursiveImport-\(UUID().uuidString)", isDirectory: true)
        let nestedURL = directoryURL.appendingPathComponent("Nested/Deeper", isDirectory: true)
        try FileManager.default.createDirectory(at: nestedURL, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: directoryURL) }

        let doorURL = nestedURL.appendingPathComponent("A Door.mp3")
        let thunderURL = directoryURL.appendingPathComponent("B Thunder.wav")
        let ignoredURL = nestedURL.appendingPathComponent("notes.txt")
        try Data([0x01]).write(to: doorURL)
        try Data([0x02]).write(to: thunderURL)
        try Data([0x03]).write(to: ignoredURL)

        viewModel.importDroppedEffectURLs([directoryURL], to: targetPlaylist.id)

        let imported = await waitUntil {
            viewModel.effectPlaylists
                .first { $0.id == targetPlaylist.id }?
                .effects
                .count == 2
        }

        XCTAssertTrue(imported)
        XCTAssertEqual(viewModel.selectedEffectPlaylistID, selectedPlaylist.id)
        XCTAssertTrue(
            viewModel.effectPlaylists
                .first { $0.id == selectedPlaylist.id }?
                .effects
                .isEmpty == true
        )
        let importedEffects = try XCTUnwrap(
            viewModel.effectPlaylists
                .first { $0.id == targetPlaylist.id }?
                .effects
        )
        XCTAssertEqual(importedEffects.map(\.title), ["A Door", "B Thunder"])
        XCTAssertEqual(importedEffects.map(\.role), [.effect, .effect])
        XCTAssertEqual(
            importedEffects.map(\.path),
            [doorURL.standardizedFileURL.path, thunderURL.standardizedFileURL.path]
        )
        XCTAssertEqual(importedEffects.map(\.bookmarkData), [bookmarkData, bookmarkData])
        XCTAssertEqual(
            harness.resolver.bookmarkRequests,
            [doorURL.standardizedFileURL, thunderURL.standardizedFileURL]
        )

        let persisted = try harness.persistedEffectPlaylists()
        XCTAssertEqual(
            persisted.first { $0.id == targetPlaylist.id }?.effects.map(\.id),
            importedEffects.map(\.id)
        )
    }

    private func waitUntil(
        attempts: Int = 100,
        condition: () -> Bool
    ) async -> Bool {
        for _ in 0..<attempts {
            if condition() { return true }
            await Task.yield()
            try? await Task.sleep(nanoseconds: 5_000_000)
        }
        return condition()
    }
}
