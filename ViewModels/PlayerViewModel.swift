import Foundation
import Combine
import AppKit
@preconcurrency import AVFAudio
import UniformTypeIdentifiers

/// Главная логика плеера.
/// Разделяет музыкальные и SFX-плейлисты и управляет воспроизведением.
@MainActor
final class PlayerViewModel: NSObject, ObservableObject, @preconcurrency AVAudioPlayerDelegate {
    struct ImportConflictSummary: Identifiable, Equatable {
        let id = UUID()
        let target: String
        let attemptedCount: Int
        let addedCount: Int
        let duplicateCount: Int
        let duplicateTitles: [String]
    }

    // MARK: - Публичное состояние

    @Published var musicPlaylists: [Playlist] = []
    @Published var effectPlaylists: [EffectPlaylist] = []

    @Published var selectedMusicPlaylistID: UUID? {
        didSet {
            if !isHydratingPreferences {
                savePreferences()
            }
        }
    }

    @Published var selectedEffectPlaylistID: UUID? {
        didSet {
            if !isHydratingPreferences {
                savePreferences()
            }
        }
    }

    @Published var currentTrackID: UUID?
    @Published private(set) var playbackMusicPlaylistID: UUID?
    @Published var isPlaying: Bool = false

    @Published var volume: Double = 0.8 {
        didSet {
            // Нормализуем значение синхронно в didSet:
            // это исключает "отпрыгивания" громкости из-за отложенных async-перезаписей.
            let clamped = clampedUnitVolume(volume)
            if clamped != volume {
                volume = clamped
                return
            }
            applyMusicVolume(animated: false)
            if !isHydratingPreferences {
                savePreferences()
            }
        }
    }

    @Published var effectsVolume: Double = 0.8 {
        didSet {
            let clamped = clampedUnitVolume(effectsVolume)
            if clamped != effectsVolume {
                effectsVolume = clamped
                return
            }
            applyEffectsVolume()
            if !isHydratingPreferences {
                savePreferences()
            }
        }
    }

    @Published var repeatMode: RepeatMode = .off {
        didSet {
            if !isHydratingPreferences {
                savePreferences()
            }
        }
    }

    @Published var isShuffleEnabled: Bool = false {
        didSet {
            if !isHydratingPreferences {
                savePreferences()
            }
        }
    }

    @Published var isMusicFadeOutOnPauseEnabled: Bool = false {
        didSet {
            if !isHydratingPreferences {
                savePreferences()
            }
        }
    }

    @Published var currentTime: TimeInterval = 0
    @Published var duration: TimeInterval = 0
    @Published var errorMessage: String? {
        didSet {
            guard let errorMessage, !errorMessage.isEmpty else { return }
            AppTelemetry.shared.error(
                errorMessage,
                metadata: [
                    "music_playlists": "\(musicPlaylists.count)",
                    "effect_playlists": "\(effectPlaylists.count)"
                ]
            )
        }
    }

    @Published var importConflictSummary: ImportConflictSummary?
    @Published var isSentryTelemetryEnabled: Bool = false {
        didSet {
            if !isHydratingPreferences {
                savePreferences()
            }
        }
    }
    @Published var sentryDSN: String = "" {
        didSet {
            if !isHydratingPreferences {
                savePreferences()
            }
        }
    }

    @Published var duckingAmount: Double = 0.55 {
        didSet {
            // Приглушение ограничено безопасным диапазоном, чтобы музыка не "исчезала" полностью.
            let clamped = clampedDuckingAmount(duckingAmount)
            if clamped != duckingAmount {
                duckingAmount = clamped
                return
            }
            applyMusicVolume(animated: false)
            if !isHydratingPreferences {
                savePreferences()
            }
        }
    }

    @Published var musicColumnsCount: Int = 3 {
        didSet {
            let normalized = normalizedColumnsCount(musicColumnsCount)
            if normalized != musicColumnsCount {
                musicColumnsCount = normalized
                return
            }
            if !isHydratingPreferences {
                savePreferences()
            }
        }
    }

    @Published var effectsColumnsCount: Int = 3 {
        didSet {
            let normalized = normalizedColumnsCount(effectsColumnsCount)
            if normalized != effectsColumnsCount {
                effectsColumnsCount = normalized
                return
            }
            if !isHydratingPreferences {
                savePreferences()
            }
        }
    }

    // MARK: - Внутренние свойства

    private var musicPlayer: AVAudioPlayer?
    // Для наложения SFX используем несколько плееров одновременно (polyphony).
    private var effectPlayers: [ObjectIdentifier: AVAudioPlayer] = [:]
    private var effectScopedResources: [ObjectIdentifier: (url: URL, hasScope: Bool)] = [:]
    private var effectVolumeMultipliers: [ObjectIdentifier: Double] = [:]
    private var timer: Timer?
    private var playbackHistory: [UUID] = []
    private var activeDuckCount: Int = 0

    private var activeScopedURL: URL?
    private var hasActiveSecurityScope: Bool = false
    // Во время загрузки настроек отключаем лишние savePreferences() из didSet.
    private var isHydratingPreferences: Bool = false

    // MARK: - Поддерживаемые расширения

    private let supportedExtensions: Set<String> = [
        "mp3", "wav", "aiff", "aif", "m4a", "aac", "caf", "mp4"
    ]

    private enum PlaylistTarget {
        case music
        case effect
    }

    // MARK: - Инициализация

    override init() {
        super.init()

        loadState()
        loadPreferences()
        ensureDefaultsAfterLoading()
        startTimer()
    }

    deinit {
        timer?.invalidate()
        musicPlayer?.stop()
        for (key, effectPlayer) in effectPlayers {
            effectPlayer.stop()
            if let resource = effectScopedResources[key], resource.hasScope {
                resource.url.stopAccessingSecurityScopedResource()
            }
        }
        if hasActiveSecurityScope, let activeScopedURL {
            activeScopedURL.stopAccessingSecurityScopedResource()
        }
    }

    // MARK: - Вычисляемые свойства

    var selectedMusicPlaylistIndex: Int? {
        guard let selectedMusicPlaylistID else { return nil }
        return musicPlaylists.firstIndex(where: { $0.id == selectedMusicPlaylistID })
    }

    var selectedEffectPlaylistIndex: Int? {
        guard let selectedEffectPlaylistID else { return nil }
        return effectPlaylists.firstIndex(where: { $0.id == selectedEffectPlaylistID })
    }

