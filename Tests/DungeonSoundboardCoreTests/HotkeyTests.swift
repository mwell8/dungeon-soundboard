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

    func testPhysicalHotkeyIdentityIgnoresKeyboardLayoutLabel() {
        let latin = Hotkey(keyCode: 0, label: "A", modifier: .control)
        let cyrillic = Hotkey(keyCode: 0, label: "Ф", modifier: .control)

        XCTAssertEqual(latin, cyrillic)
        XCTAssertEqual(Set([latin, cyrillic]).count, 1)
    }

    func testConflictDetectionUsesPhysicalKeyInsteadOfLabel() {
        let latin = Hotkey(keyCode: 0, label: "A", modifier: .none)
        let cyrillic = Hotkey(keyCode: 0, label: "Ф", modifier: .none)
        var configuration = HotkeyConfiguration(bindings: [
            HotkeyBinding(action: .playPause, hotkey: latin)
        ])

        let conflict = configuration.assign(cyrillic, to: .stopAll, resolvingConflicts: false)

        XCTAssertEqual(conflict?.existingAction, .playPause)
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

    func testHotkeyNormalizationRejectsTabForNativeFocusTraversal() {
        XCTAssertNil(
            Hotkey.normalized(
                keyCode: 48,
                charactersIgnoringModifiers: "\t",
                characters: "\t",
                isShiftPressed: false,
                isControlPressed: false,
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

    func testMoveTransferRetargetsMusicBindingAndKeepsHotkey() {
        let sourcePlaylistID = UUID()
        let destinationPlaylistID = UUID()
        let trackID = UUID()
        let hotkey = Hotkey(keyCode: 12, label: "Q", modifier: .control)
        var configuration = HotkeyConfiguration(bindings: [
            HotkeyBinding(
                action: .playMusicTrack(playlistID: sourcePlaylistID, trackID: trackID),
                hotkey: hotkey
            )
        ])
        let result = TrackTransferResult(
            operation: .move,
            role: .music,
            sourcePlaylistID: sourcePlaylistID,
            destinationPlaylistID: destinationPlaylistID,
            transferred: [TrackTransferRecord(sourceTrackID: trackID, destinationTrackID: trackID)],
            duplicateTrackIDs: [],
            missingTrackIDs: []
        )

        XCTAssertEqual(configuration.applyTransferResult(result), 1)
        XCTAssertNil(
            configuration.hotkey(
                for: .playMusicTrack(playlistID: sourcePlaylistID, trackID: trackID)
            )
        )
        XCTAssertEqual(
            configuration.hotkey(
                for: .playMusicTrack(playlistID: destinationPlaylistID, trackID: trackID)
            ),
            hotkey
        )
    }

    func testCopyTransferDoesNotRetargetOriginalBinding() {
        let sourcePlaylistID = UUID()
        let destinationPlaylistID = UUID()
        let sourceTrackID = UUID()
        let copiedTrackID = UUID()
        let action = HotkeyAction.playEffect(playlistID: sourcePlaylistID, trackID: sourceTrackID)
        let hotkey = Hotkey(keyCode: 13, label: "W", modifier: .none)
        var configuration = HotkeyConfiguration(bindings: [
            HotkeyBinding(action: action, hotkey: hotkey)
        ])
        let result = TrackTransferResult(
            operation: .copy,
            role: .effect,
            sourcePlaylistID: sourcePlaylistID,
            destinationPlaylistID: destinationPlaylistID,
            transferred: [
                TrackTransferRecord(sourceTrackID: sourceTrackID, destinationTrackID: copiedTrackID)
            ],
            duplicateTrackIDs: [],
            missingTrackIDs: []
        )

        XCTAssertEqual(configuration.applyTransferResult(result), 0)
        XCTAssertEqual(configuration.hotkey(for: action), hotkey)
    }

    func testReconcileRetargetsUniquelyMovedTrackAndRemovesMissingTrack() {
        let oldPlaylistID = UUID()
        let destinationPlaylistID = UUID()
        let movedTrackID = UUID()
        let missingTrackID = UUID()
        let movedHotkey = Hotkey(keyCode: 14, label: "E", modifier: .none)
        var configuration = HotkeyConfiguration(bindings: [
            HotkeyBinding(
                action: .playMusicTrack(playlistID: oldPlaylistID, trackID: movedTrackID),
                hotkey: movedHotkey
            ),
            HotkeyBinding(
                action: .playMusicTrack(playlistID: oldPlaylistID, trackID: missingTrackID),
                hotkey: Hotkey(keyCode: 15, label: "R", modifier: .none)
            ),
            HotkeyBinding(
                action: .playPause,
                hotkey: Hotkey(keyCode: 49, label: "Space", modifier: .none)
            )
        ])
        let musicPlaylists = [
            Playlist(
                id: destinationPlaylistID,
                name: "Destination",
                tracks: [Track(id: movedTrackID, title: "Moved", path: "/tmp/moved.mp3")]
            )
        ]

        let result = configuration.reconcileTrackBindings(
            musicPlaylists: musicPlaylists,
            effectPlaylists: []
        )

        XCTAssertEqual(result, HotkeyReconciliationResult(retargetedCount: 1, removedCount: 1))
        XCTAssertEqual(
            configuration.hotkey(
                for: .playMusicTrack(playlistID: destinationPlaylistID, trackID: movedTrackID)
            ),
            movedHotkey
        )
        XCTAssertNotNil(configuration.hotkey(for: .playPause))
    }

    func testReconcileKeepsValidExactPairWhenTrackIDHasMultipleOwners() {
        let firstPlaylistID = UUID()
        let secondPlaylistID = UUID()
        let trackID = UUID()
        let hotkey = Hotkey(keyCode: 16, label: "Y", modifier: .none)
        var configuration = HotkeyConfiguration(bindings: [
            HotkeyBinding(
                action: .playMusicTrack(playlistID: firstPlaylistID, trackID: trackID),
                hotkey: hotkey
            )
        ])
        let duplicatedTrack = Track(id: trackID, title: "Duplicate ID", path: "/tmp/a.mp3")

        let result = configuration.reconcileTrackBindings(
            musicPlaylists: [
                Playlist(id: firstPlaylistID, name: "First", tracks: [duplicatedTrack]),
                Playlist(id: secondPlaylistID, name: "Second", tracks: [duplicatedTrack])
            ],
            effectPlaylists: []
        )

        XCTAssertFalse(result.didChange)
        XCTAssertEqual(
            configuration.hotkey(
                for: .playMusicTrack(playlistID: firstPlaylistID, trackID: trackID)
            ),
            hotkey
        )
    }

    func testReconcileRemovesAmbiguousBindingWhenOriginalPairIsGone() {
        let oldPlaylistID = UUID()
        let firstOwnerID = UUID()
        let secondOwnerID = UUID()
        let trackID = UUID()
        var configuration = HotkeyConfiguration(bindings: [
            HotkeyBinding(
                action: .playEffect(playlistID: oldPlaylistID, trackID: trackID),
                hotkey: Hotkey(keyCode: 17, label: "T", modifier: .none)
            )
        ])
        let duplicatedTrack = Track(id: trackID, title: "Duplicate ID", path: "/tmp/effect.wav", role: .effect)

        let result = configuration.reconcileTrackBindings(
            musicPlaylists: [],
            effectPlaylists: [
                EffectPlaylist(id: firstOwnerID, name: "First", effects: [duplicatedTrack]),
                EffectPlaylist(id: secondOwnerID, name: "Second", effects: [duplicatedTrack])
            ]
        )

        XCTAssertEqual(result.removedCount, 1)
        XCTAssertTrue(configuration.bindings.isEmpty)
    }
}
