import Foundation

/// Режимы повтора.
/// - off: без повтора
/// - one: повтор текущего трека
/// - all: повтор всего плейлиста
enum RepeatMode: String, CaseIterable, Codable, Identifiable {
    case off = "off"
    case one = "one"
    case all = "all"

    var id: String { rawValue }

    /// Стабильный локализационный ключ, не зависящий от сохраненного rawValue.
    var localizedKey: String {
        switch self {
        case .off:
            return "repeat.off"
        case .one:
            return "repeat.one"
        case .all:
            return "repeat.all"
        }
    }

    static func fromStoredValue(_ value: String) -> RepeatMode? {
        if let mode = RepeatMode(rawValue: value) {
            return mode
        }

        // Backward compatibility for pre-localization persisted raw values.
        switch value {
        case "Без повтора":
            return .off
        case "Повтор трека":
            return .one
        case "Повтор плейлиста":
            return .all
        default:
            return nil
        }
    }
}
