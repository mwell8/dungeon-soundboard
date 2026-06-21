import AppKit
import Combine
import Foundation

final class HotkeyStore: ObservableObject {
    @Published private(set) var configuration: HotkeyConfiguration
    @Published var captureAction: HotkeyAction?

    private let defaults: UserDefaults

    init(defaults: UserDefaults = .standard) {
        self.defaults = defaults
        self.configuration = Self.loadConfiguration(defaults: defaults)
    }

    var bindings: [HotkeyBinding] {
        configuration.bindings
    }

    func beginCapture(for action: HotkeyAction) {
        captureAction = action
    }

    func cancelCapture() {
        captureAction = nil
    }

    func hotkey(for action: HotkeyAction) -> Hotkey? {
        configuration.hotkey(for: action)
    }

    func action(for hotkey: Hotkey) -> HotkeyAction? {
        configuration.action(for: hotkey)
    }

    func assign(_ hotkey: Hotkey, to action: HotkeyAction, resolvingConflicts: Bool) -> HotkeyConflict? {
        var updated = configuration
        let conflict = updated.assign(hotkey, to: action, resolvingConflicts: resolvingConflicts)
        guard conflict == nil else { return conflict }
        configuration = updated
        save()
        return nil
    }

    func clear(_ action: HotkeyAction) {
        var updated = configuration
        updated.clear(action)
        configuration = updated
        save()
    }

    func restoreDefaults() {
        configuration = .defaults
        save()
    }

    func purgeMissingTrackBindings(musicPlaylists: [Playlist], effectPlaylists: [EffectPlaylist]) {
        var updated = configuration
        updated.removeMissingTrackBindings(
            musicPlaylistIDs: Set(musicPlaylists.map(\.id)),
            musicTrackIDs: Set(musicPlaylists.flatMap { $0.tracks.map(\.id) }),
            effectPlaylistIDs: Set(effectPlaylists.map(\.id)),
            effectTrackIDs: Set(effectPlaylists.flatMap { $0.effects.map(\.id) })
        )
        guard updated != configuration else { return }
        configuration = updated
        save()
    }

    private func save() {
        guard let data = try? JSONEncoder().encode(configuration) else { return }
        defaults.set(data, forKey: PlayerDefaultsKeys.hotkeyBindings)
    }

    private static func loadConfiguration(defaults: UserDefaults) -> HotkeyConfiguration {
        guard let data = defaults.data(forKey: PlayerDefaultsKeys.hotkeyBindings),
              let decoded = try? JSONDecoder().decode(HotkeyConfiguration.self, from: data) else {
            return .defaults
        }
        var migrated = decoded
        migrated.migrateLegacyDeleteStopAllDefault()
        if migrated != decoded, let data = try? JSONEncoder().encode(migrated) {
            defaults.set(data, forKey: PlayerDefaultsKeys.hotkeyBindings)
        }
        return migrated
    }
}

extension Hotkey {
    static func from(event: NSEvent) -> Hotkey? {
        let flags = event.modifierFlags.intersection(.deviceIndependentFlagsMask)
        return normalized(
            keyCode: event.keyCode,
            charactersIgnoringModifiers: event.charactersIgnoringModifiers,
            characters: event.characters,
            isShiftPressed: flags.contains(.shift),
            isControlPressed: flags.contains(.control),
            isOptionPressed: flags.contains(.option),
            isCommandPressed: flags.contains(.command)
        )
    }
}
