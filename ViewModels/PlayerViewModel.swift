import Foundation
import Combine
import AppKit
import AVFAudio
import UniformTypeIdentifiers

/// Главная логика плеера.
/// Разделяет музыкальные и SFX-плейлисты и управляет воспроизведением.
final class PlayerViewModel: NSObject, ObservableObject, AVAudioPlayerDelegate {
    enum PlaylistEditorTarget: Equatable {
        case music(UUID)
        case effect(UUID)
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

    @Published var currentTime: TimeInterval = 0
    @Published var duration: TimeInterval = 0
    @Published var errorMessage: String?

    @Published var duckingAmount: Double = 0.55 {
        didSet {
            // Ducking ограничен безопасным диапазоном, чтобы музыка не "исчезала" полностью.
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

    @Published var activePlaylistEditorTarget: PlaylistEditorTarget?

    // MARK: - Внутренние свойства

    private var musicPlayer: AVAudioPlayer?
    // Для наложения SFX используем несколько плееров одновременно (polyphony).
    private var effectPlayers: [ObjectIdentifier: AVAudioPlayer] = [:]
    private var effectScopedResources: [ObjectIdentifier: (url: URL, hasScope: Bool)] = [:]
    private var timer: Timer?
    private var playbackHistory: [UUID] = []
    private var activeDuckCount: Int = 0

    private var activeScopedURL: URL?
    private var hasActiveSecurityScope: Bool = false
    // Во время загрузки настроек отключаем лишние savePreferences() из didSet.
    private var isHydratingPreferences: Bool = false

    // MARK: - Ключи UserDefaults

    private let legacyPlaylistsKey = "macos_dungeon_soundboard_playlists"
    private let migrationCompletedKey = "macos_dungeon_soundboard_migration_v2_completed"

    private let musicPlaylistsKey = "macos_dungeon_soundboard_music_playlists"
    private let effectPlaylistsKey = "macos_dungeon_soundboard_effect_playlists"

    private let volumeKey = "macos_dungeon_soundboard_volume"
    private let effectsVolumeKey = "macos_dungeon_soundboard_effects_volume"
    private let repeatModeKey = "macos_dungeon_soundboard_repeat_mode"
    private let shuffleKey = "macos_dungeon_soundboard_shuffle_enabled"
    private let selectedMusicPlaylistKey = "macos_dungeon_soundboard_selected_music_playlist_id"
    private let selectedEffectPlaylistKey = "macos_dungeon_soundboard_selected_effect_playlist_id"
    private let legacySelectedPlaylistKey = "macos_dungeon_soundboard_selected_playlist_id"
    private let duckingAmountKey = "macos_dungeon_soundboard_ducking_amount"
    private let musicColumnsKey = "macos_dungeon_soundboard_music_columns"
    private let effectsColumnsKey = "macos_dungeon_soundboard_effects_columns"

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
        stopAllEffects()
        releaseScopedResource()
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

    var selectedEffectPlaylist: EffectPlaylist? {
        guard let index = selectedEffectPlaylistIndex else { return nil }
        return effectPlaylists[index]
    }

    var currentTrack: Track? {
        guard let playlist = selectedMusicPlaylist, let currentTrackID else { return nil }
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

    var activePlaylistDisplayName: String {
        guard let target = activePlaylistEditorTarget else { return "" }
        switch target {
        case .music(let id):
            return musicPlaylists.first(where: { $0.id == id })?.name ?? ""
        case .effect(let id):
            return effectPlaylists.first(where: { $0.id == id })?.name ?? ""
        }
    }

    // MARK: - Музыкальные плейлисты

    func createMusicPlaylist() {
        let playlist = Playlist(name: nextMusicPlaylistName())
        musicPlaylists.append(playlist)
        selectedMusicPlaylistID = playlist.id
        activePlaylistEditorTarget = .music(playlist.id)
        saveState()
    }

    func renameSelectedMusicPlaylist(to newName: String) {
        guard let index = selectedMusicPlaylistIndex else { return }

        let trimmed = newName.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else { return }

        musicPlaylists[index].name = trimmed
        activePlaylistEditorTarget = .music(musicPlaylists[index].id)
        saveState()
    }

    func deleteSelectedMusicPlaylist() {
        guard let index = selectedMusicPlaylistIndex else { return }

        let deletedPlaylist = musicPlaylists[index]

        if selectedMusicPlaylistID == deletedPlaylist.id {
            stop()
            currentTrackID = nil
        }

        let deletedIDs = Set(deletedPlaylist.tracks.map { $0.id })
        playbackHistory.removeAll { deletedIDs.contains($0) }

        musicPlaylists.remove(at: index)

        if musicPlaylists.isEmpty {
            let playlist = Playlist(name: L10n.tr("playlist.main.default"))
            musicPlaylists = [playlist]
            selectedMusicPlaylistID = playlist.id
            activePlaylistEditorTarget = .music(playlist.id)
        } else {
            selectedMusicPlaylistID = musicPlaylists.first?.id
            if let id = selectedMusicPlaylistID {
                activePlaylistEditorTarget = .music(id)
            }
        }

        saveState()
    }

    func playMusicPlaylistShuffled(_ playlist: Playlist) {
        guard let playlistIndex = musicPlaylists.firstIndex(where: { $0.id == playlist.id }) else { return }

        selectedMusicPlaylistID = playlist.id
        activePlaylistEditorTarget = .music(playlist.id)
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
        activePlaylistEditorTarget = .effect(playlist.id)
        saveState()
    }

    func renameSelectedEffectPlaylist(to newName: String) {
        guard let index = selectedEffectPlaylistIndex else { return }

        let trimmed = newName.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else { return }

        effectPlaylists[index].name = trimmed
        activePlaylistEditorTarget = .effect(effectPlaylists[index].id)
        saveState()
    }

    func deleteSelectedEffectPlaylist() {
        guard let index = selectedEffectPlaylistIndex else { return }

        let deletedPlaylist = effectPlaylists[index]
        let deletedIDs = Set(deletedPlaylist.effects.map { $0.id })

        playbackHistory.removeAll { deletedIDs.contains($0) }

        effectPlaylists.remove(at: index)

        if effectPlaylists.isEmpty {
            let playlist = EffectPlaylist(name: L10n.tr("playlist.sfx_master.default"))
            effectPlaylists = [playlist]
            selectedEffectPlaylistID = playlist.id
            activePlaylistEditorTarget = .effect(playlist.id)
        } else {
            selectedEffectPlaylistID = effectPlaylists.first?.id
            if let id = selectedEffectPlaylistID {
                activePlaylistEditorTarget = .effect(id)
            }
        }

        saveState()
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
            let newTracks = panel.urls.map { makeTrackWithSecurityScope(from: $0, role: role) }

            switch target {
            case .music:
                appendUniqueMusicTracks(newTracks)
            case .effect:
                appendUniqueEffectTracks(newTracks)
            }
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
            Task.detached(priority: .userInitiated) { [weak self] in
                guard let self else { return }
                // Security scope должен жить ровно столько, сколько длится фоновый обход папки.
                let didStartAccessing = folderURL.startAccessingSecurityScopedResource()
                defer {
                    if didStartAccessing {
                        folderURL.stopAccessingSecurityScopedResource()
                    }
                }
                let urls = Self.scanTrackURLs(in: folderURL, supportedExtensions: supportedExtensions)
                await MainActor.run {
                    let tracks = urls.map { self.makeTrackWithSecurityScope(from: $0, role: role) }
                    switch target {
                    case .music:
                        self.appendUniqueMusicTracks(tracks)
                    case .effect:
                        self.appendUniqueEffectTracks(tracks)
                    }
                }
            }
        }
    }

    func setActiveEditorTargetMusic(_ playlistID: UUID) {
        selectedMusicPlaylistID = playlistID
        activePlaylistEditorTarget = .music(playlistID)
    }

    func setActiveEditorTargetEffect(_ playlistID: UUID) {
        selectedEffectPlaylistID = playlistID
        activePlaylistEditorTarget = .effect(playlistID)
    }

    func renameActivePlaylist(to newName: String) {
        guard let target = activePlaylistEditorTarget else { return }
        switch target {
        case .music:
            renameSelectedMusicPlaylist(to: newName)
        case .effect:
            renameSelectedEffectPlaylist(to: newName)
        }
    }

    func deleteActivePlaylist() {
        guard let target = activePlaylistEditorTarget else { return }
        switch target {
        case .music:
            deleteSelectedMusicPlaylist()
        case .effect:
            deleteSelectedEffectPlaylist()
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

    func removeEffectTrack(_ track: Track) {
        guard let playlistIndex = selectedEffectPlaylistIndex else { return }
        guard let trackIndex = effectPlaylists[playlistIndex].effects.firstIndex(where: { $0.id == track.id }) else {
            return
        }

        effectPlaylists[playlistIndex].effects.remove(at: trackIndex)
        saveState()
    }

    // MARK: - Управление воспроизведением музыки

    func playMusicTrack(_ track: Track) {
        switchToTrack(track.id, addCurrentTrackToHistory: true)
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
            pause()
        } else if let musicPlayer = musicPlayer {
            musicPlayer.play()
            isPlaying = true
        } else {
            if currentTrack == nil, let first = selectedMusicPlaylist?.tracks.first {
                currentTrackID = first.id
            }
            playCurrentTrack()
        }
    }

    func pause() {
        musicPlayer?.pause()
        isPlaying = false
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
        guard let playlist = selectedMusicPlaylist, !playlist.tracks.isEmpty else { return }

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
        guard let playlist = selectedMusicPlaylist, !playlist.tracks.isEmpty else { return }

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
            effectPlayer.volume = Float(effectsVolume)
            effectPlayer.prepareToPlay()

            let key = ObjectIdentifier(effectPlayer)
            effectPlayers[key] = effectPlayer
            effectScopedResources[key] = (playbackURL, hasScope)

            beginDuckingIfNeeded()
            effectPlayer.play()
            errorMessage = nil
        } catch {
            errorMessage = L10n.tr("error.play_effect", track.title, error.localizedDescription)
        }
    }

    func stopEffects() {
        stopAllEffects()
    }

    func fadeOutMusic() {
        guard let musicPlayer else { return }
        musicPlayer.setVolume(0, fadeDuration: 1.2)
        DispatchQueue.main.asyncAfter(deadline: .now() + 1.25) { [weak self] in
            guard let self else { return }
            guard let currentPlayer = self.musicPlayer else { return }
            if currentPlayer.volume <= 0.01 {
                self.stop()
            }
        }
    }

    // MARK: - AVAudioPlayerDelegate

    func audioPlayerDidFinishPlaying(_ player: AVAudioPlayer, successfully flag: Bool) {
        let effectKey = ObjectIdentifier(player)
        if effectPlayers[effectKey] != nil {
            effectPlayers[effectKey] = nil
            releaseEffectScopedResource(for: effectKey)
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

        let existingPaths = Set(musicPlaylists[playlistIndex].tracks.map { $0.path })
        let filteredTracks = newTracks.filter { !existingPaths.contains($0.path) }

        guard !filteredTracks.isEmpty else {
            errorMessage = L10n.tr("error.no_new_audio_files")
            return
        }

        musicPlaylists[playlistIndex].tracks.append(contentsOf: filteredTracks)
        saveState()

        if currentTrackID == nil, let first = musicPlaylists[playlistIndex].tracks.first {
            currentTrackID = first.id
        }
    }

    private func appendUniqueEffectTracks(_ newTracks: [Track]) {
        guard let playlistIndex = selectedEffectPlaylistIndex else { return }

        let existingPaths = Set(effectPlaylists[playlistIndex].effects.map { $0.path })
        let filteredTracks = newTracks.filter { !existingPaths.contains($0.path) }

        guard !filteredTracks.isEmpty else {
            errorMessage = L10n.tr("error.no_new_audio_files")
            return
        }

        effectPlaylists[playlistIndex].effects.append(contentsOf: filteredTracks)
        saveState()
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
        timer = Timer.scheduledTimer(withTimeInterval: 0.25, repeats: true) { [weak self] _ in
            guard let self = self else { return }
            guard let musicPlayer = self.musicPlayer else {
                self.currentTime = 0
                self.duration = 0
                return
            }

            self.currentTime = musicPlayer.currentTime
            self.duration = musicPlayer.duration
        }
    }

    private func stopAllEffects() {
        // Явно останавливаем и освобождаем каждый SFX-плеер, чтобы не копить ресурсы.
        for (key, effectPlayer) in effectPlayers {
            effectPlayer.stop()
            releaseEffectScopedResource(for: key)
        }

        effectPlayers.removeAll()
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
        guard activeDuckCount > 0 else { return volume }
        return volume * duckingAmount
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
        let targetVolume = Float(effectsVolume)
        for player in effectPlayers.values {
            player.volume = targetVolume
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
            defaults.set(musicData, forKey: musicPlaylistsKey)
            defaults.set(effectData, forKey: effectPlaylistsKey)
        } catch {
            errorMessage = L10n.tr("error.save_playlists")
        }
    }

    private func loadState() {
        let defaults = UserDefaults.standard

        if !defaults.bool(forKey: migrationCompletedKey),
           let legacyData = defaults.data(forKey: legacyPlaylistsKey),
           let legacyPlaylists = try? JSONDecoder().decode([Playlist].self, from: legacyData) {
            // Однократная миграция с legacy-структуры на разделённые music/sfx плейлисты.
            migrateLegacyPlaylists(legacyPlaylists)
            defaults.set(true, forKey: migrationCompletedKey)
            saveState()
            return
        }

        if let musicData = defaults.data(forKey: musicPlaylistsKey) {
            do {
                musicPlaylists = try JSONDecoder().decode([Playlist].self, from: musicData)
            } catch {
                musicPlaylists = []
                errorMessage = L10n.tr("error.load_music_playlists")
            }
        }

        if let effectData = defaults.data(forKey: effectPlaylistsKey) {
            do {
                effectPlaylists = try JSONDecoder().decode([EffectPlaylist].self, from: effectData)
            } catch {
                effectPlaylists = []
                errorMessage = L10n.tr("error.load_sfx_playlists")
            }
        }
    }

    private func migrateLegacyPlaylists(_ legacyPlaylists: [Playlist]) {
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
            migratedMusicPlaylists = [Playlist(name: L10n.tr("playlist.main.default"))]
        }

        let deduplicatedEffects = deduplicateTracksByPath(collectedEffects)
        let effectPlaylist = EffectPlaylist(name: L10n.tr("playlist.sfx_master.default"), effects: deduplicatedEffects)

        musicPlaylists = migratedMusicPlaylists
        effectPlaylists = [effectPlaylist]

        let defaults = UserDefaults.standard
        if let legacySelectedIDString = defaults.string(forKey: legacySelectedPlaylistKey),
           let legacySelectedID = UUID(uuidString: legacySelectedIDString),
           musicPlaylists.contains(where: { $0.id == legacySelectedID }) {
            selectedMusicPlaylistID = legacySelectedID
        } else {
            selectedMusicPlaylistID = musicPlaylists.first?.id
        }

        selectedEffectPlaylistID = effectPlaylist.id
    }

    private func deduplicateTracksByPath(_ tracks: [Track]) -> [Track] {
        // Путь — стабильный ключ дедупликации при переносе старых данных.
        var seen = Set<String>()
        var result: [Track] = []

        for track in tracks {
            let key = track.path.lowercased()
            if seen.contains(key) { continue }
            seen.insert(key)
            result.append(track)
        }

        return result
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

        if activePlaylistEditorTarget == nil {
            if let selectedMusicPlaylistID {
                activePlaylistEditorTarget = .music(selectedMusicPlaylistID)
            } else if let selectedEffectPlaylistID {
                activePlaylistEditorTarget = .effect(selectedEffectPlaylistID)
            }
        }

        if currentTrackID == nil {
            currentTrackID = selectedMusicPlaylist?.tracks.first?.id
        }

        if didChange {
            saveState()
        }
    }

    private func savePreferences() {
        let defaults = UserDefaults.standard
        defaults.set(volume, forKey: volumeKey)
        defaults.set(effectsVolume, forKey: effectsVolumeKey)
        defaults.set(repeatMode.rawValue, forKey: repeatModeKey)
        defaults.set(isShuffleEnabled, forKey: shuffleKey)
        defaults.set(selectedMusicPlaylistID?.uuidString, forKey: selectedMusicPlaylistKey)
        defaults.set(selectedEffectPlaylistID?.uuidString, forKey: selectedEffectPlaylistKey)
        defaults.set(duckingAmount, forKey: duckingAmountKey)
        defaults.set(musicColumnsCount, forKey: musicColumnsKey)
        defaults.set(effectsColumnsCount, forKey: effectsColumnsKey)
    }

    private func loadPreferences() {
        let defaults = UserDefaults.standard
        isHydratingPreferences = true
        defer { isHydratingPreferences = false }

        if defaults.object(forKey: volumeKey) != nil {
            volume = defaults.double(forKey: volumeKey)
        }

        if defaults.object(forKey: effectsVolumeKey) != nil {
            effectsVolume = defaults.double(forKey: effectsVolumeKey)
        }

        if let rawRepeatMode = defaults.string(forKey: repeatModeKey),
           let savedRepeatMode = RepeatMode.fromStoredValue(rawRepeatMode) {
            repeatMode = savedRepeatMode
        }

        if defaults.object(forKey: shuffleKey) != nil {
            isShuffleEnabled = defaults.bool(forKey: shuffleKey)
        }

        if defaults.object(forKey: duckingAmountKey) != nil {
            duckingAmount = defaults.double(forKey: duckingAmountKey)
        }

        if defaults.object(forKey: musicColumnsKey) != nil {
            musicColumnsCount = normalizedColumnsCount(defaults.integer(forKey: musicColumnsKey))
        }

        if defaults.object(forKey: effectsColumnsKey) != nil {
            effectsColumnsCount = normalizedColumnsCount(defaults.integer(forKey: effectsColumnsKey))
        }

        if let savedMusicPlaylistIDString = defaults.string(forKey: selectedMusicPlaylistKey),
           let savedMusicPlaylistID = UUID(uuidString: savedMusicPlaylistIDString),
           musicPlaylists.contains(where: { $0.id == savedMusicPlaylistID }) {
            selectedMusicPlaylistID = savedMusicPlaylistID
        }

        if let savedEffectPlaylistIDString = defaults.string(forKey: selectedEffectPlaylistKey),
           let savedEffectPlaylistID = UUID(uuidString: savedEffectPlaylistIDString),
           effectPlaylists.contains(where: { $0.id == savedEffectPlaylistID }) {
            selectedEffectPlaylistID = savedEffectPlaylistID
        }

        if let selectedMusicPlaylistID {
            activePlaylistEditorTarget = .music(selectedMusicPlaylistID)
        } else if let selectedEffectPlaylistID {
            activePlaylistEditorTarget = .effect(selectedEffectPlaylistID)
        }
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