    var selectedMusicPlaylist: Playlist? {
        guard let index = selectedMusicPlaylistIndex else { return nil }
        return musicPlaylists[index]
    }

    var playbackMusicPlaylistIndex: Int? {
        let playlistID = playbackMusicPlaylistID ?? selectedMusicPlaylistID
        guard let playlistID else { return nil }
        return musicPlaylists.firstIndex(where: { $0.id == playlistID })
    }

    var playbackMusicPlaylist: Playlist? {
        guard let index = playbackMusicPlaylistIndex else { return nil }
        return musicPlaylists[index]
    }

    var selectedEffectPlaylist: EffectPlaylist? {
        guard let index = selectedEffectPlaylistIndex else { return nil }
        return effectPlaylists[index]
    }

    var currentTrack: Track? {
        guard let playlist = playbackMusicPlaylist, let currentTrackID else { return nil }
        return playlist.tracks.first(where: { $0.id == currentTrackID })
    }

    var musicTracks: [Track] {
        selectedMusicPlaylist?.tracks ?? []
    }

    var effectTracks: [Track] {
        selectedEffectPlaylist?.effects ?? []
    }

    var hasActiveEffects: Bool {
        !effectPlayers.isEmpty
    }

    // MARK: - Музыкальные плейлисты

    func createMusicPlaylist() {
        let playlist = Playlist(name: nextMusicPlaylistName())
        musicPlaylists.append(playlist)
        selectedMusicPlaylistID = playlist.id
        saveState()
    }

    func renameSelectedMusicPlaylist(to newName: String) {
        guard let selectedMusicPlaylistID else { return }
        renameMusicPlaylist(selectedMusicPlaylistID, to: newName)
    }

    func renameMusicPlaylist(_ playlistID: UUID, to newName: String) {
        guard let index = musicPlaylists.firstIndex(where: { $0.id == playlistID }) else { return }

        let trimmed = newName.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else { return }

        musicPlaylists[index].name = trimmed
        saveState()
    }

    func deleteSelectedMusicPlaylist() {
        guard let selectedMusicPlaylistID else { return }
        deleteMusicPlaylist(selectedMusicPlaylistID)
    }

    func deleteMusicPlaylist(_ playlistID: UUID) {
        guard let index = musicPlaylists.firstIndex(where: { $0.id == playlistID }) else { return }

        let deletedPlaylist = musicPlaylists[index]
        let wasSelected = selectedMusicPlaylistID == deletedPlaylist.id
        let wasPlaybackContext = playbackMusicPlaylistID == deletedPlaylist.id

        if wasSelected || wasPlaybackContext {
            stop()
            currentTrackID = nil
            playbackMusicPlaylistID = nil
        }

        let deletedIDs = Set(deletedPlaylist.tracks.map { $0.id })
        playbackHistory.removeAll { deletedIDs.contains($0) }

        musicPlaylists.remove(at: index)

        if musicPlaylists.isEmpty {
            let playlist = Playlist(name: L10n.tr("playlist.main.default"))
            musicPlaylists = [playlist]
            selectedMusicPlaylistID = playlist.id
        } else if wasSelected {
            let nextIndex = min(index, musicPlaylists.count - 1)
            selectedMusicPlaylistID = musicPlaylists[nextIndex].id
        }

        saveState()
    }

    func moveMusicPlaylist(_ draggedID: UUID, to targetID: UUID) {
        guard draggedID != targetID,
              let sourceIndex = musicPlaylists.firstIndex(where: { $0.id == draggedID }),
              let targetIndex = musicPlaylists.firstIndex(where: { $0.id == targetID }) else {
            return
        }

        let playlist = musicPlaylists.remove(at: sourceIndex)
        let destinationIndex = min(targetIndex, musicPlaylists.count)
        musicPlaylists.insert(playlist, at: destinationIndex)
        saveState()
    }

    func playMusicPlaylistShuffled(_ playlist: Playlist) {
        guard let playlistIndex = musicPlaylists.firstIndex(where: { $0.id == playlist.id }) else { return }

        selectedMusicPlaylistID = playlist.id
        playbackMusicPlaylistID = playlist.id
        isShuffleEnabled = true

        let playlist = musicPlaylists[playlistIndex]
        guard !playlist.tracks.isEmpty else { return }

        playRandomTrack(from: playlist)
    }

    // MARK: - SFX плейлисты

    func createEffectPlaylist() {
        let playlist = EffectPlaylist(name: nextEffectPlaylistName())
        effectPlaylists.append(playlist)
        selectedEffectPlaylistID = playlist.id
        saveState()
    }

    func renameSelectedEffectPlaylist(to newName: String) {
        guard let selectedEffectPlaylistID else { return }
        renameEffectPlaylist(selectedEffectPlaylistID, to: newName)
    }

    func renameEffectPlaylist(_ playlistID: UUID, to newName: String) {
        guard let index = effectPlaylists.firstIndex(where: { $0.id == playlistID }) else { return }

        let trimmed = newName.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else { return }

        effectPlaylists[index].name = trimmed
        saveState()
    }

    func deleteSelectedEffectPlaylist() {
        guard let selectedEffectPlaylistID else { return }
        deleteEffectPlaylist(selectedEffectPlaylistID)
    }

    func deleteEffectPlaylist(_ playlistID: UUID) {
        guard let index = effectPlaylists.firstIndex(where: { $0.id == playlistID }) else { return }

        let deletedPlaylist = effectPlaylists[index]
        let wasSelected = selectedEffectPlaylistID == deletedPlaylist.id
        let deletedIDs = Set(deletedPlaylist.effects.map { $0.id })

        playbackHistory.removeAll { deletedIDs.contains($0) }

        effectPlaylists.remove(at: index)

        if effectPlaylists.isEmpty {
            let playlist = EffectPlaylist(name: L10n.tr("playlist.sfx_master.default"))
            effectPlaylists = [playlist]
            selectedEffectPlaylistID = playlist.id
        } else if wasSelected {
            let nextIndex = min(index, effectPlaylists.count - 1)
            selectedEffectPlaylistID = effectPlaylists[nextIndex].id
        }

        saveState()
    }

    func moveEffectPlaylist(_ draggedID: UUID, to targetID: UUID) {
        guard draggedID != targetID,
              let sourceIndex = effectPlaylists.firstIndex(where: { $0.id == draggedID }),
              let targetIndex = effectPlaylists.firstIndex(where: { $0.id == targetID }) else {
            return
        }

        let playlist = effectPlaylists.remove(at: sourceIndex)
        let destinationIndex = min(targetIndex, effectPlaylists.count)
        effectPlaylists.insert(playlist, at: destinationIndex)
        saveState()
    }

