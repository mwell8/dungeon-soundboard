import AppKit
import SwiftUI
import UniformTypeIdentifiers

extension UTType {
    static let dungeonTrackSelection = UTType(
        exportedAs: "com.mwell.dungeon-soundboard.track-selection"
    )
    static let dungeonPlaylistReference = UTType(
        exportedAs: "com.mwell.dungeon-soundboard.playlist-reference"
    )
}

enum DragTrackRole: String, Codable, Sendable {
    case music
    case effect

    init(_ role: TrackRole) {
        switch role {
        case .music:
            self = .music
        case .effect:
            self = .effect
        }
    }
}

struct TrackDragPayload: Codable, Equatable, Sendable {
    static let currentVersion = 1

    let version: Int
    let sessionID: UUID
    let role: DragTrackRole
    let sourcePlaylistID: UUID
    let trackIDs: [UUID]

    init(role: DragTrackRole, sourcePlaylistID: UUID, trackIDs: [UUID]) {
        version = Self.currentVersion
        sessionID = UUID()
        self.role = role
        self.sourcePlaylistID = sourcePlaylistID
        self.trackIDs = trackIDs
    }
}

struct PlaylistDragPayload: Codable, Equatable, Sendable {
    static let currentVersion = 1

    let version: Int
    let sessionID: UUID
    let role: DragTrackRole
    let playlistID: UUID

    init(role: DragTrackRole, playlistID: UUID) {
        version = Self.currentVersion
        sessionID = UUID()
        self.role = role
        self.playlistID = playlistID
    }
}

enum DungeonDragItemProvider {
    static func track(_ payload: TrackDragPayload) -> NSItemProvider {
        provider(for: payload, contentType: .dungeonTrackSelection)
    }

    static func playlist(_ payload: PlaylistDragPayload) -> NSItemProvider {
        provider(for: payload, contentType: .dungeonPlaylistReference)
    }

    private static func provider<Payload: Encodable>(
        for payload: Payload,
        contentType: UTType
    ) -> NSItemProvider {
        let provider = NSItemProvider()
        guard let data = try? JSONEncoder().encode(payload) else {
            return provider
        }

        provider.registerDataRepresentation(
            forTypeIdentifier: contentType.identifier,
            visibility: .ownProcess
        ) { completion in
            completion(data, nil)
            return nil
        }
        return provider
    }
}

struct PlaylistRowDropDelegate: DropDelegate {
    let role: DragTrackRole
    let targetPlaylistID: UUID
    @Binding var activeTrackPayload: TrackDragPayload?
    @Binding var activePlaylistPayload: PlaylistDragPayload?
    @Binding var targetedPlaylistID: UUID?
    let movePlaylist: (UUID, UUID) -> Void
    let transferTracks: (TrackDragPayload, UUID, TrackTransferOperation) -> Void
    let importFiles: ([NSItemProvider], UUID) -> Void
    let finishPlaylistDrag: () -> Void
    let finishTrackDrag: () -> Void

    func validateDrop(info: DropInfo) -> Bool {
        if info.hasItemsConforming(to: [.dungeonPlaylistReference]) {
            guard let payload = activePlaylistPayload else { return false }
            return payload.version == PlaylistDragPayload.currentVersion
                && payload.role == role
                && payload.playlistID != targetPlaylistID
        }

        if info.hasItemsConforming(to: [.dungeonTrackSelection]) {
            guard let payload = activeTrackPayload else { return false }
            return payload.version == TrackDragPayload.currentVersion
                && payload.role == role
                && payload.sourcePlaylistID != targetPlaylistID
                && !payload.trackIDs.isEmpty
        }

        return info.hasItemsConforming(to: [.fileURL])
    }

    func dropUpdated(info: DropInfo) -> DropProposal? {
        guard validateDrop(info: info) else {
            return DropProposal(operation: .cancel)
        }

        if info.hasItemsConforming(to: [.fileURL]) {
            return DropProposal(operation: .copy)
        }
        if info.hasItemsConforming(to: [.dungeonPlaylistReference]) {
            return DropProposal(operation: .move)
        }
        return DropProposal(operation: requestedTrackOperation.dropOperation)
    }

    func dropEntered(info: DropInfo) {
        guard validateDrop(info: info) else { return }
        targetedPlaylistID = targetPlaylistID
    }

    func dropExited(info: DropInfo) {
        if targetedPlaylistID == targetPlaylistID {
            targetedPlaylistID = nil
        }
    }

    func performDrop(info: DropInfo) -> Bool {
        defer {
            if targetedPlaylistID == targetPlaylistID {
                targetedPlaylistID = nil
            }
        }

        if info.hasItemsConforming(to: [.dungeonPlaylistReference]),
           let payload = activePlaylistPayload,
           payload.version == PlaylistDragPayload.currentVersion,
           payload.role == role,
           payload.playlistID != targetPlaylistID {
            movePlaylist(payload.playlistID, targetPlaylistID)
            finishPlaylistDrag()
            return true
        }

        if info.hasItemsConforming(to: [.dungeonTrackSelection]),
           let payload = activeTrackPayload,
           payload.role == role,
           payload.sourcePlaylistID != targetPlaylistID {
            transferTracks(payload, targetPlaylistID, requestedTrackOperation)
            finishTrackDrag()
            return true
        }

        let providers = info.itemProviders(for: [.fileURL])
        guard !providers.isEmpty else { return false }
        importFiles(providers, targetPlaylistID)
        return true
    }

