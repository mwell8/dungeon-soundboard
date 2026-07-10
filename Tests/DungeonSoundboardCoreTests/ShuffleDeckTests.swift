import XCTest
@testable import DungeonSoundboardCore

final class ShuffleDeckTests: XCTestCase {
    private let playlistID = UUID(uuidString: "10000000-0000-0000-0000-000000000000")!
    private let firstID = UUID(uuidString: "10000000-0000-0000-0000-000000000001")!
    private let secondID = UUID(uuidString: "10000000-0000-0000-0000-000000000002")!
    private let thirdID = UUID(uuidString: "10000000-0000-0000-0000-000000000003")!

    func testMusicTrackReferenceIncludesPlaylistContext() {
        let otherPlaylistID = UUID(uuidString: "20000000-0000-0000-0000-000000000000")!
        let first = MusicTrackReference(playlistID: playlistID, trackID: firstID)
        let second = MusicTrackReference(playlistID: otherPlaylistID, trackID: firstID)

        XCTAssertNotEqual(first, second)
        XCTAssertEqual(Set([first, second]).count, 2)
    }

    func testRepeatOffConsumesOneDeterministicCycleThenStops() {
        var deck = ShuffleDeck(
            playlistID: playlistID,
            trackIDs: [firstID, secondID, thirdID],
            excluding: firstID,
            shuffler: { $0 }
        )

        XCTAssertEqual(
            deck.drawNext(
                repeatMode: .off,
                availableTrackIDs: [firstID, secondID, thirdID],
                currentTrackID: firstID
            ),
            MusicTrackReference(playlistID: playlistID, trackID: secondID)
        )
        XCTAssertEqual(
            deck.drawNext(
                repeatMode: .off,
                availableTrackIDs: [firstID, secondID, thirdID],
                currentTrackID: secondID
            )?.trackID,
            thirdID
        )
        XCTAssertNil(
            deck.drawNext(
                repeatMode: .off,
                availableTrackIDs: [firstID, secondID, thirdID],
                currentTrackID: thirdID
            )
        )
    }

    func testRepeatAllStartsNewCycleWithoutImmediateCurrentTrack() {
        var deck = ShuffleDeck(
            playlistID: playlistID,
            trackIDs: [firstID, secondID],
            excluding: firstID,
            shuffler: { $0 }
        )

        XCTAssertEqual(
            deck.drawNext(
                repeatMode: .all,
                availableTrackIDs: [firstID, secondID],
                currentTrackID: firstID
            )?.trackID,
            secondID
        )
        XCTAssertEqual(
            deck.drawNext(
                repeatMode: .all,
                availableTrackIDs: [firstID, secondID],
                currentTrackID: secondID,
                shuffler: { Array($0.reversed()) }
            )?.trackID,
            firstID
        )
    }

    func testRepeatAllNewCycleContainsEveryTrackWithoutImmediateRepeat() throws {
        var deck = ShuffleDeck(
            playlistID: playlistID,
            trackIDs: [firstID, secondID, thirdID],
            excluding: firstID,
            shuffler: { $0 }
        )

        var current = firstID
        var drawn: [UUID] = []
        for _ in 0..<5 {
            let next = deck.drawNext(
                repeatMode: .all,
                availableTrackIDs: [firstID, secondID, thirdID],
                currentTrackID: current,
                shuffler: { $0 }
            )?.trackID
            let nextID = try XCTUnwrap(next)
            drawn.append(nextID)
            current = nextID
        }

        XCTAssertEqual(drawn, [secondID, thirdID, firstID, secondID, thirdID])
        XCTAssertFalse(zip([firstID] + drawn, drawn).contains { $0 == $1 })
    }

    func testRepeatAllRepeatsSingleTrackButRepeatOffDoesNot() {
        var repeatAllDeck = ShuffleDeck(
            playlistID: playlistID,
            trackIDs: [firstID],
            excluding: firstID
        )
        var repeatOffDeck = repeatAllDeck

        XCTAssertEqual(
            repeatAllDeck.drawNext(
                repeatMode: .all,
                availableTrackIDs: [firstID],
                currentTrackID: firstID
            )?.trackID,
            firstID
        )
        XCTAssertNil(
            repeatOffDeck.drawNext(
                repeatMode: .off,
                availableTrackIDs: [firstID],
                currentTrackID: firstID
            )
        )
    }