    func refreshLocalizedDefaultPlaylistNames() {
        var didChange = false
        let knownMusicDefaults = L10n.translations(for: "playlist.main.default")
        let knownEffectDefaults = L10n.translations(for: "playlist.sfx_master.default")
        let currentMusicDefault = L10n.tr("playlist.main.default")
        let currentEffectDefault = L10n.tr("playlist.sfx_master.default")

        for index in musicPlaylists.indices where knownMusicDefaults.contains(musicPlaylists[index].name) {
            if musicPlaylists[index].name != currentMusicDefault {
                musicPlaylists[index].name = currentMusicDefault
                didChange = true
            }
        }

        for index in effectPlaylists.indices where knownEffectDefaults.contains(effectPlaylists[index].name) {
            if effectPlaylists[index].name != currentEffectDefault {
                effectPlaylists[index].name = currentEffectDefault
                didChange = true
            }
        }

        if didChange {
            saveState()
        }
    }

    // MARK: - Импорт

    func addMusicTracksFromFinder() {
        addFilesFromFinder(role: .music, target: .music)
    }

    func addEffectsFromFinder() {
        addFilesFromFinder(role: .effect, target: .effect)
    }

    func addMusicFolderFromFinder() {
        addFolderFromFinder(target: .music)
    }

    func addEffectsFolderFromFinder() {
        addFolderFromFinder(target: .effect)
    }

    func importDroppedMusicURLs(_ urls: [URL]) {
        importTrackURLs(urls, role: .music, target: .music)
    }

    func importDroppedEffectURLs(_ urls: [URL]) {
        importTrackURLs(urls, role: .effect, target: .effect)
    }

    private func addFilesFromFinder(role: TrackRole, target: PlaylistTarget) {
        switch target {
        case .music:
            guard selectedMusicPlaylistIndex != nil else { return }
        case .effect:
            guard selectedEffectPlaylistIndex != nil else { return }
        }

        let panel = NSOpenPanel()
        panel.title = L10n.tr("import.files.title")
        panel.allowedContentTypes = [
            UTType.mp3,
            UTType.wav,
            UTType.aiff,
            UTType.mpeg4Audio,
            UTType.audio
        ]
        panel.allowsMultipleSelection = true
        panel.canChooseDirectories = false
        panel.canChooseFiles = true

        if panel.runModal() == .OK {
            importTrackURLs(panel.urls, role: role, target: target)
        }
    }

    private func addFolderFromFinder(target: PlaylistTarget) {
        switch target {
        case .music:
            guard selectedMusicPlaylistIndex != nil else { return }
        case .effect:
            guard selectedEffectPlaylistIndex != nil else { return }
        }

        let panel = NSOpenPanel()
        panel.title = L10n.tr("import.folder.title")
        panel.message = L10n.tr("import.folder.message")
        panel.allowsMultipleSelection = false
        panel.canChooseDirectories = true
        panel.canChooseFiles = false

        if panel.runModal() == .OK, let folderURL = panel.url {
            let role: TrackRole = target == .music ? .music : .effect
            let supportedExtensions = self.supportedExtensions
            Task { @MainActor [weak self] in
                let urls = await Task.detached(priority: .userInitiated) {
                    // Доступ к папке должен жить ровно столько, сколько длится фоновый обход.
                    let didStartAccessing = folderURL.startAccessingSecurityScopedResource()
                    defer {
                        if didStartAccessing {
                            folderURL.stopAccessingSecurityScopedResource()
                        }
                    }
                    return Self.scanTrackURLs(in: folderURL, supportedExtensions: supportedExtensions)
                }.value
                self?.importTrackURLs(urls, role: role, target: target)
            }
        }
    }

    private func importTrackURLs(_ urls: [URL], role: TrackRole, target: PlaylistTarget) {
        var collected: [URL] = []

        for url in urls {
            var isDirectory: ObjCBool = false
            if FileManager.default.fileExists(atPath: url.path, isDirectory: &isDirectory), isDirectory.boolValue {
                let scanned = Self.scanTrackURLs(in: url, supportedExtensions: supportedExtensions)
                collected.append(contentsOf: scanned)
            } else if supportedExtensions.contains(url.pathExtension.lowercased()) {
                collected.append(url)
            }
        }

        let newTracks = collected.map { makeTrackWithSecurityScope(from: $0, role: role) }
        switch target {
        case .music:
            appendUniqueMusicTracks(newTracks)
        case .effect:
            appendUniqueEffectTracks(newTracks)
        }
    }

    func removeMusicTrack(_ track: Track) {
        guard let playlistIndex = selectedMusicPlaylistIndex else { return }
        guard let trackIndex = musicPlaylists[playlistIndex].tracks.firstIndex(where: { $0.id == track.id }) else {
            return
        }

        let removedTrack = musicPlaylists[playlistIndex].tracks.remove(at: trackIndex)
        playbackHistory.removeAll { $0 == removedTrack.id }

        if currentTrackID == removedTrack.id {
            stop()
            currentTrackID = nil
        }

        saveState()
    }

    func removeMusicTracks(_ trackIDs: Set<UUID>) {
        guard !trackIDs.isEmpty else { return }
        guard let playlistIndex = selectedMusicPlaylistIndex else { return }

        let removedCurrent = currentTrackID.map(trackIDs.contains) ?? false
        musicPlaylists[playlistIndex].tracks.removeAll { trackIDs.contains($0.id) }
        playbackHistory.removeAll { trackIDs.contains($0) }

        if removedCurrent {
            stop()
            playbackMusicPlaylistID = selectedMusicPlaylistID
            currentTrackID = musicPlaylists[playlistIndex].tracks.first?.id
        }
        saveState()
    }

    func moveMusicTrack(_ draggedID: UUID, to targetID: UUID) {
        guard let playlistIndex = selectedMusicPlaylistIndex,
              Track.moveTrack(in: &musicPlaylists[playlistIndex].tracks, draggedID: draggedID, to: targetID) else {
            return
        }

        saveState()
    }

