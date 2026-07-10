import XCTest
@testable import DungeonSoundboardCore

final class TrackTransferTests: XCTestCase {
    private let sourcePlaylistID = UUID(uuidString: "30000000-0000-0000-0000-000000000000")!
    private let destinationPlaylistID = UUID(uuidString: "40000000-0000-0000-0000-000000000000")!
    private let firstID = UUID(uuidString: "30000000-0000-0000-0000-000000000001")!
    private let secondID = UUID(uuidString: "30000000-0000-0000-0000-000000000002")!
    private let thirdID = UUID(uuidString: "30000000-0000-0000-0000-000000000003")!

    func testMovePreservesIDsAndTransfersInSourceOrder() throws {
        let first = Track(id: firstID, title: "First", path: "/tmp/first.mp3", volumeMultiplier: 0.7)
        let second = Track(id: secondID, title: "Second", path: "/tmp/second.mp3")
        let third = Track(id: thirdID, title: "Third", path: "/tmp/third.mp3")
        var playlists = [
            Playlist(id: sourcePlaylistID, name: "Source", tracks: [first, second, third]),
            Playlist(id: destinationPlaylistID, name: "Destination")
        ]

        let result = try XCTUnwrap(
            playlists.transferMusicTracks(
                [thirdID, firstID],
                from: sourcePlaylistID,
                to: destinationPlaylistID,
                operation: .move
            )
        )

        XCTAssertEqual(playlists[0].tracks.map(\.id), [secondID])
        XCTAssertEqual(playlists[1].tracks.map(\.id), [firstID, thirdID])
        XCTAssertEqual(playlists[1].tracks[0], first)
        XCTAssertEqual(result.transferredSourceTrackIDs, [firstID, thirdID])
        XCTAssertEqual(result.destinationTrackIDs, [firstID, thirdID])
        XCTAssertEqual(result.role, .music)
        XCTAssertEqual(result.attemptedCount, 2)
        XCTAssertFalse(result.hasPartialConflicts)
    }

    func testCopyCreatesNewIDsAndPreservesTrackMetadata() throws {
        let bookmark = Data([0x01, 0x02, 0x03])
        let source = Track(
            id: firstID,
            title: "Rain",
            path: "/tmp/rain.wav",
            role: .music,
            bookmarkData: bookmark,
            volumeMultiplier: 1.4
        )
        let copyID = UUID(uuidString: "50000000-0000-0000-0000-000000000001")!
        var playlists = [
            Playlist(id: sourcePlaylistID, name: "Source", tracks: [source]),
            Playlist(id: destinationPlaylistID, name: "Destination")
        ]

        let result = try XCTUnwrap(
            playlists.transferMusicTracks(
                [firstID],
                from: sourcePlaylistID,
                to: destinationPlaylistID,
                operation: .copy,
                makeTrackID: { copyID }
            )
        )

        XCTAssertEqual(playlists[0].tracks, [source])
        let copied = try XCTUnwrap(playlists[1].tracks.first)
        XCTAssertEqual(copied.id, copyID)
        XCTAssertEqual(copied.title, source.title)
        XCTAssertEqual(copied.path, source.path)
        XCTAssertEqual(copied.role, source.role)
        XCTAssertEqual(copied.bookmarkData, source.bookmarkData)
        XCTAssertEqual(copied.volumeMultiplier, source.volumeMultiplier)
        XCTAssertEqual(
            result.transferred,
            [TrackTransferRecord(sourceTrackID: firstID, destinationTrackID: copyID)]
        )
    }

