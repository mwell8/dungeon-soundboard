import Foundation

struct LegacyMigrationResult: Equatable {
    var musicPlaylists: [Playlist]
    var effectPlaylists: [EffectPlaylist]
    var selectedMusicPlaylistID: UUID?
    var selectedEffectPlaylistID: UUID?
}

enum PlaylistMigration {
    static func migrateLegacyPlaylists(
        _ legacyPlaylists: [Playlist],
        legacySelectedID: UUID?,
        defaultMusicPlaylistName: String,
        defaultSFXPlaylistName: String
    ) -> LegacyMigrationResult {
        var migratedMusicPlaylists: [Playlist] = []
        var collectedEffects: [Track] = []

        for playlist in legacyPlaylists {
            let musicTracks = playlist.tracks.filter { $0.role == .music }
            let effectTracks = playlist.tracks.filter { $0.role == .effect }

            migratedMusicPlaylists.append(
                Playlist(id: playlist.id, name: playlist.name, tracks: musicTracks)
            )
            collectedEffects.append(contentsOf: effectTracks)
        }

        if migratedMusicPlaylists.isEmpty {
            migratedMusicPlaylists = [Playlist(name: defaultMusicPlaylistName)]
        }

        let deduplicatedEffects = deduplicateTracksByPath(collectedEffects)
        let effectPlaylist = EffectPlaylist(name: defaultSFXPlaylistName, effects: deduplicatedEffects)
        let selectedMusicPlaylistID: UUID?
        if let legacySelectedID,
           migratedMusicPlaylists.contains(where: { $0.id == legacySelectedID }) {
            selectedMusicPlaylistID = legacySelectedID
        } else {
            selectedMusicPlaylistID = migratedMusicPlaylists.first?.id
        }

        return LegacyMigrationResult(
            musicPlaylists: migratedMusicPlaylists,
            effectPlaylists: [effectPlaylist],
            selectedMusicPlaylistID: selectedMusicPlaylistID,
            selectedEffectPlaylistID: effectPlaylist.id
        )
    }

    static func deduplicateTracksByPath(_ tracks: [Track]) -> [Track] {
        var seen = Set<String>()
        var result: [Track] = []

        for track in tracks {
            let normalizedPath = track.path
                .trimmingCharacters(in: .whitespacesAndNewlines)
                .lowercased()
            guard !normalizedPath.isEmpty else { continue }
            if seen.insert(normalizedPath).inserted {
                result.append(track)
            }
        }

        return result
    }
}