    func renameMusicTrack(playlistID: UUID, trackID: UUID, to newTitle: String) {
        guard let playlistIndex = musicPlaylists.firstIndex(where: { $0.id == playlistID }),
              let trackIndex = musicPlaylists[playlistIndex].tracks.firstIndex(where: { $0.id == trackID }) else {
            return
        }

        let currentTitle = musicPlaylists[playlistIndex].tracks[trackIndex].title
        let title = Track.normalizedTitle(newTitle, fallback: currentTitle)
        guard title != currentTitle else { return }

        musicPlaylists[playlistIndex].tracks[trackIndex].title = title
        saveState()
    }

    func musicTrackVolumeMultiplier(playlistID: UUID, trackID: UUID) -> Double {
        guard let playlistIndex = musicPlaylists.firstIndex(where: { $0.id == playlistID }),
              let track = musicPlaylists[playlistIndex].tracks.first(where: { $0.id == trackID }) else {
            return Track.defaultVolumeMultiplier
        }
        return track.volumeMultiplier
    }

    func setMusicTrackVolumeMultiplier(_ value: Double, playlistID: UUID, trackID: UUID) {
        guard let playlistIndex = musicPlaylists.firstIndex(where: { $0.id == playlistID }),
              let trackIndex = musicPlaylists[playlistIndex].tracks.firstIndex(where: { $0.id == trackID }) else {
            return
        }

        musicPlaylists[playlistIndex].tracks[trackIndex].volumeMultiplier = value
        if currentTrackID == trackID {
            applyMusicVolume(animated: false)
        }
        saveState()
    }

    func removeEffectTrack(_ track: Track) {
        guard let playlistIndex = selectedEffectPlaylistIndex else { return }
        guard let trackIndex = effectPlaylists[playlistIndex].effects.firstIndex(where: { $0.id == track.id }) else {
            return
        }

        effectPlaylists[playlistIndex].effects.remove(at: trackIndex)
        saveState()
    }

    func removeEffectTracks(_ trackIDs: Set<UUID>) {
        guard !trackIDs.isEmpty else { return }
        guard let playlistIndex = selectedEffectPlaylistIndex else { return }
        effectPlaylists[playlistIndex].effects.removeAll { trackIDs.contains($0.id) }
        saveState()
    }

    func moveEffectTrack(_ draggedID: UUID, to targetID: UUID) {
        guard let playlistIndex = selectedEffectPlaylistIndex,
              Track.moveTrack(in: &effectPlaylists[playlistIndex].effects, draggedID: draggedID, to: targetID) else {
            return
        }

        saveState()
    }

    func renameEffectTrack(playlistID: UUID, trackID: UUID, to newTitle: String) {
        guard let playlistIndex = effectPlaylists.firstIndex(where: { $0.id == playlistID }),
              let trackIndex = effectPlaylists[playlistIndex].effects.firstIndex(where: { $0.id == trackID }) else {
            return
        }

        let currentTitle = effectPlaylists[playlistIndex].effects[trackIndex].title
        let title = Track.normalizedTitle(newTitle, fallback: currentTitle)
        guard title != currentTitle else { return }

        effectPlaylists[playlistIndex].effects[trackIndex].title = title
        saveState()
    }

    func effectTrackVolumeMultiplier(playlistID: UUID, trackID: UUID) -> Double {
        guard let playlistIndex = effectPlaylists.firstIndex(where: { $0.id == playlistID }),
              let track = effectPlaylists[playlistIndex].effects.first(where: { $0.id == trackID }) else {
            return Track.defaultVolumeMultiplier
        }
        return track.volumeMultiplier
    }

    func setEffectTrackVolumeMultiplier(_ value: Double, playlistID: UUID, trackID: UUID) {
        guard let playlistIndex = effectPlaylists.firstIndex(where: { $0.id == playlistID }),
              let trackIndex = effectPlaylists[playlistIndex].effects.firstIndex(where: { $0.id == trackID }) else {
            return
        }

        effectPlaylists[playlistIndex].effects[trackIndex].volumeMultiplier = value
        saveState()
    }

    // MARK: - Управление воспроизведением музыки

    func playMusicTrack(_ track: Track) {
        playbackMusicPlaylistID = selectedMusicPlaylistID
        switchToTrack(track.id, addCurrentTrackToHistory: true)
    }

    func playMusicTrack(playlistID: UUID, trackID: UUID) -> Bool {
        guard let playlistIndex = musicPlaylists.firstIndex(where: { $0.id == playlistID }),
              musicPlaylists[playlistIndex].tracks.contains(where: { $0.id == trackID }) else {
            return false
        }
        playbackMusicPlaylistID = playlistID
        switchToTrack(trackID, addCurrentTrackToHistory: true)
        return true
    }

    func playCurrentTrack() {
        guard let track = currentTrack else { return }

        releaseScopedResource()

        do {
            let playbackURL = try resolvePlaybackURL(for: track)

            musicPlayer = try AVAudioPlayer(contentsOf: playbackURL)
            musicPlayer?.delegate = self
            applyMusicVolume(animated: false)
            musicPlayer?.prepareToPlay()
            musicPlayer?.play()

            duration = musicPlayer?.duration ?? 0
            currentTime = musicPlayer?.currentTime ?? 0
            isPlaying = true
            errorMessage = nil
        } catch {
            isPlaying = false
            errorMessage = L10n.tr("error.play_file", track.title, error.localizedDescription)
        }
    }

    func playPause() {
        if isPlaying {
            if isMusicFadeOutOnPauseEnabled {
                pauseWithFadeOut()
            } else {
                pause()
            }
        } else if let musicPlayer = musicPlayer {
            applyMusicVolume(animated: false)
            musicPlayer.play()
            isPlaying = true
        } else {
            if currentTrack == nil, let first = selectedMusicPlaylist?.tracks.first {
                playbackMusicPlaylistID = selectedMusicPlaylistID
                currentTrackID = first.id
            }
            playCurrentTrack()
        }
    }

    func pause() {
        musicPlayer?.pause()
        isPlaying = false
    }

    private func pauseWithFadeOut() {
        guard let musicPlayer else {
            pause()
            return
        }

        let targetVolume = Float(effectiveMusicVolume())
        musicPlayer.setVolume(0, fadeDuration: 1.2)
        let expectedPlayerID = ObjectIdentifier(musicPlayer)

        Task { @MainActor [weak self] in
            try? await Task.sleep(nanoseconds: 1_250_000_000)
            guard let self,
                  let musicPlayer = self.musicPlayer,
                  ObjectIdentifier(musicPlayer) == expectedPlayerID else {
                return
            }
            musicPlayer.pause()
            musicPlayer.volume = targetVolume
            currentTime = musicPlayer.currentTime
            isPlaying = false
        }
    }

