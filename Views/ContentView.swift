import SwiftUI
import AppKit
import UniformTypeIdentifiers

/// Главный экран приложения.
/// Здесь только интерфейс и вызовы методов ViewModel.
struct ContentView: View {
    @StateObject private var vm = PlayerViewModel()

    @AppStorage(AppLanguage.userDefaultsKey) private var appLanguageRawValue: String = AppLanguage.defaultLanguage.rawValue
    @AppStorage("sidebar_music_pane_height") private var persistedMusicPaneHeight: Double = 220
    @AppStorage("main_music_pane_height") private var persistedMainMusicPaneHeight: Double = 260
    @State private var musicPaneHeight: CGFloat = 220
    @State private var mainMusicPaneHeight: CGFloat = 260
    @State private var playlistEditorName: String = ""
    @State private var isSettingsPresented: Bool = false
    @State private var isResizingSidebar: Bool = false
    @State private var isSidebarDividerHovered: Bool = false
    @State private var isResizingCenterDivider: Bool = false
    @State private var isCenterDividerHovered: Bool = false
    @State private var sidebarDragStartHeight: CGFloat?
    @State private var centerDividerDragStartHeight: CGFloat?
    @State private var isDeletePlaylistConfirmationPresented: Bool = false
    @State private var selectedMusicTrackIDs: Set<UUID> = []
    @State private var selectedEffectTrackIDs: Set<UUID> = []
    @State private var isMusicDropTargeted: Bool = false
    @State private var isEffectsDropTargeted: Bool = false

    var body: some View {
        ZStack {
            LinearGradient(
                colors: [DndTheme.backgroundTop, DndTheme.backgroundBottom],
                startPoint: .topLeading,
                endPoint: .bottomTrailing
            )
            .ignoresSafeArea()

            VStack(spacing: 0) {
                topBar
                    .padding(.horizontal, 12)
                    .padding(.top, 12)

                HStack(spacing: 0) {
                    sidebar
                        .frame(minWidth: 250, idealWidth: 270, maxWidth: 300)
                        .background(DndTheme.panel.opacity(0.92))

                    Divider()

                    centerArea
                        .frame(minWidth: 560)
                        .frame(maxWidth: .infinity, maxHeight: .infinity)
                        .background(DndTheme.panel.opacity(0.78))
                }

                Divider()

                playerBar
                    .padding(12)
                    .background(DndTheme.panelAlt.opacity(0.95))
            }
            .padding(12)
        }
        .frame(minWidth: 1080, minHeight: 680)
        .alert("error.title", isPresented: Binding(
            get: { vm.errorMessage != nil },
            set: { isPresented in
                if !isPresented {
                    vm.errorMessage = nil
                }
            }
        )) {
            Button("action.ok", role: .cancel) {
                vm.errorMessage = nil
            }
        } message: {
            Text(vm.errorMessage ?? L10n.tr("error.unknown"))
        }
        .sheet(isPresented: $isSettingsPresented) {
            settingsView
        }
        .alert(
            "import.conflicts.title",
            isPresented: Binding(
                get: { vm.importConflictSummary != nil },
                set: { isPresented in
                    if !isPresented {
                        vm.importConflictSummary = nil
                    }
                }
            )
        ) {
            Button("action.ok", role: .cancel) {
                vm.importConflictSummary = nil
            }
        } message: {
            if let summary = vm.importConflictSummary {
                Text(
                    L10n.tr(
                        "import.conflicts.message",
                        summary.target,
                        summary.attemptedCount,
                        summary.addedCount,
                        summary.duplicateCount
                    ) + "\n" + summary.duplicateTitles.joined(separator: ", ")
                )
            } else {
                Text("")
            }
        }
        .confirmationDialog(
            "playlist.delete.confirm.title",
            isPresented: $isDeletePlaylistConfirmationPresented,
            titleVisibility: .visible
        ) {
            Button("action.delete", role: .destructive) {
                vm.deleteActivePlaylist()
                playlistEditorName = vm.activePlaylistDisplayName
            }
            Button("action.cancel", role: .cancel) {}
        } message: {
            Text("playlist.delete.confirm.message")
        }
        .onAppear {
            playlistEditorName = vm.activePlaylistDisplayName
            musicPaneHeight = CGFloat(persistedMusicPaneHeight)
            mainMusicPaneHeight = CGFloat(persistedMainMusicPaneHeight)
        }
        .onChange(of: vm.activePlaylistEditorTarget) {
            playlistEditorName = vm.activePlaylistDisplayName
        }
        .onChange(of: vm.selectedMusicPlaylistID) {
            selectedMusicTrackIDs.removeAll()
        }
        .onChange(of: vm.selectedEffectPlaylistID) {
            selectedEffectTrackIDs.removeAll()
        }
        .onDeleteCommand {
            if !selectedMusicTrackIDs.isEmpty {
                vm.removeMusicTracks(selectedMusicTrackIDs)
                selectedMusicTrackIDs.removeAll()
            } else if !selectedEffectTrackIDs.isEmpty {
                vm.removeEffectTracks(selectedEffectTrackIDs)
                selectedEffectTrackIDs.removeAll()
            }
        }
        .overlay(alignment: .topLeading) {
            hotkeysProxy
                .frame(width: 0, height: 0)
                .allowsHitTesting(false)
                .opacity(0)
        }
    }

