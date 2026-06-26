import XCTest
@testable import DungeonSoundboardCore

final class TrackTests: XCTestCase {
    func testDecodingLegacyTrackDefaultsVolumeMultiplierToOne() throws {
        let json = """
        {
          "id": "00000000-0000-0000-0000-000000000001",
          "title": "Rain",
          "path": "/tmp/rain.mp3",
          "role": "music"
        }
        """
        let track = try JSONDecoder().decode(Track.self, from: Data(json.utf8))

        XCTAssertEqual(track.volumeMultiplier, 1.0)
    }

    func testVolumeMultiplierIsClamped() {
        var quiet = Track(title: "Quiet", path: "/tmp/quiet.mp3", volumeMultiplier: -2)
        XCTAssertEqual(quiet.volumeMultiplier, 0)

        quiet.volumeMultiplier = 3
        XCTAssertEqual(quiet.volumeMultiplier, 2)

        let invalid = Track(title: "Invalid", path: "/tmp/invalid.mp3", volumeMultiplier: .nan)
        XCTAssertEqual(invalid.volumeMultiplier, 1)
    }

    func testNormalizedTitleTrimsAndFallsBack() {
        XCTAssertEqual(Track.normalizedTitle("  Tavern Rain  ", fallback: "Rain"), "Tavern Rain")
        XCTAssertEqual(Track.normalizedTitle("   ", fallback: "Rain"), "Rain")
    }

    func testOutputVolumeUsesMasterTrackMultiplierAndDucking() {
        XCTAssertEqual(
            Track.outputVolume(masterVolume: 0.5, trackMultiplier: 1.5, duckingMultiplier: 0.5),
            0.375,
            accuracy: 0.0001
        )
        XCTAssertEqual(
            Track.outputVolume(masterVolume: 1.0, trackMultiplier: 2.0),
            1.0,
            accuracy: 0.0001
        )
        XCTAssertEqual(
            Track.outputVolume(masterVolume: 0.2, trackMultiplier: 2.0),
            0.4,
            accuracy: 0.0001
        )
    }

    func testMoveTrackReordersIDs() {
        let firstID = UUID()
        let secondID = UUID()
        let thirdID = UUID()
        var tracks = [
            Track(id: firstID, title: "First", path: "/tmp/first.mp3"),
            Track(id: secondID, title: "Second", path: "/tmp/second.mp3"),
            Track(id: thirdID, title: "Third", path: "/tmp/third.mp3")
        ]

        XCTAssertTrue(Track.moveTrack(in: &tracks, draggedID: firstID, to: thirdID))
        XCTAssertEqual(tracks.map(\.id), [secondID, thirdID, firstID])

        XCTAssertTrue(Track.moveTrack(in: &tracks, draggedID: firstID, to: secondID))
        XCTAssertEqual(tracks.map(\.id), [firstID, secondID, thirdID])
    }
}