    private var requestedTrackOperation: TrackTransferOperation {
        NSEvent.modifierFlags.contains(.option) ? .copy : .move
    }
}

struct TrackCardDropDelegate: DropDelegate {
    let role: DragTrackRole
    let playlistID: UUID?
    let targetTrackID: UUID
    @Binding var activePayload: TrackDragPayload?
    @Binding var targetedTrackID: UUID?
    let reorderTracks: (TrackDragPayload, UUID) -> Void
    let finishDrag: () -> Void

    func validateDrop(info: DropInfo) -> Bool {
        guard info.hasItemsConforming(to: [.dungeonTrackSelection]),
              let payload = activePayload else {
            return false
        }
        return payload.version == TrackDragPayload.currentVersion
            && payload.role == role
            && playlistID == payload.sourcePlaylistID
            && !payload.trackIDs.isEmpty
    }

    func dropUpdated(info: DropInfo) -> DropProposal? {
        DropProposal(operation: validateDrop(info: info) ? .move : .cancel)
    }

    func dropEntered(info: DropInfo) {
        guard validateDrop(info: info) else { return }
        targetedTrackID = targetTrackID
    }

    func dropExited(info: DropInfo) {
        if targetedTrackID == targetTrackID {
            targetedTrackID = nil
        }
    }

    func performDrop(info: DropInfo) -> Bool {
        defer {
            if targetedTrackID == targetTrackID {
                targetedTrackID = nil
            }
        }
        guard validateDrop(info: info), let payload = activePayload else {
            return false
        }
        if !payload.trackIDs.contains(targetTrackID) {
            reorderTracks(payload, targetTrackID)
        }
        finishDrag()
        return true
    }
}

struct TrackGridEndDropDelegate: DropDelegate {
    let role: DragTrackRole
    let playlistID: UUID?
    @Binding var activePayload: TrackDragPayload?
    @Binding var targetedCardID: UUID?
    @Binding var isTargeted: Bool
    let reorderTracksToEnd: (TrackDragPayload) -> Void
    let importFiles: ([NSItemProvider], UUID) -> Void
    let finishDrag: () -> Void

    func validateDrop(info: DropInfo) -> Bool {
        if info.hasItemsConforming(to: [.fileURL]) {
            return playlistID != nil
        }

        guard info.hasItemsConforming(to: [.dungeonTrackSelection]),
              let payload = activePayload else {
            return false
        }
        return payload.version == TrackDragPayload.currentVersion
            && payload.role == role
            && playlistID == payload.sourcePlaylistID
            && targetedCardID == nil
            && !payload.trackIDs.isEmpty
    }

    func dropUpdated(info: DropInfo) -> DropProposal? {
        if info.hasItemsConforming(to: [.fileURL]), playlistID != nil {
            return DropProposal(operation: .copy)
        }
        return DropProposal(operation: validateDrop(info: info) ? .move : .cancel)
    }

    func dropEntered(info: DropInfo) {
        guard validateDrop(info: info) else { return }
        isTargeted = true
    }

    func dropExited(info: DropInfo) {
        isTargeted = false
    }

    func performDrop(info: DropInfo) -> Bool {
        defer { isTargeted = false }

        if info.hasItemsConforming(to: [.fileURL]), let playlistID {
            let providers = info.itemProviders(for: [.fileURL])
            guard !providers.isEmpty else { return false }
            importFiles(providers, playlistID)
            return true
        }

        guard validateDrop(info: info), let payload = activePayload else {
            return false
        }
        reorderTracksToEnd(payload)
        finishDrag()
        return true
    }
}

private extension TrackTransferOperation {
    var dropOperation: DropOperation {
        switch self {
        case .move:
            return .move
        case .copy:
            return .copy
        }
    }
}

@MainActor
final class OrderedFileURLDropCollector {
    private let providers: [(offset: Int, element: NSItemProvider)]
    private let completion: @MainActor ([URL]) -> Void
    private var remaining: Int
    private var urlsByOffset: [Int: URL] = [:]

    private init(
        providers: [NSItemProvider],
        completion: @escaping @MainActor ([URL]) -> Void
    ) {
        self.providers = providers.enumerated().filter { pair in
            pair.element.hasItemConformingToTypeIdentifier(UTType.fileURL.identifier)
        }
        self.completion = completion
        remaining = self.providers.count
    }

    @discardableResult
    static func collect(
        from providers: [NSItemProvider],
        completion: @escaping @MainActor ([URL]) -> Void
    ) -> Bool {
        let collector = OrderedFileURLDropCollector(providers: providers, completion: completion)
        guard collector.remaining > 0 else { return false }
        collector.start()
        return true
    }

    private func start() {
        for provider in providers {
            let offset = provider.offset
            let itemProvider = provider.element
            itemProvider.loadItem(
                forTypeIdentifier: UTType.fileURL.identifier,
                options: nil
            ) { [self] item, _ in
                let url: URL?
                if let data = item as? Data {
                    url = URL(dataRepresentation: data, relativeTo: nil)
                } else if let fileURL = item as? URL {
                    url = fileURL
                } else if let fileURL = item as? NSURL {
                    url = fileURL as URL
                } else {
                    url = nil
                }

                Task { @MainActor [self] in
                    finish(offset: offset, url: url)
                }
            }
        }
    }

    private func finish(offset: Int, url: URL?) {
        if let url {
            urlsByOffset[offset] = url
        }
        remaining -= 1
        guard remaining == 0 else { return }

        let orderedURLs = urlsByOffset.keys.sorted().compactMap { urlsByOffset[$0] }
        completion(orderedURLs)
    }
}
