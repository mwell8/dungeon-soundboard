import Foundation

enum HotkeyModifier: String, Codable, CaseIterable {
    case none
    case shift
    case control

    var displayPrefix: String {
        switch self {
        case .none: return ""
        case .shift: return "Shift+"
        case .control: return "Ctrl+"
        }
    }
}

struct Hotkey: Codable, Hashable, Identifiable {
    var keyCode: UInt16
    var label: String
    var modifier: HotkeyModifier

    var id: String {
        "\(modifier.rawValue)-\(keyCode)"
    }

    var displayText: String {
        "\(modifier.displayPrefix)\(label)"
    }
}

extension Hotkey {
    static func normalized(
        keyCode: UInt16,
        charactersIgnoringModifiers: String?,
        characters: String?,
        isShiftPressed: Bool,
        isControlPressed: Bool,
        isOptionPressed: Bool,
        isCommandPressed: Bool
    ) -> Hotkey? {
        let normalizedKeyCode = normalizedKeyCode(keyCode)
        guard !isOptionPressed, !isCommandPressed, !(isShiftPressed && isControlPressed) else {
            return nil
        }

        let modifier: HotkeyModifier = isControlPressed ? .control : (isShiftPressed ? .shift : .none)
        guard let label = label(for: normalizedKeyCode, charactersIgnoringModifiers: charactersIgnoringModifiers, characters: characters),
              !label.isEmpty else {
            return nil
        }
        return Hotkey(keyCode: normalizedKeyCode, label: label, modifier: modifier)
    }

    private static func normalizedKeyCode(_ keyCode: UInt16) -> UInt16 {
        if keyCode == 117 {
            return 51
        }
        return keyCode
    }

    private static func label(for keyCode: UInt16, charactersIgnoringModifiers: String?, characters: String?) -> String? {
        switch keyCode {
        case 24:
            return "+"
        case 27:
            return "-"
        case 36:
            return "Return"
        case 48:
            return "Tab"
        case 49:
            return "Space"
        case 51:
            return "Delete"
        case 53:
            return "Esc"
        case 123:
            return "Left"
        case 124:
            return "Right"
        case 125:
            return "Down"
        case 126:
            return "Up"
        default:
            let raw = charactersIgnoringModifiers ?? characters ?? ""
            return raw.uppercased()
        }
    }
}

enum HotkeyAction: Codable, Hashable, Identifiable {
    case stopAll
    case stopEffects
    case playPause
    case musicVolumeUp
    case musicVolumeDown
    case effectsVolumeUp
    case effectsVolumeDown
    case playMusicTrack(playlistID: UUID, trackID: UUID)
    case playEffect(playlistID: UUID, trackID: UUID)

    var id: String {
        switch self {
        case .stopAll:
            return "system.stopAll"
        case .stopEffects:
            return "system.stopEffects"
        case .playPause:
            return "system.playPause"
        case .musicVolumeUp:
            return "system.musicVolumeUp"
        case .musicVolumeDown:
            return "system.musicVolumeDown"
        case .effectsVolumeUp:
            return "system.effectsVolumeUp"
        case .effectsVolumeDown:
            return "system.effectsVolumeDown"
        case .playMusicTrack(let playlistID, let trackID):
            return "music.\(playlistID.uuidString).\(trackID.uuidString)"
        case .playEffect(let playlistID, let trackID):
            return "effect.\(playlistID.uuidString).\(trackID.uuidString)"
        }
    }

    var isSystemAction: Bool {
        switch self {
        case .stopAll, .stopEffects, .playPause, .musicVolumeUp, .musicVolumeDown, .effectsVolumeUp, .effectsVolumeDown:
            return true
        case .playMusicTrack, .playEffect:
            return false
        }
    }

    static let systemActions: [HotkeyAction] = [
        .stopEffects,
        .stopAll,
        .playPause,
        .musicVolumeUp,
        .musicVolumeDown,
        .effectsVolumeUp,
        .effectsVolumeDown
    ]
}