    func testRepeatOneReturnsCurrentTrackOnlyWhenItStillExists() {
        var deck = ShuffleDeck(
            playlistID: playlistID,
            trackIDs: [firstID, secondID],
            excluding: nil,
            shuffler: { $0 }
        )

        XCTAssertEqual(
            deck.drawNext(
                repeatMode: .one,
                availableTrackIDs: [firstID, secondID],
                currentTrackID: firstID
            )?.trackID,
            firstID
        )
        XCTAssertNil(
            deck.drawNext(
                repeatMode: .one,
                availableTrackIDs: [secondID],
                currentTrackID: firstID
            )
        )
        XCTAssertEqual(deck.remainingTrackIDs, [secondID])
    }

    func testReconcileRemovesUnavailableIDsWithoutReaddingConsumedTracks() {
        var deck = ShuffleDeck(
            playlistID: playlistID,
            trackIDs: [firstID, secondID, thirdID],
            shuffler: { $0 }
        )
        _ = deck.drawNext(
            repeatMode: .off,
            availableTrackIDs: [firstID, secondID, thirdID],
            currentTrackID: nil
        )

        deck.reconcile(availableTrackIDs: [firstID, thirdID])

        XCTAssertEqual(deck.remainingTrackIDs, [thirdID])
    }

    func testReconcileAddsNewTracksToCurrentCycleWithoutReaddingPlayedTracks() {
        var deck = ShuffleDeck(
            playlistID: playlistID,
            trackIDs: [firstID, secondID],
            excluding: firstID,
            shuffler: { $0 }
        )

        deck.reconcile(
            availableTrackIDs: [firstID, secondID, thirdID],
            currentTrackID: firstID,
            shuffler: { $0 }
        )

        XCTAssertEqual(deck.remainingTrackIDs, [secondID, thirdID])
    }

    func testRepeatOffCanDrawTrackAddedAfterOriginalCycleWasExhausted() {
        var deck = ShuffleDeck(
            playlistID: playlistID,
            trackIDs: [firstID, secondID],
            excluding: firstID,
            shuffler: { $0 }
        )
        XCTAssertEqual(
            deck.drawNext(
                repeatMode: .off,
                availableTrackIDs: [firstID, secondID],
                currentTrackID: firstID,
                shuffler: { $0 }
            )?.trackID,
            secondID
        )

        XCTAssertEqual(
            deck.drawNext(
                repeatMode: .off,
                availableTrackIDs: [firstID, secondID, thirdID],
                currentTrackID: secondID,
                shuffler: { $0 }
            )?.trackID,
            thirdID
        )
        XCTAssertNil(
            deck.drawNext(
                repeatMode: .off,
                availableTrackIDs: [firstID, secondID, thirdID],
                currentTrackID: thirdID,
                shuffler: { $0 }
            )
        )
    }

    func testDeletedTrackBecomesNewAgainIfItIsLaterReadded() {
        var deck = ShuffleDeck(
            playlistID: playlistID,
            trackIDs: [firstID, secondID],
            excluding: firstID,
            shuffler: { $0 }
        )
        _ = deck.drawNext(
            repeatMode: .off,
            availableTrackIDs: [firstID, secondID],
            currentTrackID: firstID,
            shuffler: { $0 }
        )
        deck.reconcile(
            availableTrackIDs: [firstID],
            currentTrackID: firstID,
            shuffler: { $0 }
        )

        XCTAssertEqual(
            deck.drawNext(
                repeatMode: .off,
                availableTrackIDs: [firstID, secondID],
                currentTrackID: firstID,
                shuffler: { $0 }
            )?.trackID,
            secondID
        )
    }

    func testMalformedShufflerCannotDuplicateOrDropCandidates() {
        let deck = ShuffleDeck(
            playlistID: playlistID,
            trackIDs: [firstID, secondID, thirdID],
            shuffler: { _ in [secondID, secondID] }
        )

        XCTAssertEqual(deck.remainingTrackIDs, [secondID, firstID, thirdID])
    }
}