    func testMoveReportsPartialDuplicatesAndLeavesSkippedSourceTracksInPlace() throws {
        let duplicate = Track(id: firstID, title: "Rain", path: "/tmp/Rain.wav")
        let transferable = Track(id: secondID, title: "Wind", path: "/tmp/wind.wav")
        let existing = Track(title: "Existing Rain", path: "/tmp/rain.wav")
        let missingID = UUID(uuidString: "30000000-0000-0000-0000-000000000099")!
        var playlists = [
            Playlist(id: sourcePlaylistID, name: "Source", tracks: [duplicate, transferable]),
            Playlist(id: destinationPlaylistID, name: "Destination", tracks: [existing])
        ]

        let result = try XCTUnwrap(
            playlists.transferMusicTracks(
                [firstID, secondID, missingID],
                from: sourcePlaylistID,
                to: destinationPlaylistID,
                operation: .move
            )
        )

        XCTAssertEqual(playlists[0].tracks.map(\.id), [firstID])
        XCTAssertEqual(playlists[1].tracks.map(\.id), [existing.id, secondID])
        XCTAssertEqual(result.transferredSourceTrackIDs, [secondID])
        XCTAssertEqual(result.duplicateTrackIDs, [firstID])
        XCTAssertEqual(result.missingTrackIDs, [missingID])
        XCTAssertEqual(result.attemptedCount, 3)
        XCTAssertTrue(result.hasPartialConflicts)
    }

    func testDuplicatesWithinTransferBatchAreReportedStably() throws {
        let first = Track(id: firstID, title: "Rain One", path: "/tmp/rain.wav")
        let second = Track(id: secondID, title: "Rain Two", path: "/tmp/RAIN.wav")
        var playlists = [
            Playlist(id: sourcePlaylistID, name: "Source", tracks: [first, second]),
            Playlist(id: destinationPlaylistID, name: "Destination")
        ]

        let result = try XCTUnwrap(
            playlists.transferMusicTracks(
                [firstID, secondID],
                from: sourcePlaylistID,
                to: destinationPlaylistID,
                operation: .copy,
                makeTrackID: { UUID(uuidString: "50000000-0000-0000-0000-000000000002")! }
            )
        )

        XCTAssertEqual(result.transferredSourceTrackIDs, [firstID])
        XCTAssertEqual(result.duplicateTrackIDs, [secondID])
        XCTAssertEqual(playlists[1].tracks.count, 1)
    }

    func testExistingDestinationTrackIDIsReportedAsDuplicateEvenWhenPathDiffers() throws {
        let source = Track(id: firstID, title: "Source", path: "/tmp/source.mp3")
        let corruptDestinationCopy = Track(id: firstID, title: "Other", path: "/tmp/other.mp3")
        var playlists = [
            Playlist(id: sourcePlaylistID, name: "Source", tracks: [source]),
            Playlist(
                id: destinationPlaylistID,
                name: "Destination",
                tracks: [corruptDestinationCopy]
            )
        ]

        let result = try XCTUnwrap(
            playlists.transferMusicTracks(
                [firstID],
                from: sourcePlaylistID,
                to: destinationPlaylistID,
                operation: .move
            )
        )

        XCTAssertEqual(result.duplicateTrackIDs, [firstID])
        XCTAssertTrue(result.transferred.isEmpty)
        XCTAssertEqual(playlists[0].tracks, [source])
        XCTAssertEqual(playlists[1].tracks, [corruptDestinationCopy])
    }

    func testCopyCanRepairSourceIDCollisionByGeneratingFreshID() throws {
        let source = Track(id: firstID, title: "Source", path: "/tmp/source.mp3")
        let collidingDestination = Track(id: firstID, title: "Other", path: "/tmp/other.mp3")
        let copiedID = UUID(uuidString: "50000000-0000-0000-0000-000000000099")!
        var playlists = [
            Playlist(id: sourcePlaylistID, name: "Source", tracks: [source]),
            Playlist(id: destinationPlaylistID, name: "Destination", tracks: [collidingDestination])
        ]

        let result = try XCTUnwrap(
            playlists.transferMusicTracks(
                [firstID],
                from: sourcePlaylistID,
                to: destinationPlaylistID,
                operation: .copy,
                makeTrackID: { copiedID }
            )
        )

        XCTAssertEqual(result.destinationTrackIDs, [copiedID])
        XCTAssertTrue(result.duplicateTrackIDs.isEmpty)
        XCTAssertEqual(playlists[1].tracks.map(\.id), [firstID, copiedID])
    }

