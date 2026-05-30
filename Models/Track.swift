import Foundation

enum TrackRole: String, Codable, CaseIterable {
    case music
    case effect
}

/// Одна музыкальная дорожка.
/// Теперь кроме path храним еще bookmarkData,
/// чтобы приложение могло получить доступ к файлу после перезапуска.
struct Track: Identifiable, Codable, Equatable {
    let id: UUID
    var title: String
    var path: String
    var role: TrackRole
    var bookmarkData: Data?

    init(
        id: UUID = UUID(),
        title: String,
        path: String,
        role: TrackRole = .music,
        bookmarkData: Data? = nil
    ) {
        self.id = id
        self.title = title
        self.path = path
        self.role = role
        self.bookmarkData = bookmarkData
    }

    /// Обычный URL по пути.
    /// Полезен как fallback, если bookmarkData еще нет.
    var url: URL {
        URL(fileURLWithPath: path)
    }

    private enum CodingKeys: String, CodingKey {
        case id
        case title
        case path
        case role
        case bookmarkData
    }

    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        id = try container.decode(UUID.self, forKey: .id)
        title = try container.decode(String.self, forKey: .title)
        path = try container.decode(String.self, forKey: .path)
        role = try container.decodeIfPresent(TrackRole.self, forKey: .role) ?? .music
        bookmarkData = try container.decodeIfPresent(Data.self, forKey: .bookmarkData)
    }

    func encode(to encoder: Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(id, forKey: .id)
        try container.encode(title, forKey: .title)
        try container.encode(path, forKey: .path)
        try container.encode(role, forKey: .role)
        try container.encodeIfPresent(bookmarkData, forKey: .bookmarkData)
    }
}
