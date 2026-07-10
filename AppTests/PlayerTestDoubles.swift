import Foundation
@testable import Dungeon_Soundboard

enum PlayerTestFailure: Error, Equatable {
    case missingPlayer
    case missingLease(UUID)
    case decode
}

@MainActor
final class MockAudioPlayer: AudioPlayerAdapter {
    weak var eventDelegate: (any AudioPlayerAdapterDelegate)?

    var volume: Float = 0
    var currentTime: TimeInterval = 0
    var duration: TimeInterval
    var prepareResult = true
    var playResults: [Bool]

    private(set) var prepareCallCount = 0
    private(set) var playCallCount = 0
    private(set) var pauseCallCount = 0
    private(set) var stopCallCount = 0
    private(set) var volumeTransitions: [(volume: Float, duration: TimeInterval)] = []

    init(duration: TimeInterval = 180, playResults: [Bool] = [true]) {
        self.duration = duration
        self.playResults = playResults
    }

    func prepareToPlay() -> Bool {
        prepareCallCount += 1
        return prepareResult
    }

    func play() -> Bool {
        playCallCount += 1
        return playResults.isEmpty ? true : playResults.removeFirst()
    }

    func pause() {
        pauseCallCount += 1
    }

    func stop() {
        stopCallCount += 1
    }

    func setVolume(_ volume: Float, fadeDuration: TimeInterval) {
        self.volume = volume
        volumeTransitions.append((volume, fadeDuration))
    }

    func emitFinish(successfully: Bool) {
        eventDelegate?.audioPlayerAdapterDidFinish(self, successfully: successfully)
    }

    func emitDecodeError(_ error: Error? = PlayerTestFailure.decode) {
        eventDelegate?.audioPlayerAdapter(self, decodeError: error)
    }
}

@MainActor
final class MockAudioPlayerFactory: AudioPlayerFactory {
    enum Outcome {
        case player(MockAudioPlayer)
        case failure(any Error)
    }

    var outcomes: [Outcome] = []
    private(set) var requestedURLs: [URL] = []

    func enqueue(_ player: MockAudioPlayer) {
        outcomes.append(.player(player))
    }

    func enqueue(error: any Error) {
        outcomes.append(.failure(error))
    }

    func makePlayer(url: URL) throws -> any AudioPlayerAdapter {
        requestedURLs.append(url)
        guard !outcomes.isEmpty else { throw PlayerTestFailure.missingPlayer }
        switch outcomes.removeFirst() {
        case .player(let player):
            return player
        case .failure(let error):
            throw error
        }
    }
}

@MainActor
final class MockFileAccessResolver: FileAccessResolving {
    private var leasesByTrackID: [UUID: [FileAccessLease]] = [:]
    var bookmarkDataToReturn: Data?
    private(set) var resolvedTrackIDs: [UUID] = []
    private(set) var bookmarkRequests: [URL] = []

    func enqueue(_ lease: FileAccessLease, for trackID: UUID) {
        leasesByTrackID[trackID, default: []].append(lease)
    }

    func resolve(track: Track) throws -> FileAccessLease {
        resolvedTrackIDs.append(track.id)
        guard var leases = leasesByTrackID[track.id], !leases.isEmpty else {
            throw PlayerTestFailure.missingLease(track.id)
        }
        let lease = leases.removeFirst()
        leasesByTrackID[track.id] = leases
        return lease
    }

    func bookmarkData(for url: URL) -> Data? {
        bookmarkRequests.append(url)
        return bookmarkDataToReturn
    }
}

@MainActor
final class MockPlaybackScheduler: PlaybackScheduling {
    private(set) var requestedIntervals: [UInt64] = []
    private var actions: [@MainActor @Sendable () -> Void] = []

    func scheduleRepeating(
        everyNanoseconds interval: UInt64,
        action: @escaping @MainActor @Sendable () -> Void
    ) -> Task<Void, Never> {
        requestedIntervals.append(interval)
        actions.append(action)
        return Task { @MainActor in }
    }

    func fire() {
        actions.forEach { $0() }
    }
}

final class MockTelemetry: TelemetryReporting, @unchecked Sendable {
    struct Event: Equatable, Sendable {
        let message: String
        let metadata: [String: String]
    }

    private let lock = NSLock()
    private var storedInfo: [Event] = []
    private var storedWarnings: [Event] = []
    private var storedErrors: [Event] = []