    // MARK: - Верхняя панель

    private var topBar: some View {
        HStack {
            Text("app.title")
                .font(.headline)
                .foregroundStyle(DndTheme.textPrimary)

            Spacer()

            Button {
                isSettingsPresented = true
            } label: {
                Image(systemName: "gearshape")
            }
            .help("settings.title")
            .buttonStyle(.bordered)
        }
    }

    // MARK: - Левая панель

    private var sidebar: some View {
        GeometryReader { geometry in
            // Высота блока редактора фиксирована для расчёта доступного пространства секций.
            let editorHeight: CGFloat = 108
            let dragHandleHeight: CGFloat = 10
            let sectionSpacing: CGFloat = 10
            let verticalPadding: CGFloat = 12
            let contentHeight = max(
                220,
                geometry.size.height - editorHeight - sectionSpacing - verticalPadding * 2
            )
            let minSectionHeight: CGFloat = 96
            let maxMusicHeight = max(minSectionHeight, contentHeight - minSectionHeight - dragHandleHeight)
            let musicHeight = clampedMusicPaneHeight(maxHeight: maxMusicHeight)
            let effectsHeight = max(minSectionHeight, contentHeight - musicHeight - dragHandleHeight)

            VStack(alignment: .leading, spacing: sectionSpacing) {
                musicPlaylistsSection
                    .frame(height: musicHeight)

                sidebarDivider(minHeight: minSectionHeight, maxHeight: maxMusicHeight)
                    .frame(height: dragHandleHeight)

                effectPlaylistsSection
                    .frame(height: effectsHeight)

                unifiedPlaylistEditor
            }
            .padding(.horizontal, 12)
            .padding(.vertical, verticalPadding)
        }
    }

    private var musicPlaylistsSection: some View {
        VStack(alignment: .leading, spacing: 8) {
            HStack {
                Text("sidebar.music_playlists")
                    .font(.headline)
                    .foregroundStyle(DndTheme.textPrimary)

                Spacer()

                Button {
                    vm.createMusicPlaylist()
                } label: {
                    Image(systemName: "plus")
                }
                .buttonStyle(.borderless)
                .foregroundStyle(DndTheme.accent)
            }

            List(selection: $vm.selectedMusicPlaylistID) {
                ForEach(vm.musicPlaylists) { playlist in
                    HStack(spacing: 8) {
                        Text(playlist.name)
                            .foregroundStyle(DndTheme.textPrimary)

                        Spacer()

                        Button {
                            vm.playMusicPlaylistShuffled(playlist)
                        } label: {
                            Image(systemName: "shuffle")
                        }
                        .buttonStyle(.borderless)
                        .foregroundStyle(DndTheme.accent)
                        .help("sidebar.shuffle_play")
                    }
                    .tag(playlist.id)
                    .contentShape(Rectangle())
                    .onTapGesture {
                        vm.setActiveEditorTargetMusic(playlist.id)
                    }
                }
            }
            .scrollContentBackground(.hidden)
        }
    }

