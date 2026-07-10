import Foundation

enum TrackTransferOperation: String, Codable, CaseIterable {
    case move
    case copy
}

struct TrackTransferRecord: Equatable {
    let sourceTrackID: UUID
    let destinationTrackID: UUID
}

struct TrackTransferResult: Equatable {
    let operation: TrackTransferOperation
    let role: TrackRole
    let sourcePlaylistID: UUID
    let destinationPlaylistID: UUID
    let transferred: [TrackTransferRecord]
    let duplicateTrackIDs: [UUID]
    let missingTrackIDs: [UUID]

    var transferredCount: Int { transferred.count }
    var duplicateCount: Int { duplicateTrackIDs.count }
    var attemptedCount: Int { transferredCount + duplicateCount + missingTrackIDs.count }
    var transferredSourceTrackIDs: [UUID] { transferred.map(\.sourceTrackID) }
    var destinationTrackIDs: [UUID] { transferred.map(\.destinationTrackID) }
    var hasPartialConflicts: Bool { !duplicateTrackIDs.isEmpty || !missingTrackIDs.isEmpty }
}

extension Array where Element == Playlist {
    /// Transfers music tracks in their source-playlist order and appends them to
    /// the destination. Duplicate file identities are skipped individually.
    @discardableResult
    mutating func transferMusicTracks(
        _ trackIDs: Set<UUID>,
        from sourcePlaylistID: UUID,
        to destinationPlaylistID: UUID,
        operation: TrackTransferOperation,
        makeTrackID: () -> UUID = { UUID() }
    ) -> TrackTransferResult? {
        guard sourcePlaylistID != destinationPlaylistID,
              let sourceIndex = firstIndex(where: { $0.id == sourcePlaylistID }),
              let destinationIndex = firstIndex(where: { $0.id == destinationPlaylistID }) else {
            return nil
        }

        let allUsedIDs = Set(flatMap { $0.tracks.map(\.id) })
        var sourceTracks = self[sourceIndex].tracks
        var destinationTracks = self[destinationIndex].tracks
        let result = TrackTransferEngine.transfer(
            requestedIDs: trackIDs,
            sourceTracks: &sourceTracks,
            destinationTracks: &destinationTracks,
            operation: operation,
            role: .music,
            sourcePlaylistID: sourcePlaylistID,
            destinationPlaylistID: destinationPlaylistID,
            usedTrackIDs: allUsedIDs,
            makeTrackID: makeTrackID
        )

        self[sourceIndex].tracks = sourceTracks
        self[destinationIndex].tracks = destinationTracks
        return result
    }

    /// Reorders a selected group as one stable block. Dropping a group forward
    /// places it after the target; dropping it backward places it before target,
    /// matching the existing single-track reorder behavior.
    @discardableResult
    mutating func reorderMusicTracks(
        _ trackIDs: Set<UUID>,
        in playlistID: UUID,
        to targetTrackID: UUID
    ) -> Bool {
        guard let playlistIndex = firstIndex(where: { $0.id == playlistID }) else { return false }
        return TrackTransferEngine.reorder(
            tracks: &self[playlistIndex].tracks,
            movingIDs: trackIDs,
            to: targetTrackID
        )
    }

    /// Moves a stable selected group to the trailing edge even when the current
    /// last track is part of that group and therefore cannot serve as a target.
    @discardableResult
    mutating func reorderMusicTracksToEnd(
        _ trackIDs: Set<UUID>,
        in playlistID: UUID
    ) -> Bool {
        guard let playlistIndex = firstIndex(where: { $0.id == playlistID }) else { return false }
        return TrackTransferEngine.reorderToEnd(
            tracks: &self[playlistIndex].tracks,
            movingIDs: trackIDs
        )
    }
}