    var infoEvents: [Event] { withLock { storedInfo } }
    var warningEvents: [Event] { withLock { storedWarnings } }
    var errorEvents: [Event] { withLock { storedErrors } }

    func info(_ message: String, metadata: [String: String]) {
        withLock { storedInfo.append(Event(message: message, metadata: metadata)) }
    }

    func warning(_ message: String, metadata: [String: String]) {
        withLock { storedWarnings.append(Event(message: message, metadata: metadata)) }
    }

    func error(_ message: String, metadata: [String: String]) {
        withLock { storedErrors.append(Event(message: message, metadata: metadata)) }
    }

    @discardableResult
    private func withLock<T>(_ body: () -> T) -> T {
        lock.lock()
        defer { lock.unlock() }
        return body()
    }
}

@MainActor
final class PlayerTestHarness {
    let suiteName: String
    let defaults: UserDefaults
    let factory = MockAudioPlayerFactory()
    let resolver = MockFileAccessResolver()
    let scheduler = MockPlaybackScheduler()
    let telemetry = MockTelemetry()

    init(
        musicPlaylists: [Playlist]? = nil,
        effectPlaylists: [EffectPlaylist]? = nil,
        selectedMusicPlaylistID: UUID? = nil,
        selectedEffectPlaylistID: UUID? = nil,
        migrationCompleted: Bool = true
    ) throws {
        suiteName = "DungeonSoundboardAppTests.\(UUID().uuidString)"
        guard let defaults = UserDefaults(suiteName: suiteName) else {
            preconditionFailure("Unable to create isolated UserDefaults suite")
        }
        self.defaults = defaults
        defaults.removePersistentDomain(forName: suiteName)
        defaults.set(migrationCompleted, forKey: PlayerDefaultsKeys.migrationCompleted)

        if let musicPlaylists {
            defaults.set(try JSONEncoder().encode(musicPlaylists), forKey: PlayerDefaultsKeys.musicPlaylists)
        }
        if let effectPlaylists {
            defaults.set(try JSONEncoder().encode(effectPlaylists), forKey: PlayerDefaultsKeys.effectPlaylists)
        }
        if let selectedMusicPlaylistID {
            defaults.set(selectedMusicPlaylistID.uuidString, forKey: PlayerDefaultsKeys.selectedMusicPlaylistID)
        }
        if let selectedEffectPlaylistID {
            defaults.set(selectedEffectPlaylistID.uuidString, forKey: PlayerDefaultsKeys.selectedEffectPlaylistID)
        }
    }

    func makeViewModel() -> PlayerViewModel {
        PlayerViewModel(
            defaults: defaults,
            audioPlayerFactory: factory,
            fileAccessResolver: resolver,
            playbackScheduler: scheduler,
            telemetry: telemetry
        )
    }

    func persistedMusicPlaylists() throws -> [Playlist] {
        let data = try requiredData(forKey: PlayerDefaultsKeys.musicPlaylists)
        return try JSONDecoder().decode([Playlist].self, from: data)
    }

    func persistedEffectPlaylists() throws -> [EffectPlaylist] {
        let data = try requiredData(forKey: PlayerDefaultsKeys.effectPlaylists)
        return try JSONDecoder().decode([EffectPlaylist].self, from: data)
    }

    func cleanup() {
        defaults.removePersistentDomain(forName: suiteName)
    }

    private func requiredData(forKey key: String) throws -> Data {
        guard let data = defaults.data(forKey: key) else {
            throw CocoaError(.fileReadNoSuchFile)
        }
        return data
    }
}

@MainActor
func makeTestTrack(
    id: UUID = UUID(),
    title: String,
    role: TrackRole = .music,
    path: String? = nil,
    bookmarkData: Data? = nil,
    volumeMultiplier: Double = 1
) -> Track {
    Track(
        id: id,
        title: title,
        path: path ?? "/tmp/\(id.uuidString).mp3",
        role: role,
        bookmarkData: bookmarkData,
        volumeMultiplier: volumeMultiplier
    )
}

@MainActor
func makeTestLease(for track: Track, refreshedBookmarkData: Data? = nil) -> FileAccessLease {
    FileAccessLease(url: track.url, refreshedBookmarkData: refreshedBookmarkData)
}

@MainActor
func isTestLeaseClosed(_ lease: FileAccessLease) -> Bool {
    lease.isClosed
}
