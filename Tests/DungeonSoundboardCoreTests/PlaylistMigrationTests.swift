import XCTest
@testable import DungeonSoundboardCore

final class PlaylistMigrationTests: XCTestCase {
    func testMigrationSplitsMusicAndEffectsAndPreservesSelectedPlaylistWhenPossible() {
        let musicID = UUID()
        let selected = musicID
        let effectID = UUID()

        let legacy = [
            Playlist(
                id: musicID,
                name: "Scene 1",
                tracks: [
                    Track(id: UUID(), title: "Music A", path: "/tmp/a.mp3", role: .music),
                    Track(id: effectID, title: "Thunder", path: "/tmp/thunder.wav", role: .effect)
                ]
            ),
            Playlist(
                id: UUID(),
                name: "Scene 2",
                tracks: [
                    Track(id: UUID(), title: "Music B", path: "/tmp/b.mp3", role: .music),
                    Track(id: UUID(), title: "Thunder Copy", path: "/tmp/thunder.wav", role: .effect)
                ]
            )
        ]

        let result = PlaylistMigration.migrateLegacyPlaylists(
            legacy,
            legacySelectedID: selected,
            defaultMusicPlaylistName: "Main Playlist",
            defaultSFXPlaylistName: "SFX Master"
        )

        XCTAssertEqual(result.musicPlaylists.count, 2)
        XCTAssertEqual(result.musicPlaylists[0].tracks.count, 1)
        XCTAssertEqual(result.musicPlaylists[1].tracks.count, 1)
        XCTAssertEqual(result.effectPlaylists.count, 1)
        XCTAssertEqual(result.effectPlaylists[0].effects.count, 1, "Effects must be deduplicated by path")
        XCTAssertEqual(result.selectedMusicPlaylistID, selected)
        XCTAssertEqual(result.selectedEffectPlaylistID, result.effectPlaylists[0].id)
    }

    func testMigrationCreatesDefaultsWhenLegacyIsEmpty() {
        let result = PlaylistMigration.migrateLegacyPlaylists(
            [],
            legacySelectedID: nil,
            defaultMusicPlaylistName: "Main Playlist",
            defaultSFXPlaylistName: "SFX Master"
        )

        XCTAssertEqual(result.musicPlaylists.count, 1)
        XCTAssertEqual(result.musicPlaylists[0].name, "Main Playlist")
        XCTAssertEqual(result.effectPlaylists.count, 1)
        XCTAssertEqual(result.effectPlaylists[0].name, "SFX Master")
        XCTAssertEqual(result.selectedMusicPlaylistID, result.musicPlaylists[0].id)
    }
}