    func testSymlinkAndActualPathAreReportedAsTheSameFile() throws {
        let fileManager = FileManager.default
        let directory = fileManager.temporaryDirectory
            .appendingPathComponent("TrackTransferTests-\(UUID().uuidString)", isDirectory: true)
        let actualURL = directory.appendingPathComponent("rain.wav")
        let symlinkURL = directory.appendingPathComponent("rain-alias.wav")
        try fileManager.createDirectory(at: directory, withIntermediateDirectories: true)
        defer { try? fileManager.removeItem(at: directory) }
        XCTAssertTrue(fileManager.createFile(atPath: actualURL.path, contents: Data([0x01])))
        try fileManager.createSymbolicLink(at: symlinkURL, withDestinationURL: actualURL)

        let source = Track(id: firstID, title: "Alias", path: symlinkURL.path)
        let existing = Track(title: "Actual", path: actualURL.path)
        var playlists = [
            Playlist(id: sourcePlaylistID, name: "Source", tracks: [source]),
            Playlist(id: destinationPlaylistID, name: "Destination", tracks: [existing])
        ]

        let result = try XCTUnwrap(
            playlists.transferMusicTracks(
                [firstID],
                from: sourcePlaylistID,
                to: destinationPlaylistID,
                operation: .move
            )
        )

        XCTAssertEqual(result.duplicateTrackIDs, [firstID])
        XCTAssertTrue(result.transferred.isEmpty)
        XCTAssertEqual(playlists[0].tracks, [source])
    }

    func testResolvedBookmarkTakesPrecedenceOverStaleStoredPath() throws {
        let fileManager = FileManager.default
        let directory = fileManager.temporaryDirectory
            .appendingPathComponent("TrackTransferBookmarkTests-\(UUID().uuidString)", isDirectory: true)
        let actualURL = directory.appendingPathComponent("thunder.wav")
        try fileManager.createDirectory(at: directory, withIntermediateDirectories: true)
        defer { try? fileManager.removeItem(at: directory) }
        XCTAssertTrue(fileManager.createFile(atPath: actualURL.path, contents: Data([0x02])))
        let bookmark = try actualURL.bookmarkData(
            options: [.minimalBookmark],
            includingResourceValuesForKeys: nil,
            relativeTo: nil
        )

        let source = Track(
            id: firstID,
            title: "Bookmarked",
            path: "/stale/location/thunder.wav",
            bookmarkData: bookmark
        )
        let existing = Track(title: "Actual", path: actualURL.path)
        var playlists = [
            Playlist(id: sourcePlaylistID, name: "Source", tracks: [source]),
            Playlist(id: destinationPlaylistID, name: "Destination", tracks: [existing])
        ]

        let result = try XCTUnwrap(
            playlists.transferMusicTracks(
                [firstID],
                from: sourcePlaylistID,
                to: destinationPlaylistID,
                operation: .copy
            )
        )

        XCTAssertEqual(result.duplicateTrackIDs, [firstID])
        XCTAssertTrue(result.transferred.isEmpty)
    }

    func testEffectTransferUsesEffectRoleAndPreservesMoveIdentity() throws {
        let effect = Track(id: firstID, title: "Thunder", path: "/tmp/thunder.wav", role: .effect)
        var playlists = [
            EffectPlaylist(id: sourcePlaylistID, name: "Source", effects: [effect]),
            EffectPlaylist(id: destinationPlaylistID, name: "Destination")
        ]

        let result = try XCTUnwrap(
            playlists.transferEffectTracks(
                [firstID],
                from: sourcePlaylistID,
                to: destinationPlaylistID,
                operation: .move
            )
        )

        XCTAssertEqual(result.role, .effect)
        XCTAssertTrue(playlists[0].effects.isEmpty)
        XCTAssertEqual(playlists[1].effects, [effect])
    }

