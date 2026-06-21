import XCTest
@testable import DungeonSoundboardCore

final class PlayerDefaultsKeysTests: XCTestCase {
    func testMigrationCriticalKeysAreStable() {
        XCTAssertEqual(PlayerDefaultsKeys.legacyPlaylists, "macos_dungeon_soundboard_playlists")
        XCTAssertEqual(PlayerDefaultsKeys.migrationCompleted, "macos_dungeon_soundboard_migration_v2_completed")
        XCTAssertEqual(PlayerDefaultsKeys.legacySelectedPlaylistID, "macos_dungeon_soundboard_selected_playlist_id")
    }

    func testCurrentPlaybackPreferenceKeysAreStable() {
        XCTAssertEqual(PlayerDefaultsKeys.musicPlaylists, "macos_dungeon_soundboard_music_playlists")
        XCTAssertEqual(PlayerDefaultsKeys.effectPlaylists, "macos_dungeon_soundboard_effect_playlists")
        XCTAssertEqual(PlayerDefaultsKeys.repeatMode, "macos_dungeon_soundboard_repeat_mode")
        XCTAssertEqual(PlayerDefaultsKeys.shuffleEnabled, "macos_dungeon_soundboard_shuffle_enabled")
        XCTAssertEqual(PlayerDefaultsKeys.selectedMusicPlaylistID, "macos_dungeon_soundboard_selected_music_playlist_id")
        XCTAssertEqual(PlayerDefaultsKeys.selectedEffectPlaylistID, "macos_dungeon_soundboard_selected_effect_playlist_id")
        XCTAssertEqual(PlayerDefaultsKeys.theme, "macos_dungeon_soundboard_theme")
        XCTAssertEqual(PlayerDefaultsKeys.customThemePresets, "macos_dungeon_soundboard_custom_theme_presets")
        XCTAssertEqual(PlayerDefaultsKeys.hotkeyBindings, "macos_dungeon_soundboard_hotkey_bindings")
    }
}