struct HotkeyBinding: Codable, Equatable, Identifiable {
    var action: HotkeyAction
    var hotkey: Hotkey

    var id: String {
        action.id
    }
}

struct HotkeyConflict: Equatable {
    var existingAction: HotkeyAction
    var hotkey: Hotkey
}

struct HotkeyConfiguration: Codable, Equatable {
    var bindings: [HotkeyBinding]

    init(bindings: [HotkeyBinding] = []) {
        self.bindings = bindings
    }

    static var defaults: HotkeyConfiguration {
        HotkeyConfiguration(bindings: [
            HotkeyBinding(action: .stopEffects, hotkey: deleteHotkey),
            HotkeyBinding(action: .playPause, hotkey: Hotkey(keyCode: 49, label: "Space", modifier: .none)),
            HotkeyBinding(action: .musicVolumeUp, hotkey: Hotkey(keyCode: 24, label: "+", modifier: .none)),
            HotkeyBinding(action: .musicVolumeDown, hotkey: Hotkey(keyCode: 27, label: "-", modifier: .none)),
            HotkeyBinding(action: .effectsVolumeUp, hotkey: Hotkey(keyCode: 24, label: "+", modifier: .shift)),
            HotkeyBinding(action: .effectsVolumeDown, hotkey: Hotkey(keyCode: 27, label: "-", modifier: .shift))
        ])
    }

    static let deleteHotkey = Hotkey(keyCode: 51, label: "Delete", modifier: .none)

    func hotkey(for action: HotkeyAction) -> Hotkey? {
        bindings.first { $0.action == action }?.hotkey
    }

    func action(for hotkey: Hotkey) -> HotkeyAction? {
        bindings.first { $0.hotkey == hotkey }?.action
    }

    func conflict(for hotkey: Hotkey, excluding action: HotkeyAction) -> HotkeyConflict? {
        guard let binding = bindings.first(where: { $0.hotkey == hotkey && $0.action != action }) else {
            return nil
        }
        return HotkeyConflict(existingAction: binding.action, hotkey: hotkey)
    }

    mutating func assign(_ hotkey: Hotkey, to action: HotkeyAction, resolvingConflicts: Bool) -> HotkeyConflict? {
        if let conflict = conflict(for: hotkey, excluding: action), !resolvingConflicts {
            return conflict
        }

        bindings.removeAll { binding in
            binding.action == action || (resolvingConflicts && binding.hotkey == hotkey)
        }
        bindings.append(HotkeyBinding(action: action, hotkey: hotkey))
        return nil
    }

    mutating func clear(_ action: HotkeyAction) {
        bindings.removeAll { $0.action == action }
    }

    mutating func migrateLegacyDeleteStopAllDefault() {
        guard hotkey(for: .stopAll) == Self.deleteHotkey,
              hotkey(for: .stopEffects) == nil else {
            return
        }
        clear(.stopAll)
        _ = assign(Self.deleteHotkey, to: .stopEffects, resolvingConflicts: true)
    }

    mutating func removeMissingTrackBindings(
        musicPlaylistIDs: Set<UUID>,
        musicTrackIDs: Set<UUID>,
        effectPlaylistIDs: Set<UUID>,
        effectTrackIDs: Set<UUID>
    ) {
        bindings.removeAll { binding in
            switch binding.action {
            case .playMusicTrack(let playlistID, let trackID):
                return !musicPlaylistIDs.contains(playlistID) || !musicTrackIDs.contains(trackID)
            case .playEffect(let playlistID, let trackID):
                return !effectPlaylistIDs.contains(playlistID) || !effectTrackIDs.contains(trackID)
            case .stopAll, .stopEffects, .playPause, .musicVolumeUp, .musicVolumeDown, .effectsVolumeUp, .effectsVolumeDown:
                return false
            }
        }
    }
}