extension Array where Element == EffectPlaylist {
    /// Effect counterpart of `transferMusicTracks`.
    @discardableResult
    mutating func transferEffectTracks(
        _ trackIDs: Set<UUID>,
        from sourcePlaylistID: UUID,
        to destinationPlaylistID: UUID,
        operation: TrackTransferOperation,
        makeTrackID: () -> UUID = { UUID() }
    ) -> TrackTransferResult? {
        guard sourcePlaylistID != destinationPlaylistID,
              let sourceIndex = firstIndex(where: { $0.id == sourcePlaylistID }),
              let destinationIndex = firstIndex(where: { $0.id == destinationPlaylistID }) else {
            return nil
        }

        let allUsedIDs = Set(flatMap { $0.effects.map(\.id) })
        var sourceTracks = self[sourceIndex].effects
        var destinationTracks = self[destinationIndex].effects
        let result = TrackTransferEngine.transfer(
            requestedIDs: trackIDs,
            sourceTracks: &sourceTracks,
            destinationTracks: &destinationTracks,
            operation: operation,
            role: .effect,
            sourcePlaylistID: sourcePlaylistID,
            destinationPlaylistID: destinationPlaylistID,
            usedTrackIDs: allUsedIDs,
            makeTrackID: makeTrackID
        )

        self[sourceIndex].effects = sourceTracks
        self[destinationIndex].effects = destinationTracks
        return result
    }

    /// Effect counterpart of `reorderMusicTracks`.
    @discardableResult
    mutating func reorderEffectTracks(
        _ trackIDs: Set<UUID>,
        in playlistID: UUID,
        to targetTrackID: UUID
    ) -> Bool {
        guard let playlistIndex = firstIndex(where: { $0.id == playlistID }) else { return false }
        return TrackTransferEngine.reorder(
            tracks: &self[playlistIndex].effects,
            movingIDs: trackIDs,
            to: targetTrackID
        )
    }

    /// Effect counterpart of `reorderMusicTracksToEnd`.
    @discardableResult
    mutating func reorderEffectTracksToEnd(
        _ trackIDs: Set<UUID>,
        in playlistID: UUID
    ) -> Bool {
        guard let playlistIndex = firstIndex(where: { $0.id == playlistID }) else { return false }
        return TrackTransferEngine.reorderToEnd(
            tracks: &self[playlistIndex].effects,
            movingIDs: trackIDs
        )
    }
}

private enum TrackTransferEngine {
    static func transfer(
        requestedIDs: Set<UUID>,
        sourceTracks: inout [Track],
        destinationTracks: inout [Track],
        operation: TrackTransferOperation,
        role: TrackRole,
        sourcePlaylistID: UUID,
        destinationPlaylistID: UUID,
        usedTrackIDs: Set<UUID>,
        makeTrackID: () -> UUID
    ) -> TrackTransferResult {
        let orderedSources = sourceTracks.filter { requestedIDs.contains($0.id) }
        let foundIDs = Set(orderedSources.map(\.id))
        let missingIDs = requestedIDs
            .subtracting(foundIDs)
            .sorted { $0.uuidString < $1.uuidString }

        var destinationIdentities = Set(destinationTracks.map { TrackFileIdentity.key(for: $0) })
        var destinationIDs = Set(destinationTracks.map(\.id))
        var reservedIDs = usedTrackIDs
        var transferredTracks: [Track] = []
        var records: [TrackTransferRecord] = []
        var duplicateIDs: [UUID] = []

        for sourceTrack in orderedSources {
            let identity = TrackFileIdentity.key(for: sourceTrack)
            let hasMoveIDCollision = operation == .move && destinationIDs.contains(sourceTrack.id)
            guard !hasMoveIDCollision,
                  destinationIdentities.insert(identity).inserted else {
                duplicateIDs.append(sourceTrack.id)
                continue
            }

            let destinationTrack: Track
            switch operation {
            case .move:
                destinationTrack = sourceTrack

            case .copy:
                let newID = nextUniqueID(usedIDs: &reservedIDs, makeTrackID: makeTrackID)
                destinationTrack = Track(
                    id: newID,
                    title: sourceTrack.title,
                    path: sourceTrack.path,
                    role: sourceTrack.role,
                    bookmarkData: sourceTrack.bookmarkData,
                    volumeMultiplier: sourceTrack.volumeMultiplier
                )
            }

            transferredTracks.append(destinationTrack)
            destinationIDs.insert(destinationTrack.id)
            records.append(
                TrackTransferRecord(
                    sourceTrackID: sourceTrack.id,
                    destinationTrackID: destinationTrack.id
                )
            )
        }

        destinationTracks.append(contentsOf: transferredTracks)
        if operation == .move {
            let movedIDs = Set(records.map(\.sourceTrackID))
            sourceTracks.removeAll { movedIDs.contains($0.id) }
        }

        return TrackTransferResult(
            operation: operation,
            role: role,
            sourcePlaylistID: sourcePlaylistID,
            destinationPlaylistID: destinationPlaylistID,
            transferred: records,
            duplicateTrackIDs: duplicateIDs,
            missingTrackIDs: missingIDs
        )
    }