    func stop() {
        musicPlayer?.stop()
        musicPlayer = nil
        stopAllEffects()
        isPlaying = false
        currentTime = 0
        duration = 0
        releaseScopedResource()
    }

    func seek(to time: Double) {
        musicPlayer?.currentTime = time
        currentTime = time
    }

    func nextTrack() {
        guard let playlist = playbackMusicPlaylist, !playlist.tracks.isEmpty else { return }

        if isShuffleEnabled {
            playRandomTrack(from: playlist)
            return
        }

        guard let currentTrackID = currentTrackID,
              let currentIndex = playlist.tracks.firstIndex(where: { $0.id == currentTrackID }) else {
            self.currentTrackID = playlist.tracks.first?.id
            playCurrentTrack()
            return
        }

        let nextIndex = currentIndex + 1

        if nextIndex < playlist.tracks.count {
            switchToTrack(playlist.tracks[nextIndex].id, addCurrentTrackToHistory: true)
        } else {
            switch repeatMode {
            case .off:
                stop()

            case .one:
                playCurrentTrack()

            case .all:
                if let firstID = playlist.tracks.first?.id {
                    switchToTrack(firstID, addCurrentTrackToHistory: true)
                }
            }
        }
    }

    func previousTrack() {
        guard let playlist = playbackMusicPlaylist, !playlist.tracks.isEmpty else { return }

        if let musicPlayer = musicPlayer, musicPlayer.currentTime > 3 {
            musicPlayer.currentTime = 0
            currentTime = 0
            return
        }

        if isShuffleEnabled, let previousID = playbackHistory.popLast() {
            currentTrackID = previousID
            playCurrentTrack()
            return
        }

        guard let currentTrackID = currentTrackID,
              let currentIndex = playlist.tracks.firstIndex(where: { $0.id == currentTrackID }) else {
            self.currentTrackID = playlist.tracks.first?.id
            playCurrentTrack()
            return
        }

        let previousIndex = currentIndex - 1

        if previousIndex >= 0 {
            switchToTrack(playlist.tracks[previousIndex].id, addCurrentTrackToHistory: false)
        } else {
            switch repeatMode {
            case .off, .one:
                self.currentTrackID = playlist.tracks.first?.id
                playCurrentTrack()

            case .all:
                if let lastID = playlist.tracks.last?.id {
                    switchToTrack(lastID, addCurrentTrackToHistory: false)
                }
            }
        }
    }

    // MARK: - Управление эффектами

    func playEffect(_ track: Track) {
        do {
            let (playbackURL, hasScope) = try resolveEffectPlaybackURL(for: track)
            let effectPlayer = try AVAudioPlayer(contentsOf: playbackURL)
            effectPlayer.delegate = self
            effectPlayer.volume = Float(track.outputVolume(masterVolume: effectsVolume))
            effectPlayer.prepareToPlay()

            let key = ObjectIdentifier(effectPlayer)
            effectPlayers[key] = effectPlayer
            effectScopedResources[key] = (playbackURL, hasScope)
            effectVolumeMultipliers[key] = track.volumeMultiplier

            beginDuckingIfNeeded()
            effectPlayer.play()
            errorMessage = nil
        } catch {
            errorMessage = L10n.tr("error.play_effect", track.title, error.localizedDescription)
        }
    }

    func playEffect(playlistID: UUID, trackID: UUID) -> Bool {
        guard let playlistIndex = effectPlaylists.firstIndex(where: { $0.id == playlistID }),
              let track = effectPlaylists[playlistIndex].effects.first(where: { $0.id == trackID }) else {
            return false
        }
        playEffect(track)
        return true
    }

    func playEffectAtIndex(_ index: Int) {
        guard index >= 0, index < effectTracks.count else { return }
        playEffect(effectTracks[index])
    }

    func stopEffects() {
        stopAllEffects()
    }

    func adjustMusicVolume(by delta: Double) {
        volume = clampedUnitVolume(volume + delta)
    }

    func adjustEffectsVolume(by delta: Double) {
        effectsVolume = clampedUnitVolume(effectsVolume + delta)
    }

    // MARK: - AVAudioPlayerDelegate

    func audioPlayerDidFinishPlaying(_ player: AVAudioPlayer, successfully flag: Bool) {
        let effectKey = ObjectIdentifier(player)
        if effectPlayers[effectKey] != nil {
            effectPlayers[effectKey] = nil
            releaseEffectScopedResource(for: effectKey)
            effectVolumeMultipliers[effectKey] = nil
            endDuckingIfNeeded()
            return
        }

        switch repeatMode {
        case .one:
            player.currentTime = 0
            player.play()
            isPlaying = true

        case .off, .all:
            nextTrack()
        }
    }

    // MARK: - Внутренняя логика

    private func playRandomTrack(from playlist: Playlist) {
        guard !playlist.tracks.isEmpty else { return }

        if playlist.tracks.count == 1 {
            currentTrackID = playlist.tracks[0].id
            playCurrentTrack()
            return
        }

        let currentID = currentTrackID
        let candidates = playlist.tracks.filter { $0.id != currentID }

        guard let nextTrack = candidates.randomElement() else { return }
        switchToTrack(nextTrack.id, addCurrentTrackToHistory: true)
    }

    private func switchToTrack(_ newTrackID: UUID, addCurrentTrackToHistory: Bool) {
        if addCurrentTrackToHistory,
           let currentTrackID = currentTrackID,
           currentTrackID != newTrackID {
            playbackHistory.append(currentTrackID)
        }

        currentTrackID = newTrackID
        playCurrentTrack()
    }

    private func appendUniqueMusicTracks(_ newTracks: [Track]) {
        guard let playlistIndex = selectedMusicPlaylistIndex else { return }

        let existingKeys = Set(musicPlaylists[playlistIndex].tracks.map(trackIdentityKey))
        let filteredTracks = newTracks.filter { !existingKeys.contains(trackIdentityKey($0)) }
        let duplicates = newTracks.filter { existingKeys.contains(trackIdentityKey($0)) }

        guard !filteredTracks.isEmpty else {
            errorMessage = L10n.tr("error.no_new_audio_files")
            publishImportConflictSummary(
                target: L10n.tr("sidebar.music_playlists"),
                attemptedCount: newTracks.count,
                addedCount: 0,
                duplicates: duplicates
            )
            return
        }

        musicPlaylists[playlistIndex].tracks.append(contentsOf: filteredTracks)
        saveState()
        publishImportConflictSummary(
            target: L10n.tr("sidebar.music_playlists"),
            attemptedCount: newTracks.count,
            addedCount: filteredTracks.count,
            duplicates: duplicates
        )

        if currentTrackID == nil, let first = musicPlaylists[playlistIndex].tracks.first {
            playbackMusicPlaylistID = selectedMusicPlaylistID
            currentTrackID = first.id
        }
    }

