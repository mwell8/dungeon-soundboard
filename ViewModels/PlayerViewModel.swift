import Foundation
import Combine
import AppKit
import UniformTypeIdentifiers

/// Главная логика плеера.
/// Разделяет музыкальные и SFX-плейлисты и управляет воспроизведением.
@MainActor
final class PlayerViewModel: NSObject, ObservableObject, AudioPlayerAdapterDelegate {
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
            if oldValue != isShuffleEnabled {
                rebuildShuffleDeck()
            }
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
            telemetry.error(
                errorMessage,
                metadata: [
                    "music_playlists": "\(musicPlaylists.count)",
                    "effect_playlists": "\(effectPlaylists.count)"
                ]
            )
        }
    }

    @Published var importConflictSummary: ImportConflictSummary?
    @Published private(set) var activeEffectCount: Int = 0
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

    private final class EffectVoice {
        let player: any AudioPlayerAdapter
        let lease: FileAccessLease
        let volumeMultiplier: Double

        init(player: any AudioPlayerAdapter, lease: FileAccessLease, volumeMultiplier: Double) {
            self.player = player
            self.lease = lease
            self.volumeMultiplier = volumeMultiplier
        }
    }

    private let defaults: UserDefaults
    private let audioPlayerFactory: any AudioPlayerFactory
    private let fileAccessResolver: any FileAccessResolving
    private let playbackScheduler: any PlaybackScheduling
    private let telemetry: any TelemetryReporting

    private var musicPlayer: (any AudioPlayerAdapter)?
    private var musicFileLease: FileAccessLease?
    // Для наложения SFX используем несколько плееров одновременно (polyphony).
    private var effectVoices: [ObjectIdentifier: EffectVoice] = [:]
    private var playbackProgressTask: Task<Void, Never>?
    private var pauseFadeTask: Task<Void, Never>?
    private var playbackHistory: [MusicTrackReference] = []
    private var shuffleDeck: ShuffleDeck?
    private var activeDuckCount: Int = 0
    // Во время загрузки настроек отключаем лишние savePreferences() из didSet.
    private var isHydratingPreferences: Bool = false
    private var needsNormalizedStateSave = false
    private var pendingMigrationCompletion = false

    // MARK: - Поддерживаемые расширения

    private let supportedExtensions: Set<String> = [
        "mp3", "wav", "aiff", "aif", "m4a", "aac", "caf", "mp4"
    ]

    private enum PlaylistTarget: Equatable {
        case music(UUID)
        case effect(UUID)
    }

    // MARK: - Инициализация

    override convenience init() {
        self.init(
            defaults: .standard,
            audioPlayerFactory: SystemAudioPlayerFactory(),
            fileAccessResolver: PersistentFileAccessResolver(),
            playbackScheduler: TaskPlaybackScheduler(),
            telemetry: AppTelemetry.shared
        )
    }

    init(
        defaults: UserDefaults,
        audioPlayerFactory: any AudioPlayerFactory,
        fileAccessResolver: any FileAccessResolving,
        playbackScheduler: any PlaybackScheduling = TaskPlaybackScheduler(),
        telemetry: any TelemetryReporting
    ) {
        self.defaults = defaults
        self.audioPlayerFactory = audioPlayerFactory
        self.fileAccessResolver = fileAccessResolver
        self.playbackScheduler = playbackScheduler
        self.telemetry = telemetry
        super.init()

        isHydratingPreferences = true
        loadState()
        loadPreferences()
        ensureDefaultsAfterLoading()
        isHydratingPreferences = false
        if needsNormalizedStateSave {
            let didSave = saveState()
            if didSave, pendingMigrationCompletion {
                defaults.set(true, forKey: PlayerDefaultsKeys.migrationCompleted)
            }
        }
        savePreferences()
        startPlaybackProgressUpdates()
    }

    deinit {
        playbackProgressTask?.cancel()
        pauseFadeTask?.cancel()
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
        activeEffectCount > 0
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

        if wasPlaybackContext {
            stopMusic()
            currentTrackID = nil
            playbackMusicPlaylistID = nil
        }

        playbackHistory.removeAll { $0.playlistID == deletedPlaylist.id }

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
        let playlist = musicPlaylists[playlistIndex]
        guard !playlist.tracks.isEmpty else { return }
        var deck = ShuffleDeck(playlistID: playlist.id, trackIDs: playlist.tracks.map(\.id))
        guard let first = deck.drawNext(
            repeatMode: .off,
            availableTrackIDs: playlist.tracks.map(\.id),
            currentTrackID: nil
        ) else { return }
        guard startMusic(
            reference: first,
            addCurrentToHistory: true,
            resetShuffleDeck: false
        ) else { return }

        selectedMusicPlaylistID = playlist.id
        isShuffleEnabled = true
        // didSet создаёт новую колоду; возвращаем локальную, из которой первый ID уже извлечён.
        shuffleDeck = deck
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
        guard let selectedMusicPlaylistID else { return }
        addFilesFromFinder(role: .music, target: .music(selectedMusicPlaylistID))
    }

    func addEffectsFromFinder() {
        guard let selectedEffectPlaylistID else { return }
        addFilesFromFinder(role: .effect, target: .effect(selectedEffectPlaylistID))
    }

    func addMusicFolderFromFinder() {
        guard let selectedMusicPlaylistID else { return }
        addFolderFromFinder(target: .music(selectedMusicPlaylistID))
    }

    func addEffectsFolderFromFinder() {
        guard let selectedEffectPlaylistID else { return }
        addFolderFromFinder(target: .effect(selectedEffectPlaylistID))
    }

    func importDroppedMusicURLs(_ urls: [URL]) {
        guard let selectedMusicPlaylistID else { return }
        importDroppedMusicURLs(urls, to: selectedMusicPlaylistID)
    }

    func importDroppedEffectURLs(_ urls: [URL]) {
        guard let selectedEffectPlaylistID else { return }
        importDroppedEffectURLs(urls, to: selectedEffectPlaylistID)
    }

    func importDroppedMusicURLs(_ urls: [URL], to playlistID: UUID) {
        collectAndImportTrackURLs(urls, role: .music, target: .music(playlistID))
    }

    func importDroppedEffectURLs(_ urls: [URL], to playlistID: UUID) {
        collectAndImportTrackURLs(urls, role: .effect, target: .effect(playlistID))
    }

    private func addFilesFromFinder(role: TrackRole, target: PlaylistTarget) {
        guard containsPlaylist(target) else { return }

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
        guard containsPlaylist(target) else { return }

        let panel = NSOpenPanel()
        panel.title = L10n.tr("import.folder.title")
        panel.message = L10n.tr("import.folder.message")
        panel.allowsMultipleSelection = false
        panel.canChooseDirectories = true
        panel.canChooseFiles = false

        if panel.runModal() == .OK, let folderURL = panel.url {
            let role: TrackRole
            switch target {
            case .music: role = .music
            case .effect: role = .effect
            }
            let supportedExtensions = self.supportedExtensions
            Task { @MainActor [weak self] in
                let urls = await Task.detached(priority: .userInitiated) {
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

        let newTracks = collected.map { makeTrack(from: $0, role: role) }
        switch target {
        case .music(let playlistID):
            appendUniqueMusicTracks(newTracks, to: playlistID)
        case .effect(let playlistID):
            appendUniqueEffectTracks(newTracks, to: playlistID)
        }
    }

    private func collectAndImportTrackURLs(_ urls: [URL], role: TrackRole, target: PlaylistTarget) {
        guard containsPlaylist(target) else { return }
        let supportedExtensions = self.supportedExtensions
        Task { @MainActor [weak self] in
            let collected = await Task.detached(priority: .userInitiated) {
                var result: [URL] = []
                for url in urls {
                    var isDirectory: ObjCBool = false
                    if FileManager.default.fileExists(atPath: url.path, isDirectory: &isDirectory), isDirectory.boolValue {
                        result.append(contentsOf: Self.scanTrackURLs(in: url, supportedExtensions: supportedExtensions))
                    } else if supportedExtensions.contains(url.pathExtension.lowercased()) {
                        result.append(url)
                    }
                }
                return result
            }.value
            self?.importTrackURLs(collected, role: role, target: target)
        }
    }

    private func containsPlaylist(_ target: PlaylistTarget) -> Bool {
        switch target {
        case .music(let playlistID):
            return musicPlaylists.contains { $0.id == playlistID }
        case .effect(let playlistID):
            return effectPlaylists.contains { $0.id == playlistID }
        }
    }

    func removeMusicTrack(_ track: Track) {
        guard let playlistIndex = selectedMusicPlaylistIndex else { return }
        guard let trackIndex = musicPlaylists[playlistIndex].tracks.firstIndex(where: { $0.id == track.id }) else {
            return
        }

        let removedTrack = musicPlaylists[playlistIndex].tracks.remove(at: trackIndex)
        let playlistID = musicPlaylists[playlistIndex].id
        playbackHistory.removeAll {
            $0.playlistID == playlistID && $0.trackID == removedTrack.id
        }

        if playbackMusicPlaylistID == musicPlaylists[playlistIndex].id, currentTrackID == removedTrack.id {
            stopMusic()
            let remaining = musicPlaylists[playlistIndex].tracks
            let replacementIndex = min(trackIndex, max(0, remaining.count - 1))
            currentTrackID = remaining.indices.contains(replacementIndex) ? remaining[replacementIndex].id : nil
        }

        reconcilePlaybackCollections()
        saveState()
    }

    func removeMusicTracks(_ trackIDs: Set<UUID>) {
        guard !trackIDs.isEmpty else { return }
        guard let playlistIndex = selectedMusicPlaylistIndex else { return }

        let playlistID = musicPlaylists[playlistIndex].id
        let currentIndex = currentTrackID.flatMap { id in
            musicPlaylists[playlistIndex].tracks.firstIndex { $0.id == id }
        }
        let removedCurrent = playbackMusicPlaylistID == playlistID
            && (currentTrackID.map(trackIDs.contains) ?? false)
        musicPlaylists[playlistIndex].tracks.removeAll { trackIDs.contains($0.id) }
        playbackHistory.removeAll {
            $0.playlistID == playlistID && trackIDs.contains($0.trackID)
        }

        if removedCurrent {
            stopMusic()
            playbackMusicPlaylistID = playlistID
            let replacementIndex = min(
                currentIndex ?? 0,
                max(0, musicPlaylists[playlistIndex].tracks.count - 1)
            )
            let remaining = musicPlaylists[playlistIndex].tracks
            currentTrackID = remaining.indices.contains(replacementIndex) ? remaining[replacementIndex].id : nil
        }
        reconcilePlaybackCollections()
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
        if playbackMusicPlaylistID == playlistID, currentTrackID == trackID {
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
        guard let selectedMusicPlaylistID else { return }
        _ = startMusic(
            reference: MusicTrackReference(playlistID: selectedMusicPlaylistID, trackID: track.id),
            addCurrentToHistory: true
        )
    }

    func playMusicTrack(playlistID: UUID, trackID: UUID) -> Bool {
        let reference = MusicTrackReference(playlistID: playlistID, trackID: trackID)
        guard track(for: reference) != nil else { return false }
        _ = startMusic(reference: reference, addCurrentToHistory: true)
        return true
    }

    func playCurrentTrack() {
        guard let reference = currentMusicReference else { return }
        _ = startMusic(reference: reference, addCurrentToHistory: false)
    }

    func playPause() {
        if pauseFadeTask != nil {
            pauseFadeTask?.cancel()
            pauseFadeTask = nil
            applyMusicVolume(animated: false)
            isPlaying = musicPlayer != nil
            return
        }

        if isPlaying {
            if isMusicFadeOutOnPauseEnabled {
                pauseWithFadeOut()
            } else {
                pause()
            }
        } else if let musicPlayer {
            applyMusicVolume(animated: false)
            if musicPlayer.play() {
                isPlaying = true
                errorMessage = nil
            }
        } else {
            if let reference = currentMusicReference {
                _ = startMusic(reference: reference, addCurrentToHistory: false)
            } else if let playlistID = selectedMusicPlaylistID,
                      let first = selectedMusicPlaylist?.tracks.first {
                _ = startMusic(
                    reference: MusicTrackReference(playlistID: playlistID, trackID: first.id),
                    addCurrentToHistory: false
                )
            }
        }
    }

    func pause() {
        pauseFadeTask?.cancel()
        pauseFadeTask = nil
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
        let expectedPlayerID = ObjectIdentifier(musicPlayer as AnyObject)

        pauseFadeTask?.cancel()
        pauseFadeTask = Task { @MainActor [weak self] in
            try? await Task.sleep(nanoseconds: 1_250_000_000)
            guard !Task.isCancelled,
                  let self,
                  let musicPlayer = self.musicPlayer,
                  ObjectIdentifier(musicPlayer as AnyObject) == expectedPlayerID else {
                return
            }
            musicPlayer.pause()
            musicPlayer.volume = targetVolume
            currentTime = musicPlayer.currentTime
            isPlaying = false
            pauseFadeTask = nil
        }
    }

    func stopMusic() {
        pauseFadeTask?.cancel()
        pauseFadeTask = nil
        musicPlayer?.eventDelegate = nil
        musicPlayer?.stop()
        musicPlayer = nil
        musicFileLease?.close()
        musicFileLease = nil
        isPlaying = false
        currentTime = 0
        duration = 0
    }

    func stopEffects() {
        stopAllEffects()
    }

    func stopAll() {
        stopMusic()
        stopEffects()
    }

    /// Backward-compatible alias for the existing Stop All call sites.
    func stop() {
        stopAll()
    }

    func seek(to time: Double) {
        musicPlayer?.currentTime = time
        currentTime = time
    }

    func nextTrack() {
        advanceToNextTrack()
    }

    private func advanceToNextTrack() {
        guard let playlist = playbackMusicPlaylist, !playlist.tracks.isEmpty else {
            stopMusic()
            return
        }

        if isShuffleEnabled {
            advanceShuffled(in: playlist)
            return
        }

        guard let currentTrackID = currentTrackID,
              let currentIndex = playlist.tracks.firstIndex(where: { $0.id == currentTrackID }) else {
            guard let firstID = playlist.tracks.first?.id else { return }
            _ = startMusic(
                reference: MusicTrackReference(playlistID: playlist.id, trackID: firstID),
                addCurrentToHistory: true
            )
            return
        }

        let nextIndex = currentIndex + 1

        if nextIndex < playlist.tracks.count {
            _ = startMusic(
                reference: MusicTrackReference(playlistID: playlist.id, trackID: playlist.tracks[nextIndex].id),
                addCurrentToHistory: true
            )
        } else {
            switch repeatMode {
            case .off, .one:
                stopMusic()

            case .all:
                if let firstID = playlist.tracks.first?.id {
                    _ = startMusic(
                        reference: MusicTrackReference(playlistID: playlist.id, trackID: firstID),
                        addCurrentToHistory: true
                    )
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

        while let stale = playbackHistory.last, track(for: stale) == nil {
            playbackHistory.removeLast()
        }

        if let previous = playbackHistory.last,
           isShuffleEnabled || previous.playlistID != playlist.id {
            if startMusic(reference: previous, addCurrentToHistory: false) {
                playbackHistory.removeLast()
            }
            return
        }

        guard let currentTrackID = currentTrackID,
              let currentIndex = playlist.tracks.firstIndex(where: { $0.id == currentTrackID }) else {
            guard let firstID = playlist.tracks.first?.id else { return }
            _ = startMusic(
                reference: MusicTrackReference(playlistID: playlist.id, trackID: firstID),
                addCurrentToHistory: false
            )
            return
        }

        let previousIndex = currentIndex - 1

        if previousIndex >= 0 {
            _ = startMusic(
                reference: MusicTrackReference(playlistID: playlist.id, trackID: playlist.tracks[previousIndex].id),
                addCurrentToHistory: false
            )
        } else {
            switch repeatMode {
            case .off, .one:
                musicPlayer?.currentTime = 0
                currentTime = 0

            case .all:
                if let lastID = playlist.tracks.last?.id {
                    _ = startMusic(
                        reference: MusicTrackReference(playlistID: playlist.id, trackID: lastID),
                        addCurrentToHistory: false
                    )
                }
            }
        }
    }

    // MARK: - Управление эффектами

    func playEffect(_ track: Track) {
        _ = startEffect(track)
    }

    @discardableResult
    private func startEffect(_ track: Track) -> Bool {
        var candidateLease: FileAccessLease?
        var candidatePlayer: (any AudioPlayerAdapter)?
        do {
            let lease = try fileAccessResolver.resolve(track: track)
            candidateLease = lease
            let player = try audioPlayerFactory.makePlayer(url: lease.url)
            candidatePlayer = player
            player.eventDelegate = self
            player.volume = Float(track.outputVolume(masterVolume: effectsVolume))
            guard player.prepareToPlay(), player.play() else {
                throw CocoaError(.fileReadCorruptFile)
            }

            refreshFileReference(
                for: track.id,
                resolvedURL: lease.url,
                refreshedBookmarkData: lease.refreshedBookmarkData
            )
            let key = ObjectIdentifier(player as AnyObject)
            effectVoices[key] = EffectVoice(player: player, lease: lease, volumeMultiplier: track.volumeMultiplier)
            activeEffectCount = effectVoices.count
            beginDuckingIfNeeded()
            errorMessage = nil
            return true
        } catch {
            cleanupEffectResources(player: candidatePlayer, lease: candidateLease)
            errorMessage = L10n.tr("error.play_effect", track.title, error.localizedDescription)
            return false
        }
    }

    func playEffect(playlistID: UUID, trackID: UUID) -> Bool {
        guard let playlistIndex = effectPlaylists.firstIndex(where: { $0.id == playlistID }),
              let track = effectPlaylists[playlistIndex].effects.first(where: { $0.id == trackID }) else {
            return false
        }
        // Bool сообщает вызывающему коду, что playlist/track-пара существует.
        // Временный audio-отказ не должен удалять валидный хоткей этой пары.
        _ = startEffect(track)
        return true
    }

    func playEffectAtIndex(_ index: Int) {
        guard index >= 0, index < effectTracks.count else { return }
        playEffect(effectTracks[index])
    }

    func adjustMusicVolume(by delta: Double) {
        volume = clampedUnitVolume(volume + delta)
    }

    func adjustEffectsVolume(by delta: Double) {
        effectsVolume = clampedUnitVolume(effectsVolume + delta)
    }

    // MARK: - AudioPlayerAdapterDelegate

    func audioPlayerAdapterDidFinish(_ player: any AudioPlayerAdapter, successfully flag: Bool) {
        let key = ObjectIdentifier(player as AnyObject)
        if effectVoices[key] != nil {
            cleanupEffectVoice(for: key)
            return
        }

        guard let musicPlayer,
              ObjectIdentifier(musicPlayer as AnyObject) == key else { return }
        guard flag else {
            stopMusic()
            return
        }
        switch repeatMode {
        case .one:
            player.currentTime = 0
            if player.play() {
                isPlaying = true
            } else {
                stopMusic()
            }

        case .off, .all:
            advanceToNextTrack()
        }
    }

    func audioPlayerAdapter(_ player: any AudioPlayerAdapter, decodeError error: Error?) {
        let key = ObjectIdentifier(player as AnyObject)
        if effectVoices[key] != nil {
            cleanupEffectVoice(for: key)
            return
        }

        guard let musicPlayer,
              ObjectIdentifier(musicPlayer as AnyObject) == key else { return }
        let title = currentTrack?.title ?? L10n.tr("player.nothing_playing")
        stopMusic()
        errorMessage = L10n.tr("error.play_file", title, error?.localizedDescription ?? L10n.tr("error.unknown"))
    }

    // MARK: - Внутренняя логика

    private var currentMusicReference: MusicTrackReference? {
        guard let playlistID = playbackMusicPlaylistID ?? selectedMusicPlaylistID,
              let currentTrackID else { return nil }
        return MusicTrackReference(playlistID: playlistID, trackID: currentTrackID)
    }

    private func track(for reference: MusicTrackReference) -> Track? {
        musicPlaylists
            .first { $0.id == reference.playlistID }?
            .tracks
            .first { $0.id == reference.trackID }
    }

    @discardableResult
    private func startMusic(
        reference: MusicTrackReference,
        addCurrentToHistory: Bool,
        resetShuffleDeck: Bool = true
    ) -> Bool {
        guard let targetTrack = track(for: reference) else { return false }

        var candidateLease: FileAccessLease?
        var candidatePlayer: (any AudioPlayerAdapter)?
        do {
            let lease = try fileAccessResolver.resolve(track: targetTrack)
            candidateLease = lease
            let player = try audioPlayerFactory.makePlayer(url: lease.url)
            candidatePlayer = player
            player.eventDelegate = self
            player.volume = Float(effectiveMusicVolume(for: targetTrack))
            guard player.prepareToPlay(), player.play() else {
                throw CocoaError(.fileReadCorruptFile)
            }

            let previousReference = currentMusicReference
            let oldPlayer = musicPlayer
            let oldLease = musicFileLease

            pauseFadeTask?.cancel()
            pauseFadeTask = nil
            musicPlayer = player
            musicFileLease = lease
            playbackMusicPlaylistID = reference.playlistID
            currentTrackID = reference.trackID
            duration = player.duration
            currentTime = player.currentTime
            isPlaying = true

            if addCurrentToHistory,
               oldPlayer != nil,
               let previousReference,
               previousReference != reference,
               track(for: previousReference) != nil {
                playbackHistory.append(previousReference)
            }
            if resetShuffleDeck {
                rebuildShuffleDeck()
            }
            refreshFileReference(
                for: targetTrack.id,
                resolvedURL: lease.url,
                refreshedBookmarkData: lease.refreshedBookmarkData
            )

            oldPlayer?.eventDelegate = nil
            oldPlayer?.stop()
            oldLease?.close()
            errorMessage = nil
            return true
        } catch {
            candidatePlayer?.eventDelegate = nil
            candidatePlayer?.stop()
            candidateLease?.close()
            errorMessage = L10n.tr("error.play_file", targetTrack.title, error.localizedDescription)
            return false
        }
    }

    private func rebuildShuffleDeck() {
        guard isShuffleEnabled, let playlist = playbackMusicPlaylist else {
            shuffleDeck = nil
            return
        }
        shuffleDeck = ShuffleDeck(
            playlistID: playlist.id,
            trackIDs: playlist.tracks.map(\.id),
            excluding: currentTrackID
        )
    }

    private func reconcilePlaybackCollections() {
        playbackHistory.removeAll { track(for: $0) == nil }

        if let reference = currentMusicReference, track(for: reference) == nil {
            stopMusic()
            currentTrackID = nil
            playbackMusicPlaylistID = nil
        }

        guard isShuffleEnabled, let playlist = playbackMusicPlaylist else {
            shuffleDeck = nil
            return
        }
        if shuffleDeck?.playlistID == playlist.id {
            shuffleDeck?.reconcile(availableTrackIDs: playlist.tracks.map(\.id))
        } else {
            rebuildShuffleDeck()
        }
    }

    private func advanceShuffled(in playlist: Playlist) {
        if shuffleDeck?.playlistID != playlist.id {
            shuffleDeck = ShuffleDeck(
                playlistID: playlist.id,
                trackIDs: playlist.tracks.map(\.id),
                excluding: currentTrackID
            )
        }
        let effectiveRepeatMode: RepeatMode = repeatMode == .one ? .off : repeatMode
        guard let next = shuffleDeck?.drawNext(
            repeatMode: effectiveRepeatMode,
            availableTrackIDs: playlist.tracks.map(\.id),
            currentTrackID: currentTrackID
        ) else {
            stopMusic()
            return
        }
        _ = startMusic(
            reference: next,
            addCurrentToHistory: true,
            resetShuffleDeck: false
        )
    }

    // MARK: - Перенос и импорт треков

    @discardableResult
    func transferMusicTracks(
        ids: Set<UUID>,
        from sourcePlaylistID: UUID,
        to destinationPlaylistID: UUID,
        operation: TrackTransferOperation
    ) -> TrackTransferResult {
        guard !ids.isEmpty else {
            return emptyTransferResult(
                ids: ids,
                role: .music,
                from: sourcePlaylistID,
                to: destinationPlaylistID,
                operation: operation
            )
        }
        guard let result = musicPlaylists.transferMusicTracks(
            ids,
            from: sourcePlaylistID,
            to: destinationPlaylistID,
            operation: operation
        ) else {
            return emptyTransferResult(
                ids: ids,
                role: .music,
                from: sourcePlaylistID,
                to: destinationPlaylistID,
                operation: operation
            )
        }

        if operation == .move {
            let movedIDs = Set(result.transferredSourceTrackIDs)
            playbackHistory = playbackHistory.map { reference in
                guard reference.playlistID == sourcePlaylistID,
                      movedIDs.contains(reference.trackID) else {
                    return reference
                }
                return MusicTrackReference(
                    playlistID: destinationPlaylistID,
                    trackID: reference.trackID
                )
            }

            if playbackMusicPlaylistID == sourcePlaylistID,
               let currentTrackID,
               movedIDs.contains(currentTrackID) {
                // Плеер и lease продолжают жить: меняется только логический playback-контекст.
                playbackMusicPlaylistID = destinationPlaylistID
            }
        }

        reconcilePlaybackCollections()
        saveState()
        publishTransferConflicts(result)
        return result
    }

    @discardableResult
    func transferEffectTracks(
        ids: Set<UUID>,
        from sourcePlaylistID: UUID,
        to destinationPlaylistID: UUID,
        operation: TrackTransferOperation
    ) -> TrackTransferResult {
        guard !ids.isEmpty else {
            return emptyTransferResult(
                ids: ids,
                role: .effect,
                from: sourcePlaylistID,
                to: destinationPlaylistID,
                operation: operation
            )
        }
        guard let result = effectPlaylists.transferEffectTracks(
            ids,
            from: sourcePlaylistID,
            to: destinationPlaylistID,
            operation: operation
        ) else {
            return emptyTransferResult(
                ids: ids,
                role: .effect,
                from: sourcePlaylistID,
                to: destinationPlaylistID,
                operation: operation
            )
        }

        // Уже запущенные SFX-войсы не привязаны к положению карточки и продолжают играть.
        saveState()
        publishTransferConflicts(result)
        return result
    }

    private func emptyTransferResult(
        ids: Set<UUID>,
        role: TrackRole,
        from sourcePlaylistID: UUID,
        to destinationPlaylistID: UUID,
        operation: TrackTransferOperation
    ) -> TrackTransferResult {
        TrackTransferResult(
            operation: operation,
            role: role,
            sourcePlaylistID: sourcePlaylistID,
            destinationPlaylistID: destinationPlaylistID,
            transferred: [],
            duplicateTrackIDs: [],
            missingTrackIDs: ids.sorted { $0.uuidString < $1.uuidString }
        )
    }

    func reorderMusicTracks(ids: Set<UUID>, in playlistID: UUID, relativeTo targetTrackID: UUID) {
        guard musicPlaylists.reorderMusicTracks(ids, in: playlistID, to: targetTrackID) else { return }
        reconcilePlaybackCollections()
        saveState()
    }

    func reorderEffectTracks(ids: Set<UUID>, in playlistID: UUID, relativeTo targetTrackID: UUID) {
        guard effectPlaylists.reorderEffectTracks(ids, in: playlistID, to: targetTrackID) else { return }
        saveState()
    }

    func reorderMusicTracksToEnd(ids: Set<UUID>, in playlistID: UUID) {
        guard musicPlaylists.reorderMusicTracksToEnd(ids, in: playlistID) else { return }
        reconcilePlaybackCollections()
        saveState()
    }

    func reorderEffectTracksToEnd(ids: Set<UUID>, in playlistID: UUID) {
        guard effectPlaylists.reorderEffectTracksToEnd(ids, in: playlistID) else { return }
        saveState()
    }

    private func publishTransferConflicts(_ result: TrackTransferResult) {
        guard result.hasPartialConflicts else {
            importConflictSummary = nil
            return
        }

        let duplicateTitles: [String]
        switch result.role {
        case .music:
            duplicateTitles = musicPlaylists
                .first { $0.id == result.sourcePlaylistID }?
                .tracks
                .filter { result.duplicateTrackIDs.contains($0.id) }
                .map(\.title) ?? []
        case .effect:
            duplicateTitles = effectPlaylists
                .first { $0.id == result.sourcePlaylistID }?
                .effects
                .filter { result.duplicateTrackIDs.contains($0.id) }
                .map(\.title) ?? []
        }

        importConflictSummary = ImportConflictSummary(
            target: result.role == .music
                ? L10n.tr("sidebar.music_playlists")
                : L10n.tr("sidebar.sfx_playlists"),
            attemptedCount: result.attemptedCount,
            addedCount: result.transferredCount,
            duplicateCount: result.duplicateCount + result.missingTrackIDs.count,
            duplicateTitles: Array(duplicateTitles.prefix(8))
        )
    }

    private func appendUniqueMusicTracks(_ newTracks: [Track], to playlistID: UUID) {
        guard let playlistIndex = musicPlaylists.firstIndex(where: { $0.id == playlistID }) else { return }

        var seen = Set(musicPlaylists[playlistIndex].tracks.map(trackIdentityKey))
        var filteredTracks: [Track] = []
        var duplicates: [Track] = []
        for track in newTracks {
            if seen.insert(trackIdentityKey(track)).inserted {
                filteredTracks.append(track)
            } else {
                duplicates.append(track)
            }
        }

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

        if currentTrackID == nil,
           selectedMusicPlaylistID == playlistID,
           let first = musicPlaylists[playlistIndex].tracks.first {
            playbackMusicPlaylistID = playlistID
            currentTrackID = first.id
        }
    }

    private func appendUniqueEffectTracks(_ newTracks: [Track], to playlistID: UUID) {
        guard let playlistIndex = effectPlaylists.firstIndex(where: { $0.id == playlistID }) else { return }

        var seen = Set(effectPlaylists[playlistIndex].effects.map(trackIdentityKey))
        var filteredTracks: [Track] = []
        var duplicates: [Track] = []
        for track in newTracks {
            if seen.insert(trackIdentityKey(track)).inserted {
                filteredTracks.append(track)
            } else {
                duplicates.append(track)
            }
        }

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
        telemetry.warning(
            "Import duplicates skipped",
            metadata: [
                "target": target,
                "attempted": "\(attemptedCount)",
                "added": "\(addedCount)",
                "duplicates": "\(duplicates.count)"
            ]
        )
    }

    private func makeTrack(from url: URL, role: TrackRole) -> Track {
        let standardizedURL = url.standardizedFileURL
        let bookmarkData = fileAccessResolver.bookmarkData(for: standardizedURL)

        return Track(
            title: standardizedURL.deletingPathExtension().lastPathComponent,
            path: standardizedURL.path,
            role: role,
            bookmarkData: bookmarkData
        )
    }

    private func trackIdentityKey(_ track: Track) -> String {
        TrackFileIdentity.key(for: track)
    }

    private nonisolated static func scanTrackURLs(in folderURL: URL, supportedExtensions: Set<String>) -> [URL] {
        let fileManager = FileManager.default

        guard let enumerator = fileManager.enumerator(
            at: folderURL,
            includingPropertiesForKeys: [.isDirectoryKey],
            options: [.skipsHiddenFiles]
        ) else {
            return []
        }

        var urls: [URL] = []

        for case let fileURL as URL in enumerator {
            guard (try? fileURL.resourceValues(forKeys: [.isDirectoryKey]).isDirectory) != true else {
                continue
            }
            let fileExtension = fileURL.pathExtension.lowercased()
            guard supportedExtensions.contains(fileExtension) else { continue }
            urls.append(fileURL)
        }

        return urls.sorted { lhs, rhs in
            let nameOrder = lhs.lastPathComponent.localizedCaseInsensitiveCompare(rhs.lastPathComponent)
            if nameOrder != .orderedSame {
                return nameOrder == .orderedAscending
            }
            return lhs.standardizedFileURL.path.compare(
                rhs.standardizedFileURL.path,
                options: [.caseInsensitive, .literal]
            ) == .orderedAscending
        }
    }

    private func refreshFileReference(
        for trackID: UUID,
        resolvedURL: URL,
        refreshedBookmarkData: Data?
    ) {
        let normalizedPath = resolvedURL.standardizedFileURL.path
        if let musicPlaylistIndex = musicPlaylists.firstIndex(where: { playlist in
            playlist.tracks.contains(where: { $0.id == trackID })
        }),
        let musicTrackIndex = musicPlaylists[musicPlaylistIndex].tracks.firstIndex(where: { $0.id == trackID }) {
            var didChange = false
            if musicPlaylists[musicPlaylistIndex].tracks[musicTrackIndex].path != normalizedPath {
                musicPlaylists[musicPlaylistIndex].tracks[musicTrackIndex].path = normalizedPath
                didChange = true
            }
            if let refreshedBookmarkData,
               musicPlaylists[musicPlaylistIndex].tracks[musicTrackIndex].bookmarkData != refreshedBookmarkData {
                musicPlaylists[musicPlaylistIndex].tracks[musicTrackIndex].bookmarkData = refreshedBookmarkData
                didChange = true
            }
            if didChange { saveState() }
            return
        }

        if let effectPlaylistIndex = effectPlaylists.firstIndex(where: { playlist in
            playlist.effects.contains(where: { $0.id == trackID })
        }),
        let effectTrackIndex = effectPlaylists[effectPlaylistIndex].effects.firstIndex(where: { $0.id == trackID }) {
            var didChange = false
            if effectPlaylists[effectPlaylistIndex].effects[effectTrackIndex].path != normalizedPath {
                effectPlaylists[effectPlaylistIndex].effects[effectTrackIndex].path = normalizedPath
                didChange = true
            }
            if let refreshedBookmarkData,
               effectPlaylists[effectPlaylistIndex].effects[effectTrackIndex].bookmarkData != refreshedBookmarkData {
                effectPlaylists[effectPlaylistIndex].effects[effectTrackIndex].bookmarkData = refreshedBookmarkData
                didChange = true
            }
            if didChange { saveState() }
        }
    }

    private func startPlaybackProgressUpdates() {
        playbackProgressTask?.cancel()
        playbackProgressTask = playbackScheduler.scheduleRepeating(
            everyNanoseconds: 250_000_000
        ) { [weak self] in
            self?.updatePlaybackProgress()
        }
    }

    private func updatePlaybackProgress() {
        guard let musicPlayer else {
            currentTime = 0
            duration = 0
            return
        }

        currentTime = musicPlayer.currentTime
        duration = musicPlayer.duration
    }

    private func stopAllEffects() {
        for voice in effectVoices.values {
            cleanupEffectResources(player: voice.player, lease: voice.lease)
        }
        effectVoices.removeAll()
        activeEffectCount = 0
        activeDuckCount = 0
        applyMusicVolume(animated: true)
    }

    private func cleanupEffectVoice(for key: ObjectIdentifier) {
        guard let voice = effectVoices.removeValue(forKey: key) else { return }
        cleanupEffectResources(player: voice.player, lease: voice.lease)
        activeEffectCount = effectVoices.count
        endDuckingIfNeeded()
    }

    private func cleanupEffectResources(
        player: (any AudioPlayerAdapter)?,
        lease: FileAccessLease?
    ) {
        player?.eventDelegate = nil
        player?.stop()
        lease?.close()
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

    private func effectiveMusicVolume(for track: Track? = nil) -> Double {
        let activeDucking = activeDuckCount > 0 ? duckingAmount : 1
        return Track.outputVolume(
            masterVolume: volume,
            trackMultiplier: track?.volumeMultiplier
                ?? currentTrack?.volumeMultiplier
                ?? Track.defaultVolumeMultiplier,
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
        for voice in effectVoices.values {
            voice.player.volume = Float(
                Track.outputVolume(
                    masterVolume: effectsVolume,
                    trackMultiplier: voice.volumeMultiplier
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

    @discardableResult
    private func saveState() -> Bool {
        do {
            let musicData = try JSONEncoder().encode(musicPlaylists)
            let effectData = try JSONEncoder().encode(effectPlaylists)
            defaults.set(musicData, forKey: PlayerDefaultsKeys.musicPlaylists)
            defaults.set(effectData, forKey: PlayerDefaultsKeys.effectPlaylists)
            return true
        } catch {
            errorMessage = L10n.tr("error.save_playlists")
            return false
        }
    }

    private func loadState() {
        if !defaults.bool(forKey: PlayerDefaultsKeys.migrationCompleted),
           let legacyData = defaults.data(forKey: PlayerDefaultsKeys.legacyPlaylists),
           let legacyPlaylists = try? JSONDecoder().decode([Playlist].self, from: legacyData) {
            // Однократная миграция с legacy-структуры на разделённые music/sfx плейлисты.
            migrateLegacyPlaylists(legacyPlaylists)
            pendingMigrationCompletion = true
            needsNormalizedStateSave = true
            return
        }

        if let musicData = defaults.data(forKey: PlayerDefaultsKeys.musicPlaylists) {
            do {
                musicPlaylists = try JSONDecoder().decode([Playlist].self, from: musicData)
            } catch {
                musicPlaylists = []
                errorMessage = L10n.tr("error.load_music_playlists")
                needsNormalizedStateSave = true
            }
        }

        if let effectData = defaults.data(forKey: PlayerDefaultsKeys.effectPlaylists) {
            do {
                effectPlaylists = try JSONDecoder().decode([EffectPlaylist].self, from: effectData)
            } catch {
                effectPlaylists = []
                errorMessage = L10n.tr("error.load_sfx_playlists")
                needsNormalizedStateSave = true
            }
        }
    }

    private func migrateLegacyPlaylists(_ legacyPlaylists: [Playlist]) {
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
            needsNormalizedStateSave = true
        }
    }

    private func savePreferences() {
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
