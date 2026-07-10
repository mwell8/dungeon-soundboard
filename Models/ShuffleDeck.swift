import Foundation

/// A transient shuffle cycle for one music playlist.
///
/// The deck owns only the not-yet-played IDs. Callers supply a shuffler so tests
/// can be deterministic while production code can use the default random order.
struct ShuffleDeck: Equatable {
    typealias Shuffler = ([UUID]) -> [UUID]

    let playlistID: UUID
    private(set) var remainingTrackIDs: [UUID]
    private var knownTrackIDs: Set<UUID>

    init(
        playlistID: UUID,
        trackIDs: [UUID],
        excluding currentTrackID: UUID? = nil,
        shuffler: Shuffler = { $0.shuffled() }
    ) {
        let uniqueTrackIDs = Self.uniqueIDs(trackIDs)
        self.playlistID = playlistID
        self.knownTrackIDs = Set(uniqueTrackIDs)
        self.remainingTrackIDs = Self.makeCycle(
            trackIDs: uniqueTrackIDs,
            excluding: currentTrackID,
            shuffler: shuffler
        )
    }

    /// Reconciles an active cycle with library edits.
    ///
    /// Deleted IDs disappear immediately. Newly added IDs join the remaining
    /// cycle, while IDs already consumed in this cycle are not reintroduced.
    mutating func reconcile(
        availableTrackIDs: [UUID],
        currentTrackID: UUID? = nil,
        shuffler: Shuffler = { $0.shuffled() }
    ) {
        let uniqueAvailableIDs = Self.uniqueIDs(availableTrackIDs)
        let available = Set(uniqueAvailableIDs)
        remainingTrackIDs = remainingTrackIDs.filter {
            available.contains($0) && $0 != currentTrackID
        }

        let addedIDs = uniqueAvailableIDs.filter {
            !knownTrackIDs.contains($0) && $0 != currentTrackID
        }
        let orderedAdditions = Self.makeCycle(
            trackIDs: addedIDs,
            excluding: nil,
            shuffler: shuffler
        )
        let remaining = Set(remainingTrackIDs)
        remainingTrackIDs.append(contentsOf: orderedAdditions.filter { !remaining.contains($0) })
        knownTrackIDs = available
    }

    /// Draws the next reference according to the current repeat mode.
    ///
    /// - `.off` returns `nil` after the current cycle is exhausted.
    /// - `.all` starts a new cycle and avoids immediately repeating the current
    ///   track when the playlist contains more than one track.
    /// - `.one` returns the current track when it still exists.
    mutating func drawNext(
        repeatMode: RepeatMode,
        availableTrackIDs: [UUID],
        currentTrackID: UUID?,
        shuffler: Shuffler = { $0.shuffled() }
    ) -> MusicTrackReference? {
        let availableTrackIDs = Self.uniqueIDs(availableTrackIDs)
        let available = Set(availableTrackIDs)
        reconcile(
            availableTrackIDs: availableTrackIDs,
            currentTrackID: currentTrackID,
            shuffler: shuffler
        )

        if repeatMode == .one {
            guard let currentTrackID, available.contains(currentTrackID) else { return nil }
            return MusicTrackReference(playlistID: playlistID, trackID: currentTrackID)
        }

        if let nextID = popFirstRemaining() {
            return MusicTrackReference(playlistID: playlistID, trackID: nextID)
        }

        switch repeatMode {
        case .off:
            return nil

        case .one:
            return nil

        case .all:
            guard !availableTrackIDs.isEmpty else { return nil }

            if availableTrackIDs.count == 1 {
                return MusicTrackReference(playlistID: playlistID, trackID: availableTrackIDs[0])
            }

            remainingTrackIDs = Self.makeCompleteCycleAvoidingImmediateRepeat(
                trackIDs: availableTrackIDs,
                currentTrackID: currentTrackID,
                shuffler: shuffler
            )
            knownTrackIDs = available
            guard let nextID = popFirstRemaining() else { return nil }
            return MusicTrackReference(playlistID: playlistID, trackID: nextID)
        }
    }

    private mutating func popFirstRemaining() -> UUID? {
        guard !remainingTrackIDs.isEmpty else { return nil }
        return remainingTrackIDs.removeFirst()
    }

    private static func makeCycle(
        trackIDs: [UUID],
        excluding currentTrackID: UUID?,
        shuffler: Shuffler
    ) -> [UUID] {
        let candidates = uniqueIDs(trackIDs).filter { $0 != currentTrackID }
        guard !candidates.isEmpty else { return [] }

        // Normalize the injected order so an incomplete or duplicate-producing
        // shuffler cannot lose tracks or make a cycle play one track twice.
        let candidateSet = Set(candidates)
        var seen = Set<UUID>()
        var result: [UUID] = []

        for id in shuffler(candidates) where candidateSet.contains(id) {
            if seen.insert(id).inserted {
                result.append(id)
            }
        }

        for id in candidates where seen.insert(id).inserted {
            result.append(id)
        }

        return result
    }

    private static func makeCompleteCycleAvoidingImmediateRepeat(
        trackIDs: [UUID],
        currentTrackID: UUID?,
        shuffler: Shuffler
    ) -> [UUID] {
        var cycle = makeCycle(trackIDs: trackIDs, excluding: nil, shuffler: shuffler)
        guard cycle.count > 1,
              let currentTrackID,
              cycle.first == currentTrackID else {
            return cycle
        }

        // Текущий трек остаётся в полном новом цикле, но переносится с первой
        // позиции в конец, чтобы граница циклов не давала мгновенный повтор.
        cycle.removeFirst()
        cycle.append(currentTrackID)
        return cycle
    }

    private static func uniqueIDs(_ ids: [UUID]) -> [UUID] {
        var seen = Set<UUID>()
        return ids.filter { seen.insert($0).inserted }
    }
}