    private func appendUniqueEffectTracks(_ newTracks: [Track]) {
        guard let playlistIndex = selectedEffectPlaylistIndex else { return }

        let existingKeys = Set(effectPlaylists[playlistIndex].effects.map(trackIdentityKey))
        let filteredTracks = newTracks.filter { !existingKeys.contains(trackIdentityKey($0)) }
        let duplicates = newTracks.filter { existingKeys.contains(trackIdentityKey($0)) }

        guard !filteredTracks.isEmpty else {
            errorMessage = L10n.tr("error.no_new_audio_files")
            publishImportConflictSummary(
                target: L10n.tr("sidebar.sfx_playlists"),
                attemptedCount: newTracks.count,
                addedCount: 0,
                duplicates: duplicates
            )
            return
        }

        effectPlaylists[playlistIndex].effects.append(contentsOf: filteredTracks)
        saveState()
        publishImportConflictSummary(
            target: L10n.tr("sidebar.sfx_playlists"),
            attemptedCount: newTracks.count,
            addedCount: filteredTracks.count,
            duplicates: duplicates
        )
    }

    private func publishImportConflictSummary(
        target: String,
        attemptedCount: Int,
        addedCount: Int,
        duplicates: [Track]
    ) {
        guard !duplicates.isEmpty else {
            importConflictSummary = nil
            return
        }

        importConflictSummary = ImportConflictSummary(
            target: target,
            attemptedCount: attemptedCount,
            addedCount: addedCount,
            duplicateCount: duplicates.count,
            duplicateTitles: Array(duplicates.prefix(8)).map(\.title)
        )
        AppTelemetry.shared.warning(
            "Import duplicates skipped",
            metadata: [
                "target": target,
                "attempted": "\(attemptedCount)",
                "added": "\(addedCount)",
                "duplicates": "\(duplicates.count)"
            ]
        )
    }

    private func makeTrackWithSecurityScope(from url: URL, role: TrackRole) -> Track {
        let didStartAccessing = url.startAccessingSecurityScopedResource()
        defer {
            if didStartAccessing {
                url.stopAccessingSecurityScopedResource()
            }
        }

        return makeTrack(from: url, role: role)
    }

    private func makeTrack(from url: URL, role: TrackRole) -> Track {
        let bookmarkData = try? url.bookmarkData(
            options: [.withSecurityScope, .securityScopeAllowOnlyReadAccess],
            includingResourceValuesForKeys: nil,
            relativeTo: nil
        )

        return Track(
            title: url.deletingPathExtension().lastPathComponent,
            path: url.path,
            role: role,
            bookmarkData: bookmarkData
        )
    }

    private func trackIdentityKey(_ track: Track) -> String {
        if let bookmarkData = track.bookmarkData {
            var isStale = false
            if let resolvedURL = try? URL(
                resolvingBookmarkData: bookmarkData,
                options: [.withoutUI, .withoutMounting],
                relativeTo: nil,
                bookmarkDataIsStale: &isStale
            ) {
                return resolvedURL.standardizedFileURL.path.lowercased()
            }
        }
        return URL(fileURLWithPath: track.path).standardizedFileURL.path.lowercased()
    }

    private nonisolated static func scanTrackURLs(in folderURL: URL, supportedExtensions: Set<String>) -> [URL] {
        let fileManager = FileManager.default

        guard let enumerator = fileManager.enumerator(
            at: folderURL,
            includingPropertiesForKeys: [.isRegularFileKey],
            options: [.skipsHiddenFiles]
        ) else {
            return []
        }

        var urls: [URL] = []

        for case let fileURL as URL in enumerator {
            let fileExtension = fileURL.pathExtension.lowercased()
            guard supportedExtensions.contains(fileExtension) else { continue }
            urls.append(fileURL)
        }

        return urls.sorted {
            $0.lastPathComponent.localizedCaseInsensitiveCompare($1.lastPathComponent) == .orderedAscending
        }
    }

    private func resolvePlaybackURL(for track: Track) throws -> URL {
        if let bookmarkData = track.bookmarkData {
            var isStale = false

            let resolvedURL = try URL(
                resolvingBookmarkData: bookmarkData,
                options: [.withSecurityScope],
                relativeTo: nil,
                bookmarkDataIsStale: &isStale
            )

            let didStartAccessing = resolvedURL.startAccessingSecurityScopedResource()
            activeScopedURL = resolvedURL
            hasActiveSecurityScope = didStartAccessing

            guard didStartAccessing else {
                throw NSError(
                    domain: NSCocoaErrorDomain,
                    code: NSFileReadNoPermissionError,
                    userInfo: [NSLocalizedDescriptionKey: L10n.tr("error.no_file_access")]
                )
            }

            if isStale {
                // Обновляем устаревший bookmark, чтобы сохранить доступ после перезапуска приложения.
                refreshBookmark(for: track.id, with: resolvedURL)
            }

            return resolvedURL
        }

        return track.url
    }

    private func resolveEffectPlaybackURL(for track: Track) throws -> (url: URL, hasScope: Bool) {
        if let bookmarkData = track.bookmarkData {
            var isStale = false

            let resolvedURL = try URL(
                resolvingBookmarkData: bookmarkData,
                options: [.withSecurityScope],
                relativeTo: nil,
                bookmarkDataIsStale: &isStale
            )

            let didStartAccessing = resolvedURL.startAccessingSecurityScopedResource()
            guard didStartAccessing else {
                throw NSError(
                    domain: NSCocoaErrorDomain,
                    code: NSFileReadNoPermissionError,
                    userInfo: [NSLocalizedDescriptionKey: L10n.tr("error.no_effect_access")]
                )
            }

            if isStale {
                refreshBookmark(for: track.id, with: resolvedURL)
            }

            return (resolvedURL, true)
        }

        return (track.url, false)
    }