    private var effectPlaylistsSection: some View {
        VStack(alignment: .leading, spacing: 8) {
            HStack {
                Text("sidebar.sfx_playlists")
                    .font(.headline)
                    .foregroundStyle(DndTheme.textPrimary)

                Spacer()

                Button {
                    vm.createEffectPlaylist()
                } label: {
                    Image(systemName: "plus")
                }
                .buttonStyle(.borderless)
                .foregroundStyle(DndTheme.accent)
            }

            List(selection: $vm.selectedEffectPlaylistID) {
                ForEach(vm.effectPlaylists) { playlist in
                    Text(playlist.name)
                        .foregroundStyle(DndTheme.textPrimary)
                        .tag(playlist.id)
                        .contentShape(Rectangle())
                        .onTapGesture {
                            vm.setActiveEditorTargetEffect(playlist.id)
                        }
                }
            }
            .scrollContentBackground(.hidden)
        }
    }

    private var unifiedPlaylistEditor: some View {
        VStack(alignment: .leading, spacing: 8) {
            Text("sidebar.playlist_editor")
                .font(.subheadline.weight(.semibold))
                .foregroundStyle(DndTheme.textPrimary)

            TextField("sidebar.playlist_name", text: $playlistEditorName)
                .textFieldStyle(.roundedBorder)

            HStack {
                Button("action.save") {
                    vm.renameActivePlaylist(to: playlistEditorName)
                    playlistEditorName = vm.activePlaylistDisplayName
                }
                .disabled(vm.activePlaylistEditorTarget == nil)

                Button("action.delete") {
                    isDeletePlaylistConfirmationPresented = true
                }
                .disabled(vm.activePlaylistEditorTarget == nil)
                .foregroundStyle(DndTheme.danger)
            }
        }
    }

    private func sidebarDivider(minHeight: CGFloat, maxHeight: CGFloat) -> some View {
        let isHighlighted = isResizingSidebar || isSidebarDividerHovered
        return Rectangle()
            .fill(isHighlighted ? DndTheme.accent.opacity(0.85) : Color.white.opacity(0.16))
            .overlay {
                Capsule()
                    .fill(isHighlighted ? DndTheme.accent.opacity(0.92) : Color.white.opacity(0.26))
                    .frame(width: 42, height: 4)
            }
            .cornerRadius(4)
            .contentShape(Rectangle())
            .onHover { hovered in
                isSidebarDividerHovered = hovered
            }
            .gesture(
                DragGesture(minimumDistance: 1, coordinateSpace: .global)
                    .onChanged { value in
                        if sidebarDragStartHeight == nil {
                            // Захватываем уже ограниченную высоту один раз на старте drag,
                            // чтобы divider не перескакивал при промежуточных layout/update pass.
                            sidebarDragStartHeight = clampedMusicPaneHeight(maxHeight: maxHeight)
                        }

                        // Меняем высоту в заданных пределах, чтобы обе секции оставались пригодны к скроллу.
                        let start = sidebarDragStartHeight ?? clampedMusicPaneHeight(maxHeight: maxHeight)
                        let updated = min(max(start + value.translation.height, minHeight), maxHeight)
                        isResizingSidebar = true
                        musicPaneHeight = updated
                    }
                    .onEnded { value in
                        let start = sidebarDragStartHeight ?? clampedMusicPaneHeight(maxHeight: maxHeight)
                        let updated = min(max(start + value.translation.height, minHeight), maxHeight)
                        musicPaneHeight = updated
                        persistSidebarHeight(updated)
                        sidebarDragStartHeight = nil
                        isResizingSidebar = false
                    }
            )
            .animation(.easeOut(duration: 0.12), value: isHighlighted)
    }

