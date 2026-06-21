import XCTest
@testable import DungeonSoundboardCore

final class HotkeyTests: XCTestCase {
    func testDefaultHotkeysContainExpectedSystemActions() {
        let configuration = HotkeyConfiguration.defaults

        XCTAssertEqual(configuration.hotkey(for: .stopEffects)?.displayText, "Delete")
        XCTAssertNil(configuration.hotkey(for: .stopAll))
        XCTAssertEqual(configuration.hotkey(for: .playPause)?.displayText, "Space")
        XCTAssertEqual(configuration.hotkey(for: .effectsVolumeUp)?.displayText, "Shift++")
        XCTAssertEqual(configuration.hotkey(for: .effectsVolumeDown)?.displayText, "Shift+-")
    }

    func testAssignReportsConflictUnlessResolving() {
        let hotkey = Hotkey(keyCode: 12, label: "Q", modifier: .none)
        var configuration = HotkeyConfiguration(bindings: [
            HotkeyBinding(action: .playPause, hotkey: hotkey)
        ])

        let conflict = configuration.assign(hotkey, to: .stopAll, resolvingConflicts: false)

        XCTAssertEqual(conflict?.existingAction, .playPause)
        XCTAssertEqual(configuration.hotkey(for: .playPause), hotkey)

        let resolved = configuration.assign(hotkey, to: .stopAll, resolvingConflicts: true)

        XCTAssertNil(resolved)
        XCTAssertNil(configuration.hotkey(for: .playPause))
        XCTAssertEqual(configuration.hotkey(for: .stopAll), hotkey)
    }

    func testConfigurationSurvivesCoding() throws {
        let action = HotkeyAction.playMusicTrack(
            playlistID: UUID(uuidString: "00000000-0000-0000-0000-000000000001")!,
            trackID: UUID(uuidString: "00000000-0000-0000-0000-000000000002")!
        )
        let configuration = HotkeyConfiguration(bindings: [
            HotkeyBinding(action: action, hotkey: Hotkey(keyCode: 0, label: "A", modifier: .control))
        ])

        let data = try JSONEncoder().encode(configuration)
        let decoded = try JSONDecoder().decode(HotkeyConfiguration.self, from: data)

        XCTAssertEqual(decoded, configuration)
    }

    func testLegacyDeleteStopAllDefaultMigratesToStopEffects() {
        var configuration = HotkeyConfiguration(bindings: [
            HotkeyBinding(action: .stopAll, hotkey: HotkeyConfiguration.deleteHotkey),
            HotkeyBinding(action: .playPause, hotkey: Hotkey(keyCode: 49, label: "Space", modifier: .none))
        ])

        configuration.migrateLegacyDeleteStopAllDefault()

        XCTAssertNil(configuration.hotkey(for: .stopAll))
        XCTAssertEqual(configuration.hotkey(for: .stopEffects), HotkeyConfiguration.deleteHotkey)
        XCTAssertEqual(configuration.hotkey(for: .playPause)?.displayText, "Space")
    }

    func testHotkeyNormalizationAllowsPlainShiftAndControl() {
        XCTAssertEqual(
            Hotkey.normalized(
                keyCode: 0,
                charactersIgnoringModifiers: "a",
                characters: "a",
                isShiftPressed: false,
                isControlPressed: false,
                isOptionPressed: false,
                isCommandPressed: false
            ),
            Hotkey(keyCode: 0, label: "A", modifier: .none)
        )
        XCTAssertEqual(
            Hotkey.normalized(
                keyCode: 0,
                charactersIgnoringModifiers: "a",
                characters: "A",
                isShiftPressed: true,
                isControlPressed: false,
                isOptionPressed: false,
                isCommandPressed: false
            ),
            Hotkey(keyCode: 0, label: "A", modifier: .shift)
        )
        XCTAssertEqual(
            Hotkey.normalized(
                keyCode: 0,
                charactersIgnoringModifiers: "a",
                characters: "a",
                isShiftPressed: false,
                isControlPressed: true,
                isOptionPressed: false,
                isCommandPressed: false
            ),
            Hotkey(keyCode: 0, label: "A", modifier: .control)
        )
    }

    func testHotkeyNormalizationRejectsUnsupportedModifiers() {
        XCTAssertNil(
            Hotkey.normalized(
                keyCode: 0,
                charactersIgnoringModifiers: "a",
                characters: "a",
                isShiftPressed: false,
                isControlPressed: false,
                isOptionPressed: true,
                isCommandPressed: false
            )
        )
        XCTAssertNil(
            Hotkey.normalized(
                keyCode: 0,
                charactersIgnoringModifiers: "a",
                characters: "a",
                isShiftPressed: false,
                isControlPressed: false,
                isOptionPressed: false,
                isCommandPressed: true
            )
        )
        XCTAssertNil(
            Hotkey.normalized(
                keyCode: 0,
                charactersIgnoringModifiers: "a",
                characters: "a",
                isShiftPressed: true,
                isControlPressed: true,
                isOptionPressed: false,
                isCommandPressed: false
            )
        )
    }

    func testHotkeyNormalizationTreatsForwardDeleteAsDelete() {
        XCTAssertEqual(
            Hotkey.normalized(
                keyCode: 117,
                charactersIgnoringModifiers: nil,
                characters: nil,
                isShiftPressed: false,
                isControlPressed: false,
                isOptionPressed: false,
                isCommandPressed: false
            ),
            Hotkey(keyCode: 51, label: "Delete", modifier: .none)
        )
    }

    func testMissingTrackBindingsAreRemoved() {
        let existingPlaylistID = UUID()
        let missingPlaylistID = UUID()
        let existingTrackID = UUID()
        let missingTrackID = UUID()
        var configuration = HotkeyConfiguration(bindings: [
            HotkeyBinding(
                action: .playMusicTrack(playlistID: existingPlaylistID, trackID: existingTrackID),
                hotkey: Hotkey(keyCode: 0, label: "A", modifier: .none)
            ),
            HotkeyBinding(
                action: .playMusicTrack(playlistID: missingPlaylistID, trackID: missingTrackID),
                hotkey: Hotkey(keyCode: 1, label: "S", modifier: .none)
            )
        ])

        configuration.removeMissingTrackBindings(
            musicPlaylistIDs: [existingPlaylistID],
            musicTrackIDs: [existingTrackID],
            effectPlaylistIDs: [],
            effectTrackIDs: []
        )

        XCTAssertEqual(configuration.bindings.count, 1)
        XCTAssertEqual(configuration.bindings.first?.action, .playMusicTrack(playlistID: existingPlaylistID, trackID: existingTrackID))
    }
}