    func testInvalidOrSamePlaylistTransferDoesNotMutateLibrary() {
        let track = Track(id: firstID, title: "First", path: "/tmp/first.mp3")
        let original = [Playlist(id: sourcePlaylistID, name: "Source", tracks: [track])]
        var playlists = original

        XCTAssertNil(
            playlists.transferMusicTracks(
                [firstID],
                from: sourcePlaylistID,
                to: sourcePlaylistID,
                operation: .move
            )
        )
        XCTAssertEqual(playlists, original)
    }

    func testStableGroupReorderForwardAndBackward() {
        let fourthID = UUID(uuidString: "30000000-0000-0000-0000-000000000004")!
        let fifthID = UUID(uuidString: "30000000-0000-0000-0000-000000000005")!
        let tracks = [firstID, secondID, thirdID, fourthID, fifthID].map {
            Track(id: $0, title: $0.uuidString, path: "/tmp/\($0).mp3")
        }
        var playlists = [Playlist(id: sourcePlaylistID, name: "Source", tracks: tracks)]

        XCTAssertTrue(
            playlists.reorderMusicTracks(
                [firstID, secondID],
                in: sourcePlaylistID,
                to: fourthID
            )
        )
        XCTAssertEqual(playlists[0].tracks.map(\.id), [thirdID, fourthID, firstID, secondID, fifthID])

        XCTAssertTrue(
            playlists.reorderMusicTracks(
                [firstID, secondID],
                in: sourcePlaylistID,
                to: thirdID
            )
        )
        XCTAssertEqual(playlists[0].tracks.map(\.id), [firstID, secondID, thirdID, fourthID, fifthID])
    }

    func testStableGroupCanReorderToEndWhenLastTrackIsSelected() {
        let fourthID = UUID(uuidString: "30000000-0000-0000-0000-000000000004")!
        let tracks = [firstID, secondID, thirdID, fourthID].map {
            Track(id: $0, title: $0.uuidString, path: "/tmp/\($0).mp3")
        }
        var playlists = [Playlist(id: sourcePlaylistID, name: "Source", tracks: tracks)]

        XCTAssertTrue(
            playlists.reorderMusicTracksToEnd(
                [firstID, fourthID],
                in: sourcePlaylistID
            )
        )
        XCTAssertEqual(playlists[0].tracks.map(\.id), [secondID, thirdID, firstID, fourthID])
        XCTAssertFalse(
            playlists.reorderMusicTracksToEnd(
                [firstID, fourthID],
                in: sourcePlaylistID
            )
        )
    }

    func testEffectGroupCanReorderToEndStably() {
        let effects = [firstID, secondID, thirdID].map {
            Track(id: $0, title: $0.uuidString, path: "/tmp/\($0).wav", role: .effect)
        }
        var playlists = [
            EffectPlaylist(id: sourcePlaylistID, name: "Effects", effects: effects)
        ]

        XCTAssertTrue(
            playlists.reorderEffectTracksToEnd(
                [firstID, secondID],
                in: sourcePlaylistID
            )
        )
        XCTAssertEqual(playlists[0].effects.map(\.id), [thirdID, firstID, secondID])
    }

    func testReorderRejectsTargetInsideMovingGroup() {
        let tracks = [firstID, secondID, thirdID].map {
            Track(id: $0, title: $0.uuidString, path: "/tmp/\($0).mp3")
        }
        var playlists = [Playlist(id: sourcePlaylistID, name: "Source", tracks: tracks)]

        XCTAssertFalse(
            playlists.reorderMusicTracks(
                [firstID, secondID],
                in: sourcePlaylistID,
                to: secondID
            )
        )
        XCTAssertEqual(playlists[0].tracks.map(\.id), [firstID, secondID, thirdID])
    }
}