    static func reorder(tracks: inout [Track], movingIDs: Set<UUID>, to targetID: UUID) -> Bool {
        guard !movingIDs.isEmpty,
              !movingIDs.contains(targetID),
              let targetIndex = tracks.firstIndex(where: { $0.id == targetID }) else {
            return false
        }

        let indexedMovingTracks = tracks.enumerated().filter { movingIDs.contains($0.element.id) }
        guard let firstMovingIndex = indexedMovingTracks.first?.offset else { return false }

        let movingTracks = indexedMovingTracks.map(\.element)
        let oldOrder = tracks.map(\.id)
        tracks.removeAll { movingIDs.contains($0.id) }

        guard let remainingTargetIndex = tracks.firstIndex(where: { $0.id == targetID }) else {
            return false
        }

        let movingForward = firstMovingIndex < targetIndex
        let insertionIndex = remainingTargetIndex + (movingForward ? 1 : 0)
        tracks.insert(contentsOf: movingTracks, at: insertionIndex)
        return tracks.map(\.id) != oldOrder
    }

    static func reorderToEnd(tracks: inout [Track], movingIDs: Set<UUID>) -> Bool {
        guard !movingIDs.isEmpty else { return false }
        let movingTracks = tracks.filter { movingIDs.contains($0.id) }
        guard !movingTracks.isEmpty else { return false }

        let oldOrder = tracks.map(\.id)
        tracks.removeAll { movingIDs.contains($0.id) }
        tracks.append(contentsOf: movingTracks)
        return tracks.map(\.id) != oldOrder
    }

    private static func nextUniqueID(usedIDs: inout Set<UUID>, makeTrackID: () -> UUID) -> UUID {
        for _ in 0..<32 {
            let candidate = makeTrackID()
            if usedIDs.insert(candidate).inserted {
                return candidate
            }
        }

        // A production UUID generator will never reach this path. Keeping a
        // fallback prevents a malformed injected generator from hanging forever.
        var fallback = UUID()
        while !usedIDs.insert(fallback).inserted {
            fallback = UUID()
        }
        return fallback
    }
}

enum TrackFileIdentity {
    static func key(for track: Track) -> String {
        let trimmedPath = track.path.trimmingCharacters(in: .whitespacesAndNewlines)
        let fallbackURL = trimmedPath.isEmpty ? nil : URL(fileURLWithPath: trimmedPath)
        let actualURL = resolvedBookmarkURL(for: track) ?? fallbackURL
        guard let actualURL else { return "track-id:\(track.id.uuidString.lowercased())" }

        return actualURL
            .resolvingSymlinksInPath()
            .standardizedFileURL
            .path
            .precomposedStringWithCanonicalMapping
            .lowercased()
    }

    private static func resolvedBookmarkURL(for track: Track) -> URL? {
        guard let bookmarkData = track.bookmarkData else { return nil }
        var isStale = false
        if let url = try? URL(
            resolvingBookmarkData: bookmarkData,
            options: [.withoutUI, .withoutMounting],
            relativeTo: nil,
            bookmarkDataIsStale: &isStale
        ) {
            return url
        }
        return try? URL(
            resolvingBookmarkData: bookmarkData,
            options: [.withSecurityScope, .withoutUI, .withoutMounting],
            relativeTo: nil,
            bookmarkDataIsStale: &isStale
        )
    }

}