    private func releaseScopedResource() {
        if hasActiveSecurityScope, let activeScopedURL = activeScopedURL {
            activeScopedURL.stopAccessingSecurityScopedResource()
        }

        activeScopedURL = nil
        hasActiveSecurityScope = false
    }

    private func refreshBookmark(for trackID: UUID, with url: URL) {
        do {
            let newBookmarkData = try url.bookmarkData(
                options: [.withSecurityScope, .securityScopeAllowOnlyReadAccess],
                includingResourceValuesForKeys: nil,
                relativeTo: nil
            )

            if let musicPlaylistIndex = musicPlaylists.firstIndex(where: { playlist in
                playlist.tracks.contains(where: { $0.id == trackID })
            }),
            let musicTrackIndex = musicPlaylists[musicPlaylistIndex].tracks.firstIndex(where: { $0.id == trackID }) {
                musicPlaylists[musicPlaylistIndex].tracks[musicTrackIndex].bookmarkData = newBookmarkData
                saveState()
                return
            }

            if let effectPlaylistIndex = effectPlaylists.firstIndex(where: { playlist in
                playlist.effects.contains(where: { $0.id == trackID })
            }),
            let effectTrackIndex = effectPlaylists[effectPlaylistIndex].effects.firstIndex(where: { $0.id == trackID }) {
                effectPlaylists[effectPlaylistIndex].effects[effectTrackIndex].bookmarkData = newBookmarkData
                saveState()
            }
        } catch {
            // Не критично.
        }
    }

    private func startTimer() {
        timer = Timer.scheduledTimer(
            timeInterval: 0.25,
            target: self,
            selector: #selector(updatePlaybackTimer),
            userInfo: nil,
            repeats: true
        )
    }

    @objc private func updatePlaybackTimer() {
        guard let musicPlayer else {
            currentTime = 0
            duration = 0
            return
        }

        currentTime = musicPlayer.currentTime
        duration = musicPlayer.duration
    }

    private func stopAllEffects() {
        // Явно останавливаем и освобождаем каждый SFX-плеер, чтобы не копить ресурсы.
        for (key, effectPlayer) in effectPlayers {
            effectPlayer.stop()
            releaseEffectScopedResource(for: key)
        }

        effectPlayers.removeAll()
        effectVolumeMultipliers.removeAll()
        activeDuckCount = 0
        applyMusicVolume(animated: true)
    }

    private func releaseEffectScopedResource(for key: ObjectIdentifier) {
        guard let resource = effectScopedResources[key] else { return }
        if resource.hasScope {
            resource.url.stopAccessingSecurityScopedResource()
        }
        effectScopedResources[key] = nil
    }

    private func beginDuckingIfNeeded() {
        // Счётчик нужен, чтобы корректно обрабатывать несколько одновременных SFX.
        activeDuckCount += 1
        applyMusicVolume(animated: true)
    }

    private func endDuckingIfNeeded() {
        activeDuckCount = max(0, activeDuckCount - 1)
        applyMusicVolume(animated: true)
    }

    private func effectiveMusicVolume() -> Double {
        let activeDucking = activeDuckCount > 0 ? duckingAmount : 1
        return Track.outputVolume(
            masterVolume: volume,
            trackMultiplier: currentTrack?.volumeMultiplier ?? Track.defaultVolumeMultiplier,
            duckingMultiplier: activeDucking
        )
    }

    private func applyMusicVolume(animated: Bool) {
        guard let musicPlayer else { return }
        let targetVolume = Float(effectiveMusicVolume())
        if animated {
            musicPlayer.setVolume(targetVolume, fadeDuration: 0.18)
        } else {
            musicPlayer.volume = targetVolume
        }
    }

    private func applyEffectsVolume() {
        for (key, player) in effectPlayers {
            player.volume = Float(
                Track.outputVolume(
                    masterVolume: effectsVolume,
                    trackMultiplier: effectVolumeMultipliers[key] ?? Track.defaultVolumeMultiplier
                )
            )
        }
    }

    private func nextMusicPlaylistName() -> String {
        var number = 1
        let existingNames = Set(musicPlaylists.map { $0.name })

        while existingNames.contains(L10n.tr("playlist.default.number", number)) {
            number += 1
        }

        return L10n.tr("playlist.default.number", number)
    }

    private func nextEffectPlaylistName() -> String {
        var number = 1
        let existingNames = Set(effectPlaylists.map { $0.name })

        while existingNames.contains(L10n.tr("sfx.default.number", number)) {
            number += 1
        }

        return L10n.tr("sfx.default.number", number)
    }

    private func normalizedColumnsCount(_ value: Int) -> Int {
        if [2, 3, 4].contains(value) {
            return value
        }
        return 3
    }

    // MARK: - Сохранение / загрузка

    private func saveState() {
        do {
            let musicData = try JSONEncoder().encode(musicPlaylists)
            let effectData = try JSONEncoder().encode(effectPlaylists)
            let defaults = UserDefaults.standard
            defaults.set(musicData, forKey: PlayerDefaultsKeys.musicPlaylists)
            defaults.set(effectData, forKey: PlayerDefaultsKeys.effectPlaylists)
        } catch {
            errorMessage = L10n.tr("error.save_playlists")
        }
    }

    private func loadState() {
        let defaults = UserDefaults.standard

        if !defaults.bool(forKey: PlayerDefaultsKeys.migrationCompleted),
           let legacyData = defaults.data(forKey: PlayerDefaultsKeys.legacyPlaylists),
           let legacyPlaylists = try? JSONDecoder().decode([Playlist].self, from: legacyData) {
            // Однократная миграция с legacy-структуры на разделённые music/sfx плейлисты.
            migrateLegacyPlaylists(legacyPlaylists)
            defaults.set(true, forKey: PlayerDefaultsKeys.migrationCompleted)
            saveState()
            return
        }

        if let musicData = defaults.data(forKey: PlayerDefaultsKeys.musicPlaylists) {
            do {
                musicPlaylists = try JSONDecoder().decode([Playlist].self, from: musicData)
            } catch {
                musicPlaylists = []
                errorMessage = L10n.tr("error.load_music_playlists")
            }
        }

        if let effectData = defaults.data(forKey: PlayerDefaultsKeys.effectPlaylists) {
            do {
                effectPlaylists = try JSONDecoder().decode([EffectPlaylist].self, from: effectData)
            } catch {
                effectPlaylists = []
                errorMessage = L10n.tr("error.load_sfx_playlists")
            }
        }
    }

