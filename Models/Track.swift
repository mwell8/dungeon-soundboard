import Foundation

enum TrackRole: String, Codable, CaseIterable {
    case music
    case effect
}

/// Один музыкальный трек или SFX.
/// Кроме path храним bookmarkData, чтобы приложение могло получить доступ к файлу после перезапуска.
struct Track: Identifiable, Codable, Equatable {
    let id: UUID
    var title: String
    var path: String
    var role: TrackRole
    var bookmarkData: Data?
    var volumeMultiplier: Double {
        didSet {
            volumeMultiplier = Self.normalizedVolumeMultiplier(volumeMultiplier)
        }
    }

    init(
        id: UUID = UUID(),
        title: String,
        path: String,
        role: TrackRole = .music,
        bookmarkData: Data? = nil,
        volumeMultiplier: Double = Self.defaultVolumeMultiplier
    ) {
        self.id = id
        self.title = title
        self.path = path
        self.role = role
        self.bookmarkData = bookmarkData
        self.volumeMultiplier = Self.normalizedVolumeMultiplier(volumeMultiplier)
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
        case volumeMultiplier
    }

    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        id = try container.decode(UUID.self, forKey: .id)
        title = try container.decode(String.self, forKey: .title)
        path = try container.decode(String.self, forKey: .path)
        role = try container.decodeIfPresent(TrackRole.self, forKey: .role) ?? .music
        bookmarkData = try container.decodeIfPresent(Data.self, forKey: .bookmarkData)
        volumeMultiplier = Self.normalizedVolumeMultiplier(
            try container.decodeIfPresent(Double.self, forKey: .volumeMultiplier) ?? Self.defaultVolumeMultiplier
        )
    }

    func encode(to encoder: Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(id, forKey: .id)
        try container.encode(title, forKey: .title)
        try container.encode(path, forKey: .path)
        try container.encode(role, forKey: .role)
        try container.encodeIfPresent(bookmarkData, forKey: .bookmarkData)
        try container.encode(volumeMultiplier, forKey: .volumeMultiplier)
    }
}

extension Track {
    static let defaultVolumeMultiplier = 1.0
    static let minimumVolumeMultiplier = 0.0
    static let maximumVolumeMultiplier = 2.0

    static func normalizedTitle(_ value: String, fallback: String) -> String {
        let trimmed = value.trimmingCharacters(in: .whitespacesAndNewlines)
        return trimmed.isEmpty ? fallback : trimmed
    }

    static func normalizedVolumeMultiplier(_ value: Double) -> Double {
        guard value.isFinite else { return defaultVolumeMultiplier }
        return min(max(value, minimumVolumeMultiplier), maximumVolumeMultiplier)
    }

    static func outputVolume(
        masterVolume: Double,
        trackMultiplier: Double,
        duckingMultiplier: Double = 1.0
    ) -> Double {
        let normalizedMaster = min(max(masterVolume, 0), 1)
        let normalizedDucking = min(max(duckingMultiplier, 0), 1)
        let output = normalizedMaster * normalizedVolumeMultiplier(trackMultiplier) * normalizedDucking
        return min(max(output, 0), 1)
    }

    func outputVolume(masterVolume: Double, duckingMultiplier: Double = 1.0) -> Double {
        Self.outputVolume(
            masterVolume: masterVolume,
            trackMultiplier: volumeMultiplier,
            duckingMultiplier: duckingMultiplier
        )
    }

    @discardableResult
    static func moveTrack(in tracks: inout [Track], draggedID: UUID, to targetID: UUID) -> Bool {
        guard draggedID != targetID,
              let sourceIndex = tracks.firstIndex(where: { $0.id == draggedID }),
              let targetIndex = tracks.firstIndex(where: { $0.id == targetID }) else {
            return false
        }

        let track = tracks.remove(at: sourceIndex)
        let destinationIndex = min(targetIndex, tracks.count)
        tracks.insert(track, at: destinationIndex)
        return true
    }
}
