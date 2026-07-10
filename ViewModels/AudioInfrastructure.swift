import Foundation
@preconcurrency import AVFAudio

@MainActor
protocol AudioPlayerAdapterDelegate: AnyObject {
    func audioPlayerAdapterDidFinish(_ player: any AudioPlayerAdapter, successfully: Bool)
    func audioPlayerAdapter(_ player: any AudioPlayerAdapter, decodeError error: Error?)
}

@MainActor
protocol AudioPlayerAdapter: AnyObject {
    var eventDelegate: (any AudioPlayerAdapterDelegate)? { get set }
    var volume: Float { get set }
    var currentTime: TimeInterval { get set }
    var duration: TimeInterval { get }

    @discardableResult func prepareToPlay() -> Bool
    @discardableResult func play() -> Bool
    func pause()
    func stop()
    func setVolume(_ volume: Float, fadeDuration: TimeInterval)
}

@MainActor
protocol AudioPlayerFactory {
    func makePlayer(url: URL) throws -> any AudioPlayerAdapter
}

@MainActor
protocol PlaybackScheduling {
    func scheduleRepeating(
        everyNanoseconds interval: UInt64,
        action: @escaping @MainActor @Sendable () -> Void
    ) -> Task<Void, Never>
}

@MainActor
struct TaskPlaybackScheduler: PlaybackScheduling {
    func scheduleRepeating(
        everyNanoseconds interval: UInt64,
        action: @escaping @MainActor @Sendable () -> Void
    ) -> Task<Void, Never> {
        Task { @MainActor in
            while !Task.isCancelled {
                try? await Task.sleep(nanoseconds: interval)
                guard !Task.isCancelled else { return }
                action()
            }
        }
    }
}

@MainActor
final class SystemAudioPlayerFactory: AudioPlayerFactory {
    func makePlayer(url: URL) throws -> any AudioPlayerAdapter {
        try SystemAudioPlayerAdapter(url: url)
    }
}

@MainActor
private final class SystemAudioPlayerAdapter: NSObject, AudioPlayerAdapter, @preconcurrency AVAudioPlayerDelegate {
    weak var eventDelegate: (any AudioPlayerAdapterDelegate)?

    private let player: AVAudioPlayer

    init(url: URL) throws {
        player = try AVAudioPlayer(contentsOf: url)
        super.init()
        player.delegate = self
    }

    var volume: Float {
        get { player.volume }
        set { player.volume = newValue }
    }

    var currentTime: TimeInterval {
        get { player.currentTime }
        set { player.currentTime = newValue }
    }

    var duration: TimeInterval { player.duration }

    func prepareToPlay() -> Bool {
        player.prepareToPlay()
    }

    func play() -> Bool {
        player.play()
    }

    func pause() {
        player.pause()
    }

    func stop() {
        player.stop()
    }

    func setVolume(_ volume: Float, fadeDuration: TimeInterval) {
        player.setVolume(volume, fadeDuration: fadeDuration)
    }

    func audioPlayerDidFinishPlaying(_ player: AVAudioPlayer, successfully flag: Bool) {
        eventDelegate?.audioPlayerAdapterDidFinish(self, successfully: flag)
    }

    func audioPlayerDecodeErrorDidOccur(_ player: AVAudioPlayer, error: Error?) {
        eventDelegate?.audioPlayerAdapter(self, decodeError: error)
    }
}

@MainActor
final class FileAccessLease {
    let url: URL
    let refreshedBookmarkData: Data?

    private let didStartSecurityScope: Bool
    private(set) var isClosed = false

    init(url: URL, didStartSecurityScope: Bool = false, refreshedBookmarkData: Data? = nil) {
        self.url = url
        self.didStartSecurityScope = didStartSecurityScope
        self.refreshedBookmarkData = refreshedBookmarkData
    }

    func close() {
        guard !isClosed else { return }
        isClosed = true
        if didStartSecurityScope {
            url.stopAccessingSecurityScopedResource()
        }
    }

    deinit {
        if !isClosed, didStartSecurityScope {
            url.stopAccessingSecurityScopedResource()
        }
    }
}

@MainActor
protocol FileAccessResolving {
    func resolve(track: Track) throws -> FileAccessLease
    func bookmarkData(for url: URL) -> Data?
}

@MainActor
final class PersistentFileAccessResolver: FileAccessResolving {
    func resolve(track: Track) throws -> FileAccessLease {
        if let bookmarkData = track.bookmarkData {
            if let lease = resolveStandardBookmark(bookmarkData) {
                return lease
            }
            if let lease = resolveLegacySecurityScopedBookmark(bookmarkData) {
                return lease
            }
        }

        let fallbackURL = track.url.standardizedFileURL
        guard FileManager.default.isReadableFile(atPath: fallbackURL.path) else {
            throw CocoaError(.fileReadNoSuchFile)
        }
        return FileAccessLease(url: fallbackURL)
    }

    func bookmarkData(for url: URL) -> Data? {
        try? url.standardizedFileURL.bookmarkData(
            options: [],
            includingResourceValuesForKeys: nil,
            relativeTo: nil
        )
    }

    private func resolveStandardBookmark(_ data: Data) -> FileAccessLease? {
        var isStale = false
        guard let url = try? URL(
            resolvingBookmarkData: data,
            options: [.withoutUI, .withoutMounting],
            relativeTo: nil,
            bookmarkDataIsStale: &isStale
        ), FileManager.default.isReadableFile(atPath: url.path) else {
            return nil
        }

        let refreshed = isStale ? bookmarkData(for: url) : nil
        return FileAccessLease(url: url, refreshedBookmarkData: refreshed)
    }

    private func resolveLegacySecurityScopedBookmark(_ data: Data) -> FileAccessLease? {
        var isStale = false
        guard let url = try? URL(
            resolvingBookmarkData: data,
            options: [.withSecurityScope, .withoutUI, .withoutMounting],
            relativeTo: nil,
            bookmarkDataIsStale: &isStale
        ) else {
            return nil
        }

        let didStart = url.startAccessingSecurityScopedResource()
        guard FileManager.default.isReadableFile(atPath: url.path) else {
            if didStart {
                url.stopAccessingSecurityScopedResource()
            }
            return nil
        }

        let refreshed = isStale ? bookmarkData(for: url) : nil
        return FileAccessLease(
            url: url,
            didStartSecurityScope: didStart,
            refreshedBookmarkData: refreshed
        )
    }
}
