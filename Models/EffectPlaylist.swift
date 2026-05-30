import Foundation

/// Отдельный плейлист эффектов (SFX), независимый от музыкальных плейлистов.
struct EffectPlaylist: Identifiable, Codable, Equatable {
    let id: UUID
    var name: String
    var effects: [Track]

    init(id: UUID = UUID(), name: String, effects: [Track] = []) {
        self.id = id
        self.name = name
        self.effects = effects
    }
}