    private func clampedMusicPaneHeight(maxHeight: CGFloat) -> CGFloat {
        let minHeight: CGFloat = 96
        return min(max(musicPaneHeight, minHeight), maxHeight)
    }

    private func persistSidebarHeight(_ value: CGFloat) {
        persistedMusicPaneHeight = Double(value)
    }

    // MARK: - Центральная зона (50/50)

    private var centerArea: some View {
        GeometryReader { geometry in
            let dragHandleHeight: CGFloat = 10
            let sectionSpacing: CGFloat = 10
            let contentHeight = max(280, geometry.size.height)
            let minSectionHeight: CGFloat = 120
            let maxMusicHeight = max(minSectionHeight, contentHeight - minSectionHeight - dragHandleHeight - sectionSpacing * 2)
            let topHeight = clampedMainMusicPaneHeight(maxHeight: maxMusicHeight)
            let bottomHeight = max(minSectionHeight, contentHeight - topHeight - dragHandleHeight - sectionSpacing)

            VStack(spacing: sectionSpacing) {
                musicZone
                    .frame(height: topHeight)

                centerAreaDivider(minHeight: minSectionHeight, maxHeight: maxMusicHeight)
                    .frame(height: dragHandleHeight)

                effectsZone
                    .frame(height: bottomHeight)
            }
            .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .top)
        }
        .padding(12)
    }

    private func centerAreaDivider(minHeight: CGFloat, maxHeight: CGFloat) -> some View {
        let isHighlighted = isResizingCenterDivider || isCenterDividerHovered
        return Rectangle()
            .fill(isHighlighted ? DndTheme.accent.opacity(0.85) : Color.white.opacity(0.16))
            .overlay {
                Capsule()
                    .fill(isHighlighted ? DndTheme.accent.opacity(0.92) : Color.white.opacity(0.26))
                    .frame(width: 42, height: 4)
            }
            .cornerRadius(4)
            .contentShape(Rectangle())
            .onHover { hovered in
                isCenterDividerHovered = hovered
            }
            .gesture(
                DragGesture(minimumDistance: 1, coordinateSpace: .global)
                    .onChanged { value in
                        if centerDividerDragStartHeight == nil {
                            centerDividerDragStartHeight = clampedMainMusicPaneHeight(maxHeight: maxHeight)
                        }

                        let start = centerDividerDragStartHeight ?? clampedMainMusicPaneHeight(maxHeight: maxHeight)
                        let updated = min(max(start + value.translation.height, minHeight), maxHeight)
                        isResizingCenterDivider = true
                        mainMusicPaneHeight = updated
                    }
                    .onEnded { value in
                        let start = centerDividerDragStartHeight ?? clampedMainMusicPaneHeight(maxHeight: maxHeight)
                        let updated = min(max(start + value.translation.height, minHeight), maxHeight)
                        mainMusicPaneHeight = updated
                        persistMainCenterPaneHeight(updated)
                        centerDividerDragStartHeight = nil
                        isResizingCenterDivider = false
                    }
            )
            .animation(.easeOut(duration: 0.12), value: isHighlighted)
    }

    private func clampedMainMusicPaneHeight(maxHeight: CGFloat) -> CGFloat {
        let minHeight: CGFloat = 120
        return min(max(mainMusicPaneHeight, minHeight), maxHeight)
    }

    private func persistMainCenterPaneHeight(_ value: CGFloat) {
        persistedMainMusicPaneHeight = Double(value)
    }

    private var musicZone: some View {
        VStack(alignment: .leading, spacing: 8) {
            HStack {
                Text(vm.selectedMusicPlaylist?.name ?? L10n.tr("empty.music_playlist"))
                    .font(.title3)
                    .bold()
                    .foregroundStyle(DndTheme.textPrimary)

                Spacer()

                Button("action.add_files") {
                    vm.addMusicTracksFromFinder()
                }

                Button("action.add_folder") {
                    vm.addMusicFolderFromFinder()
                }

                if !selectedMusicTrackIDs.isEmpty {
                    Button("action.delete_selected") {
                        vm.removeMusicTracks(selectedMusicTrackIDs)
                        selectedMusicTrackIDs.removeAll()
                    }
                }
            }

            if vm.selectedMusicPlaylist != nil {
                ScrollView {
                    LazyVGrid(columns: gridColumns(vm.musicColumnsCount), spacing: 8) {
                        ForEach(vm.musicTracks) { track in
                            compactMusicTile(track)
                        }
                    }
                    .padding(.vertical, 4)
                }
            } else {
                emptyState(title: L10n.tr("empty.music_playlist"), icon: "music.note.list")
            }
        }
        .padding(8)
        .background(
            RoundedRectangle(cornerRadius: 12)
                .stroke(isMusicDropTargeted ? DndTheme.accent : Color.clear, lineWidth: 2)
        )
        .onDrop(of: [UTType.fileURL.identifier], isTargeted: $isMusicDropTargeted) { providers in
            handleFileDrop(providers: providers, target: .music)
        }
    }

    private var effectsZone: some View {
        VStack(alignment: .leading, spacing: 8) {
            HStack {
                Text(vm.selectedEffectPlaylist?.name ?? L10n.tr("empty.sfx_playlist"))
                    .font(.title3)
                    .bold()
                    .foregroundStyle(DndTheme.textPrimary)

                Spacer()

                Button("action.add_files") {
                    vm.addEffectsFromFinder()
                }

                Button("action.add_folder") {
                    vm.addEffectsFolderFromFinder()
                }

                if !selectedEffectTrackIDs.isEmpty {
                    Button("action.delete_selected") {
                        vm.removeEffectTracks(selectedEffectTrackIDs)
                        selectedEffectTrackIDs.removeAll()
                    }
                }
            }

            if vm.selectedEffectPlaylist != nil {
                ScrollView {
                    LazyVGrid(columns: gridColumns(vm.effectsColumnsCount), spacing: 8) {
                        ForEach(vm.effectTracks) { track in
                            compactEffectTile(track)
                        }
                    }
                    .padding(.vertical, 4)
                }
            } else {
                emptyState(title: L10n.tr("empty.sfx_playlist"), icon: "waveform.path")
            }
        }
        .padding(8)
        .background(
            RoundedRectangle(cornerRadius: 12)
                .stroke(isEffectsDropTargeted ? DndTheme.accent : Color.clear, lineWidth: 2)
        )
        .onDrop(of: [UTType.fileURL.identifier], isTargeted: $isEffectsDropTargeted) { providers in
            handleFileDrop(providers: providers, target: .effect)
        }
    }

    private func gridColumns(_ count: Int) -> [GridItem] {
        Array(repeating: GridItem(.flexible(), spacing: 8), count: max(1, count))
    }

    private func compactMusicTile(_ track: Track) -> some View {
        let isSelected = selectedMusicTrackIDs.contains(track.id)
        return HStack(spacing: 8) {
            Text(track.title)
                .font(.callout)
                .foregroundStyle(DndTheme.textPrimary)
                .lineLimit(1)

            Spacer(minLength: 6)

            if vm.currentTrackID == track.id {
                Image(systemName: vm.isPlaying ? "speaker.wave.2.fill" : "pause.circle")
                    .foregroundStyle(DndTheme.accent)
            }

            Button(role: .destructive) {
                vm.removeMusicTrack(track)
            } label: {
                Image(systemName: "trash")
            }
            .buttonStyle(.borderless)
            .foregroundStyle(DndTheme.danger)
        }
        .padding(.vertical, 8)
        .padding(.horizontal, 10)
        .background(
            RoundedRectangle(cornerRadius: 10)
                .fill(
                    isSelected
                        ? DndTheme.accentSoft.opacity(0.45)
                        : (vm.currentTrackID == track.id ? DndTheme.cardCurrent : DndTheme.card.opacity(0.65))
                )
        )
        .overlay(
            RoundedRectangle(cornerRadius: 10)
                .stroke(
                    isSelected ? DndTheme.accent.opacity(0.75) : (vm.currentTrackID == track.id ? DndTheme.accent.opacity(0.45) : Color.white.opacity(0.05)),
                    lineWidth: 1
                )
        )
        .contentShape(Rectangle())
        .onTapGesture {
            if NSEvent.modifierFlags.contains(.command) {
                toggleMusicTrackSelection(track.id)
                return
            }
            selectedMusicTrackIDs = [track.id]
            vm.playMusicTrack(track)
        }
        .contextMenu {
            Button("action.play") {
                vm.playMusicTrack(track)
            }

            Button("action.delete", role: .destructive) {
                let ids = selectedMusicTrackIDs.contains(track.id) ? selectedMusicTrackIDs : [track.id]
                vm.removeMusicTracks(ids)
                selectedMusicTrackIDs.subtract(ids)
            }
        }
        .help(track.path)
    }

    private func compactEffectTile(_ track: Track) -> some View {
        let isSelected = selectedEffectTrackIDs.contains(track.id)
        let hotkey = effectHotkeyLabel(for: track.id)
        return HStack(spacing: 8) {
            Text(track.title)
                .font(.callout)
                .foregroundStyle(DndTheme.textPrimary)
                .lineLimit(1)

            Spacer(minLength: 6)

            if let hotkey {
                Text(hotkey)
                    .font(.caption2.monospaced())
                    .foregroundStyle(DndTheme.textSecondary)
            }

            Button {
                vm.playEffect(track)
            } label: {
                Image(systemName: "play.circle.fill")
            }
            .buttonStyle(.borderless)
            .foregroundStyle(DndTheme.accent)

            Button(role: .destructive) {
                let ids = selectedEffectTrackIDs.contains(track.id) ? selectedEffectTrackIDs : [track.id]
                vm.removeEffectTracks(ids)
                selectedEffectTrackIDs.subtract(ids)
            } label: {
                Image(systemName: "trash")
            }
            .buttonStyle(.borderless)
            .foregroundStyle(DndTheme.danger)
        }
        .padding(.vertical, 8)
        .padding(.horizontal, 10)
        .background(
            RoundedRectangle(cornerRadius: 10)
                .fill(isSelected ? DndTheme.accentSoft.opacity(0.45) : DndTheme.card.opacity(0.65))
        )
        .overlay(
            RoundedRectangle(cornerRadius: 10)
                .stroke(isSelected ? DndTheme.accent.opacity(0.75) : Color.white.opacity(0.05), lineWidth: 1)
        )
        .contentShape(Rectangle())
        .onTapGesture {
            if NSEvent.modifierFlags.contains(.command) {
                toggleEffectTrackSelection(track.id)
                return
            }
            selectedEffectTrackIDs = [track.id]
            vm.playEffect(track)
        }
        .contextMenu {
            Button("action.play") {
                vm.playEffect(track)
            }

            Button("action.delete", role: .destructive) {
                let ids = selectedEffectTrackIDs.contains(track.id) ? selectedEffectTrackIDs : [track.id]
                vm.removeEffectTracks(ids)
                selectedEffectTrackIDs.subtract(ids)
            }
        }
        .help(track.path)
    }

    private func emptyState(title: String, icon: String) -> some View {
        VStack(spacing: 8) {
            Image(systemName: icon)
                .font(.system(size: 30))
                .foregroundStyle(DndTheme.accent)

            Text(title)
                .font(.headline)
                .foregroundStyle(DndTheme.textPrimary)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
    }

    // MARK: - Нижняя панель

    private var playerBar: some View {
        VStack(spacing: 10) {
            HStack {
                VStack(alignment: .leading, spacing: 4) {
                    Text(vm.currentTrack?.title ?? L10n.tr("player.nothing_playing"))
                        .font(.headline)
                        .foregroundStyle(DndTheme.textPrimary)

                    Text(vm.selectedMusicPlaylist?.name ?? "")
                        .font(.caption)
                        .foregroundStyle(DndTheme.textSecondary)
                }

                Spacer()

                Toggle(isOn: $vm.isShuffleEnabled) {
                    Image(systemName: vm.isShuffleEnabled ? "shuffle.circle.fill" : "shuffle.circle")
                }
                .toggleStyle(.button)
                .foregroundStyle(DndTheme.accent)
                .help("player.shuffle")

                Picker("player.repeat", selection: $vm.repeatMode) {
                    ForEach(RepeatMode.allCases) { mode in
                        Text(LocalizedStringKey(mode.localizedKey)).tag(mode)
                    }
                }
                .pickerStyle(.menu)
                .frame(width: 170)

                Divider()
                    .frame(height: 20)

                Button("player.stop_sfx") {
                    vm.stopEffects()
                }
                .disabled(!vm.hasActiveEffects)
            }

            HStack(spacing: 12) {
                Button {
                    vm.previousTrack()
                } label: {
                    Image(systemName: "backward.fill")
                }
                .keyboardShortcut(.leftArrow, modifiers: [.command])

                Button {
                    vm.playPause()
                } label: {
                    Image(systemName: vm.isPlaying ? "pause.fill" : "play.fill")
                }
                .keyboardShortcut(.space, modifiers: [])

                Button {
                    vm.nextTrack()
                } label: {
                    Image(systemName: "forward.fill")
                }
                .keyboardShortcut(.rightArrow, modifiers: [.command])

                Slider(
                    value: Binding(
                        get: { vm.currentTime },
                        set: { vm.seek(to: $0) }
                    ),
                    in: 0...(max(vm.duration, 0.1))
                )

                Text("\(formatTime(vm.currentTime)) / \(formatTime(vm.duration))")
                    .font(.caption.monospacedDigit())
                    .foregroundStyle(DndTheme.textPrimary)
                    .frame(width: 110, alignment: .trailing)

                Image(systemName: "speaker.fill")
                    .foregroundStyle(DndTheme.textPrimary)

                Slider(value: $vm.volume, in: 0...1)
                    .frame(width: 120)

                Image(systemName: "waveform.path")
                    .foregroundStyle(DndTheme.textPrimary)

                Slider(value: $vm.effectsVolume, in: 0...1)
                    .frame(width: 120)
            }
            .buttonStyle(.bordered)
        }
    }

    // MARK: - Настройки

    private var settingsView: some View {
        VStack(alignment: .leading, spacing: 16) {
            Text("settings.title")
                .font(.title2)
                .bold()

            VStack(alignment: .leading, spacing: 8) {
                Text("settings.ducking")
                    .font(.headline)

                HStack {
                    Slider(value: $vm.duckingAmount, in: 0.2...1.0)
                    Text("\(Int(vm.duckingAmount * 100))%")
                        .frame(width: 48, alignment: .trailing)
                }
            }

            VStack(alignment: .leading, spacing: 8) {
                Text("settings.music_columns")
                    .font(.headline)

                Picker("settings.music_columns", selection: $vm.musicColumnsCount) {
                    Text("2").tag(2)
                    Text("3").tag(3)
                    Text("4").tag(4)
                }
                .pickerStyle(.segmented)
            }

            VStack(alignment: .leading, spacing: 8) {
                Text("settings.effects_columns")
                    .font(.headline)

                Picker("settings.effects_columns", selection: $vm.effectsColumnsCount) {
                    Text("2").tag(2)
                    Text("3").tag(3)
                    Text("4").tag(4)
                }
                .pickerStyle(.segmented)
            }

            VStack(alignment: .leading, spacing: 8) {
                Text("settings.language")
                    .font(.headline)

                Picker("settings.language", selection: $appLanguageRawValue) {
                    ForEach(AppLanguage.allCases) { language in
                        Text(LocalizedStringKey(language.displayNameKey))
                            .tag(language.rawValue)
                    }
                }
                .pickerStyle(.segmented)
            }

            Toggle("settings.music_fade_out_on_pause", isOn: $vm.isMusicFadeOutOnPauseEnabled)

            VStack(alignment: .leading, spacing: 8) {
                Toggle("settings.telemetry.sentry_enabled", isOn: $vm.isSentryTelemetryEnabled)
                TextField("settings.telemetry.sentry_dsn", text: $vm.sentryDSN)
                    .textFieldStyle(.roundedBorder)
                    .disabled(!vm.isSentryTelemetryEnabled)
            }

            Spacer()

            HStack {
                Spacer()
                Button("action.done") {
                    isSettingsPresented = false
                }
                .keyboardShortcut(.defaultAction)
            }
        }
        .padding(20)
        .frame(minWidth: 460, minHeight: 360)
        .background(DndTheme.panel)
    }

    // MARK: - Вспомогательное

    private func formatTime(_ time: TimeInterval) -> String {
        if !time.isFinite {
            return "00:00"
        }

        let total = Int(time.rounded())
        let minutes = total / 60
        let seconds = total % 60

        return String(format: "%02d:%02d", minutes, seconds)
    }

    private func toggleMusicTrackSelection(_ id: UUID) {
        if selectedMusicTrackIDs.contains(id) {
            selectedMusicTrackIDs.remove(id)
        } else {
            selectedMusicTrackIDs.insert(id)
        }
    }

    private func toggleEffectTrackSelection(_ id: UUID) {
        if selectedEffectTrackIDs.contains(id) {
            selectedEffectTrackIDs.remove(id)
        } else {
            selectedEffectTrackIDs.insert(id)
        }
    }

    private enum DropTarget {
        case music
        case effect
    }

    private func handleFileDrop(providers: [NSItemProvider], target: DropTarget) -> Bool {
        let group = DispatchGroup()
        var collected: [URL] = []
        let lock = NSLock()
        let typeID = UTType.fileURL.identifier

        for provider in providers where provider.hasItemConformingToTypeIdentifier(typeID) {
            group.enter()
            provider.loadItem(forTypeIdentifier: typeID, options: nil) { item, _ in
                defer { group.leave() }
                var url: URL?
                if let data = item as? Data {
                    url = URL(dataRepresentation: data, relativeTo: nil)
                } else if let fileURL = item as? URL {
                    url = fileURL
                }
                if let url {
                    lock.lock()
                    collected.append(url)
                    lock.unlock()
                }
            }
        }

        group.notify(queue: .main) {
            switch target {
            case .music:
                vm.importDroppedMusicURLs(collected)
            case .effect:
                vm.importDroppedEffectURLs(collected)
            }
        }
        return true
    }

    private var hotkeysProxy: some View {
        Group {
            ForEach(Array(vm.effectTracks.prefix(9).enumerated()), id: \.offset) { index, track in
                Button("") {
                    vm.playEffect(track)
                }
                .keyboardShortcut(KeyEquivalent(Character("\(index + 1)")), modifiers: [.option])
            }
        }
    }

    private func effectHotkeyLabel(for trackID: UUID) -> String? {
        guard let index = vm.effectTracks.firstIndex(where: { $0.id == trackID }), index < 9 else {
            return nil
        }
        return "⌥\(index + 1)"
    }
}