    private func migrateLegacyPlaylists(_ legacyPlaylists: [Playlist]) {
        let defaults = UserDefaults.standard
        let legacySelectedID = defaults
            .string(forKey: PlayerDefaultsKeys.legacySelectedPlaylistID)
            .flatMap(UUID.init(uuidString:))
        let migrated = PlaylistMigration.migrateLegacyPlaylists(
            legacyPlaylists,
            legacySelectedID: legacySelectedID,
            defaultMusicPlaylistName: L10n.tr("playlist.main.default"),
            defaultSFXPlaylistName: L10n.tr("playlist.sfx_master.default")
        )

        musicPlaylists = migrated.musicPlaylists
        effectPlaylists = migrated.effectPlaylists
        selectedMusicPlaylistID = migrated.selectedMusicPlaylistID
        selectedEffectPlaylistID = migrated.selectedEffectPlaylistID
    }

    private func ensureDefaultsAfterLoading() {
        var didChange = false

        if musicPlaylists.isEmpty {
            musicPlaylists = [Playlist(name: L10n.tr("playlist.main.default"))]
            didChange = true
        }

        if effectPlaylists.isEmpty {
            effectPlaylists = [EffectPlaylist(name: L10n.tr("playlist.sfx_master.default"))]
            didChange = true
        }

        if selectedMusicPlaylist == nil {
            selectedMusicPlaylistID = musicPlaylists.first?.id
            didChange = true
        }

        if selectedEffectPlaylist == nil {
            selectedEffectPlaylistID = effectPlaylists.first?.id
            didChange = true
        }

        if currentTrackID == nil {
            playbackMusicPlaylistID = selectedMusicPlaylistID
            currentTrackID = selectedMusicPlaylist?.tracks.first?.id
        }

        if didChange {
            saveState()
        }
    }

    private func savePreferences() {
        let defaults = UserDefaults.standard
        defaults.set(volume, forKey: PlayerDefaultsKeys.volume)
        defaults.set(effectsVolume, forKey: PlayerDefaultsKeys.effectsVolume)
        defaults.set(repeatMode.rawValue, forKey: PlayerDefaultsKeys.repeatMode)
        defaults.set(isShuffleEnabled, forKey: PlayerDefaultsKeys.shuffleEnabled)
        defaults.set(isMusicFadeOutOnPauseEnabled, forKey: PlayerDefaultsKeys.musicFadeOutOnPauseEnabled)
        defaults.set(selectedMusicPlaylistID?.uuidString, forKey: PlayerDefaultsKeys.selectedMusicPlaylistID)
        defaults.set(selectedEffectPlaylistID?.uuidString, forKey: PlayerDefaultsKeys.selectedEffectPlaylistID)
        defaults.set(duckingAmount, forKey: PlayerDefaultsKeys.duckingAmount)
        defaults.set(musicColumnsCount, forKey: PlayerDefaultsKeys.musicColumns)
        defaults.set(effectsColumnsCount, forKey: PlayerDefaultsKeys.effectsColumns)
        defaults.set(isSentryTelemetryEnabled, forKey: PlayerDefaultsKeys.sentryEnabled)
        defaults.set(sentryDSN, forKey: PlayerDefaultsKeys.sentryDSN)
    }

    private func loadPreferences() {
        let defaults = UserDefaults.standard
        isHydratingPreferences = true
        defer { isHydratingPreferences = false }

        if defaults.object(forKey: PlayerDefaultsKeys.volume) != nil {
            volume = defaults.double(forKey: PlayerDefaultsKeys.volume)
        }

        if defaults.object(forKey: PlayerDefaultsKeys.effectsVolume) != nil {
            effectsVolume = defaults.double(forKey: PlayerDefaultsKeys.effectsVolume)
        }

        if let rawRepeatMode = defaults.string(forKey: PlayerDefaultsKeys.repeatMode),
           let savedRepeatMode = RepeatMode.fromStoredValue(rawRepeatMode) {
            repeatMode = savedRepeatMode
        }

        if defaults.object(forKey: PlayerDefaultsKeys.shuffleEnabled) != nil {
            isShuffleEnabled = defaults.bool(forKey: PlayerDefaultsKeys.shuffleEnabled)
        }

        if defaults.object(forKey: PlayerDefaultsKeys.musicFadeOutOnPauseEnabled) != nil {
            isMusicFadeOutOnPauseEnabled = defaults.bool(forKey: PlayerDefaultsKeys.musicFadeOutOnPauseEnabled)
        }

        if defaults.object(forKey: PlayerDefaultsKeys.duckingAmount) != nil {
            duckingAmount = defaults.double(forKey: PlayerDefaultsKeys.duckingAmount)
        }

        if defaults.object(forKey: PlayerDefaultsKeys.musicColumns) != nil {
            musicColumnsCount = normalizedColumnsCount(defaults.integer(forKey: PlayerDefaultsKeys.musicColumns))
        }

        if defaults.object(forKey: PlayerDefaultsKeys.effectsColumns) != nil {
            effectsColumnsCount = normalizedColumnsCount(defaults.integer(forKey: PlayerDefaultsKeys.effectsColumns))
        }

        if let savedMusicPlaylistIDString = defaults.string(forKey: PlayerDefaultsKeys.selectedMusicPlaylistID),
           let savedMusicPlaylistID = UUID(uuidString: savedMusicPlaylistIDString),
           musicPlaylists.contains(where: { $0.id == savedMusicPlaylistID }) {
            selectedMusicPlaylistID = savedMusicPlaylistID
        }

        if let savedEffectPlaylistIDString = defaults.string(forKey: PlayerDefaultsKeys.selectedEffectPlaylistID),
           let savedEffectPlaylistID = UUID(uuidString: savedEffectPlaylistIDString),
           effectPlaylists.contains(where: { $0.id == savedEffectPlaylistID }) {
            selectedEffectPlaylistID = savedEffectPlaylistID
        }

        isSentryTelemetryEnabled = defaults.bool(forKey: PlayerDefaultsKeys.sentryEnabled)
        sentryDSN = defaults.string(forKey: PlayerDefaultsKeys.sentryDSN) ?? ""
    }

    private func clampedUnitVolume(_ value: Double) -> Double {
        guard value.isFinite else { return 0.8 }
        return max(0, min(value, 1))
    }

    private func clampedDuckingAmount(_ value: Double) -> Double {
        guard value.isFinite else { return 0.55 }
        return min(max(value, 0.2), 1.0)
    }
}
