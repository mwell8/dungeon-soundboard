import SwiftUI
import AppKit
import UniformTypeIdentifiers

/// Главный экран приложения.
/// Здесь только интерфейс и вызовы методов ViewModel.
struct ContentView: View {
    @StateObject private var vm = PlayerViewModel()
    @StateObject private var hotkeyStore = HotkeyStore()
    @EnvironmentObject private var themeStore: ThemeStore

    @AppStorage(AppLanguage.userDefaultsKey) private var appLanguageRawValue: String = AppLanguage.defaultLanguage.rawValue
    @AppStorage("sidebar_music_pane_height") private var persistedMusicPaneHeight: Double = 220
    @AppStorage("main_music_pane_height") private var persistedMainMusicPaneHeight: Double = 260
    @State private var musicPaneHeight: CGFloat = 220
    @State private var mainMusicPaneHeight: CGFloat = 260
    @State private var isSettingsPresented: Bool = false
    @State private var isResizingSidebar: Bool = false
    @State private var isSidebarDividerHovered: Bool = false
    @State private var isResizingCenterDivider: Bool = false
    @State private var isCenterDividerHovered: Bool = false
    @State private var sidebarDragStartHeight: CGFloat?
    @State private var centerDividerDragStartHeight: CGFloat?
    @State private var hoveredMusicPlaylistID: UUID?
    @State private var hoveredEffectPlaylistID: UUID?
    @State private var draggingMusicPlaylistID: UUID?
    @State private var draggingEffectPlaylistID: UUID?
    @State private var playlistRenameTarget: PlaylistActionTarget?
    @State private var playlistDeleteTarget: PlaylistActionTarget?
    @State private var playlistRenameName: String = ""
    @State private var hoveredMusicTrackID: UUID?
    @State private var hoveredEffectTrackID: UUID?
    @State private var draggingMusicTrackID: UUID?
    @State private var draggingEffectTrackID: UUID?
    @State private var trackRenameTarget: TrackRenameTarget?
    @State private var trackRenameName: String = ""
    @State private var trackVolumeTarget: TrackVolumeTarget?
    @State private var hotkeyMonitor: Any?
    @State private var selectedMusicTrackIDs: Set<UUID> = []
    @State private var selectedEffectTrackIDs: Set<UUID> = []
    @State private var isMusicDropTargeted: Bool = false
    @State private var isEffectsDropTargeted: Bool = false

    private var theme: ResolvedTheme {
        themeStore.resolvedTheme
    }

    private var metrics: LayoutMetrics {
        themeStore.layoutMetrics
    }

    var body: some View {
        ZStack {
            backgroundLayer

            VStack(spacing: 0) {
                topBar
                    .padding(.horizontal, metrics.panelPadding)
                    .padding(.top, metrics.panelPadding)

                HStack(spacing: 0) {
                    sidebar
                        .frame(minWidth: 250, idealWidth: 270, maxWidth: 300)
                        .background(panelSurface(theme.panel, opacity: theme.panelOpacity))
                        .clipShape(RoundedRectangle(cornerRadius: metricsCornerRadius))

                    Divider()

                    centerArea
                        .frame(minWidth: 560)
                        .frame(maxWidth: .infinity, maxHeight: .infinity)
                        .background(panelSurface(theme.panel, opacity: theme.panelOpacity * 0.92))
                        .clipShape(RoundedRectangle(cornerRadius: metricsCornerRadius))
                }

                Divider()

                playerBar
                    .padding(metrics.panelPadding)
                    .background(panelSurface(theme.panelAlt, opacity: min(theme.panelOpacity + 0.06, 0.98)))
                    .clipShape(RoundedRectangle(cornerRadius: metricsCornerRadius))
            }
            .padding(metrics.panelPadding)
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
        .alert("error.title", isPresented: Binding(
            get: { themeStore.errorMessage != nil },
            set: { isPresented in
                if !isPresented {
                    themeStore.errorMessage = nil
                }
            }
        )) {
            Button("action.ok", role: .cancel) {
                themeStore.errorMessage = nil
            }
        } message: {
            Text(themeStore.errorMessage ?? L10n.tr("error.unknown"))
        }
        .sheet(isPresented: $isSettingsPresented) {
            ThemeSettingsView(
                vm: vm,
                hotkeyStore: hotkeyStore,
                appLanguageRawValue: $appLanguageRawValue,
                isPresented: $isSettingsPresented,
                onCaptureHotkey: { action in
                    hotkeyStore.beginCapture(for: action)
                }
            )
            .environmentObject(themeStore)
        }
        .sheet(item: $playlistRenameTarget) { target in
            RenameItemSheet(
                titleKey: "playlist.rename.title",
                placeholderKey: "sidebar.playlist_name",
                name: $playlistRenameName,
                onSave: {
                    renamePlaylist(target, to: playlistRenameName)
                    playlistRenameTarget = nil
                },
                onCancel: {
                    playlistRenameTarget = nil
                }
            )
        }
        .sheet(item: $trackRenameTarget) { target in
            RenameItemSheet(
                titleKey: trackRenameTitleKey(for: target),
                placeholderKey: trackRenamePlaceholderKey(for: target),
                name: $trackRenameName,
                onSave: {
                    renameTrack(target, to: trackRenameName)
                    trackRenameTarget = nil
                },
                onCancel: {
                    trackRenameTarget = nil
                }
            )
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
            isPresented: Binding(
                get: { playlistDeleteTarget != nil },
                set: { isPresented in
                    if !isPresented {
                        playlistDeleteTarget = nil
                    }
                }
            ),
            titleVisibility: .visible
        ) {
            Button("action.delete", role: .destructive) {
                if let target = playlistDeleteTarget {
                    deletePlaylist(target)
                }
                playlistDeleteTarget = nil
            }
            Button("action.cancel", role: .cancel) {}
        } message: {
            Text("playlist.delete.confirm.message")
        }
        .onAppear {
            musicPaneHeight = CGFloat(persistedMusicPaneHeight)
            mainMusicPaneHeight = CGFloat(persistedMainMusicPaneHeight)
            vm.refreshLocalizedDefaultPlaylistNames()
            hotkeyStore.purgeMissingTrackBindings(musicPlaylists: vm.musicPlaylists, effectPlaylists: vm.effectPlaylists)
            installHotkeyMonitorIfNeeded()
        }
        .onDisappear {
            removeHotkeyMonitor()
        }
        .onChange(of: appLanguageRawValue) {
            vm.refreshLocalizedDefaultPlaylistNames()
        }
        .onChange(of: vm.musicPlaylists) {
            hotkeyStore.purgeMissingTrackBindings(musicPlaylists: vm.musicPlaylists, effectPlaylists: vm.effectPlaylists)
        }
        .onChange(of: vm.effectPlaylists) {
            hotkeyStore.purgeMissingTrackBindings(musicPlaylists: vm.musicPlaylists, effectPlaylists: vm.effectPlaylists)
        }
        .onChange(of: vm.selectedMusicPlaylistID) {
            selectedMusicTrackIDs.removeAll()
        }
        .onChange(of: vm.selectedEffectPlaylistID) {
            selectedEffectTrackIDs.removeAll()
        }
        .overlay {
            if let captureAction = hotkeyStore.captureAction {
                HotkeyCaptureOverlay(
                    actionTitle: hotkeyActionTitle(captureAction, vm: vm),
                    onCancel: {
                        hotkeyStore.cancelCapture()
                    }
                )
            }
        }
        .popover(item: $trackVolumeTarget) { target in
            trackVolumePopover(for: target)
        }
    }

    private var metricsCornerRadius: CGFloat {
        CGFloat(theme.cornerRadius) * metrics.cornerCompaction
    }

    private var backgroundLayer: some View {
        ZStack {
            LinearGradient(
                colors: [theme.backgroundTop, theme.backgroundBottom],
                startPoint: .topLeading,
                endPoint: .bottomTrailing
            )

            if let image = themeStore.backgroundImage, themeStore.theme.background.mode == .image {
                backgroundImageView(image: image, layoutMode: themeStore.theme.background.layoutMode)
                    .opacity(theme.backgroundOpacity)
                    .blur(radius: theme.backgroundBlurRadius)

                Color.black
                    .opacity(theme.backgroundDimOverlay)
            }
        }
        .ignoresSafeArea()
    }

    private func backgroundImageView(image: NSImage, layoutMode: BackgroundLayoutMode) -> some View {
        GeometryReader { geometry in
            switch layoutMode {
            case .fill:
                Image(nsImage: image)
                    .resizable()
                    .scaledToFill()
                    .frame(width: geometry.size.width, height: geometry.size.height)
                    .clipped()
            case .fit:
                Image(nsImage: image)
                    .resizable()
                    .scaledToFit()
                    .frame(width: geometry.size.width, height: geometry.size.height)
            case .center:
                Image(nsImage: image)
                    .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .center)
            case .tile:
                Rectangle()
                    .fill(ImagePaint(image: Image(nsImage: image), scale: 0.35))
            }
        }
    }

    private func panelSurface(_ color: Color, opacity: Double) -> some View {
        ZStack {
            if theme.blurStrength == .high {
                Rectangle().fill(.ultraThinMaterial)
            } else if theme.blurStrength == .medium {
                Rectangle().fill(.thinMaterial)
            } else {
                Rectangle().fill(.regularMaterial.opacity(0.4))
            }
            color.opacity(opacity)
        }
    }

    // MARK: - Верхняя панель

    private var topBar: some View {
        HStack {
            Text("app.title")
                .font(.headline)
                .foregroundStyle(theme.textPrimary)

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
            let dragHandleHeight: CGFloat = 10
            let sectionSpacing = metrics.sectionSpacing
            let verticalPadding = metrics.panelPadding
            let contentHeight = max(
                220,
                geometry.size.height - sectionSpacing - verticalPadding * 2
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
            }
            .padding(.horizontal, 12)
            .padding(.vertical, verticalPadding)
        }
    }

    private var musicPlaylistsSection: some View {
        VStack(alignment: .leading, spacing: metrics.sectionSpacing * 0.8) {
            HStack {
                Text("sidebar.music_playlists")
                    .font(.headline)
                    .foregroundStyle(theme.textPrimary)

                Spacer()

                Button {
                    vm.createMusicPlaylist()
                } label: {
                    Image(systemName: "plus")
                }
                .buttonStyle(.borderless)
                .foregroundStyle(theme.accent)
            }

            List(selection: $vm.selectedMusicPlaylistID) {
                ForEach(vm.musicPlaylists) { playlist in
                    musicPlaylistRow(playlist)
                    .tag(playlist.id)
                }
            }
            .scrollContentBackground(.hidden)
        }
    }

    private var effectPlaylistsSection: some View {
        VStack(alignment: .leading, spacing: metrics.sectionSpacing * 0.8) {
            HStack {
                Text("sidebar.sfx_playlists")
                    .font(.headline)
                    .foregroundStyle(theme.textPrimary)

                Spacer()

                Button {
                    vm.createEffectPlaylist()
                } label: {
                    Image(systemName: "plus")
                }
                .buttonStyle(.borderless)
                .foregroundStyle(theme.accent)
            }

            List(selection: $vm.selectedEffectPlaylistID) {
                ForEach(vm.effectPlaylists) { playlist in
                    effectPlaylistRow(playlist)
                    .tag(playlist.id)
                }
            }
            .scrollContentBackground(.hidden)
        }
    }

    private func musicPlaylistRow(_ playlist: Playlist) -> some View {
        let isHovered = hoveredMusicPlaylistID == playlist.id
        let isDragging = draggingMusicPlaylistID == playlist.id
        let controlsOpacity = (isHovered || isDragging) ? 1.0 : 0.0
        let controlsEnabled = isHovered || isDragging

        return HStack(spacing: 8) {
            Text(playlist.name)
                .foregroundStyle(theme.textPrimary)
                .lineLimit(1)

            Spacer(minLength: 6)

            Button {
                vm.playMusicPlaylistShuffled(playlist)
            } label: {
                Image(systemName: "shuffle")
            }
            .buttonStyle(.borderless)
            .foregroundStyle(theme.accent)
            .opacity(controlsOpacity)
            .allowsHitTesting(controlsEnabled)
            .help("sidebar.shuffle_play")

            playlistDragHandle
                .opacity(controlsOpacity)
                .allowsHitTesting(controlsEnabled)
                .onDrag {
                    draggingMusicPlaylistID = playlist.id
                    return NSItemProvider(object: playlist.id.uuidString as NSString)
                }

            playlistActionsMenu(
                renameAction: {
                    presentPlaylistRename(.music(playlist.id), currentName: playlist.name)
                },
                deleteAction: {
                    playlistDeleteTarget = .music(playlist.id)
                }
            )
            .opacity(controlsOpacity)
            .allowsHitTesting(controlsEnabled)
        }
        .contentShape(Rectangle())
        .onHover { hovered in
            if hovered {
                hoveredMusicPlaylistID = playlist.id
            } else if hoveredMusicPlaylistID == playlist.id {
                hoveredMusicPlaylistID = nil
            }
        }
        .onDrop(
            of: [UTType.plainText.identifier],
            delegate: MusicPlaylistDropDelegate(
                targetID: playlist.id,
                vm: vm,
                draggingID: $draggingMusicPlaylistID
            )
        )
    }

    private func effectPlaylistRow(_ playlist: EffectPlaylist) -> some View {
        let isHovered = hoveredEffectPlaylistID == playlist.id
        let isDragging = draggingEffectPlaylistID == playlist.id
        let controlsOpacity = (isHovered || isDragging) ? 1.0 : 0.0
        let controlsEnabled = isHovered || isDragging

        return HStack(spacing: 8) {
            Text(playlist.name)
                .foregroundStyle(theme.textPrimary)
                .lineLimit(1)

            Spacer(minLength: 6)

            playlistDragHandle
                .opacity(controlsOpacity)
                .allowsHitTesting(controlsEnabled)
                .onDrag {
                    draggingEffectPlaylistID = playlist.id
                    return NSItemProvider(object: playlist.id.uuidString as NSString)
                }

            playlistActionsMenu(
                renameAction: {
                    presentPlaylistRename(.effect(playlist.id), currentName: playlist.name)
                },
                deleteAction: {
                    playlistDeleteTarget = .effect(playlist.id)
                }
            )
            .opacity(controlsOpacity)
            .allowsHitTesting(controlsEnabled)
        }
        .contentShape(Rectangle())
        .onHover { hovered in
            if hovered {
                hoveredEffectPlaylistID = playlist.id
            } else if hoveredEffectPlaylistID == playlist.id {
                hoveredEffectPlaylistID = nil
            }
        }
        .onDrop(
            of: [UTType.plainText.identifier],
            delegate: EffectPlaylistDropDelegate(
                targetID: playlist.id,
                vm: vm,
                draggingID: $draggingEffectPlaylistID
            )
        )
    }

    private var playlistDragHandle: some View {
        Image(systemName: "line.3.horizontal")
            .foregroundStyle(theme.textSecondary)
            .frame(width: 18, height: 18)
            .contentShape(Rectangle())
            .help("playlist.drag_handle")
    }

    private func playlistActionsMenu(renameAction: @escaping () -> Void, deleteAction: @escaping () -> Void) -> some View {
        Menu {
            Button("action.rename") {
                renameAction()
            }

            Button("action.delete", role: .destructive) {
                deleteAction()
            }
        } label: {
            Image(systemName: "gearshape")
                .frame(width: 18, height: 18)
        }
        .menuStyle(.borderlessButton)
        .help("playlist.actions")
    }

    private func sidebarDivider(minHeight: CGFloat, maxHeight: CGFloat) -> some View {
        let isHighlighted = isResizingSidebar || isSidebarDividerHovered
        return Rectangle()
            .fill(isHighlighted ? theme.accent.opacity(0.85) : theme.divider)
            .overlay {
                Capsule()
                    .fill(isHighlighted ? theme.accent.opacity(0.92) : theme.textSecondary.opacity(0.26))
                    .frame(width: 42, height: 4)
            }
            .cornerRadius(metricsCornerRadius * 0.3)
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
            let sectionSpacing = metrics.sectionSpacing
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
        .padding(metrics.panelPadding)
    }

    private func centerAreaDivider(minHeight: CGFloat, maxHeight: CGFloat) -> some View {
        let isHighlighted = isResizingCenterDivider || isCenterDividerHovered
        return Rectangle()
            .fill(isHighlighted ? theme.accent.opacity(0.85) : theme.divider)
            .overlay {
                Capsule()
                    .fill(isHighlighted ? theme.accent.opacity(0.92) : theme.textSecondary.opacity(0.26))
                    .frame(width: 42, height: 4)
            }
            .cornerRadius(metricsCornerRadius * 0.3)
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
        VStack(alignment: .leading, spacing: metrics.sectionSpacing * 0.8) {
            HStack {
                Text(vm.selectedMusicPlaylist?.name ?? L10n.tr("empty.music_playlist"))
                    .font(.title3)
                    .bold()
                    .foregroundStyle(theme.textPrimary)

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
                    LazyVGrid(columns: gridColumns(vm.musicColumnsCount), spacing: metrics.sectionSpacing * 0.8) {
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
        .padding(metrics.panelPadding * 0.8)
        .background(
            RoundedRectangle(cornerRadius: metricsCornerRadius)
                .fill(theme.panel.opacity(theme.panelOpacity * 0.38))
        )
        .background(
            RoundedRectangle(cornerRadius: metricsCornerRadius)
                .stroke(isMusicDropTargeted ? theme.accent : Color.clear, lineWidth: 2)
        )
        .onDrop(of: [UTType.fileURL.identifier], isTargeted: $isMusicDropTargeted) { providers in
            handleFileDrop(providers: providers, target: .music)
        }
    }

    private var effectsZone: some View {
        VStack(alignment: .leading, spacing: metrics.sectionSpacing * 0.8) {
            HStack {
                Text(vm.selectedEffectPlaylist?.name ?? L10n.tr("empty.sfx_playlist"))
                    .font(.title3)
                    .bold()
                    .foregroundStyle(theme.textPrimary)

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
                    LazyVGrid(columns: gridColumns(vm.effectsColumnsCount), spacing: metrics.sectionSpacing * 0.8) {
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
        .padding(metrics.panelPadding * 0.8)
        .background(
            RoundedRectangle(cornerRadius: metricsCornerRadius)
                .fill(theme.panel.opacity(theme.panelOpacity * 0.38))
        )
        .background(
            RoundedRectangle(cornerRadius: metricsCornerRadius)
                .stroke(isEffectsDropTargeted ? theme.accent : Color.clear, lineWidth: 2)
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
        let action = vm.selectedMusicPlaylistID.map { HotkeyAction.playMusicTrack(playlistID: $0, trackID: track.id) }
        let renameTarget = vm.selectedMusicPlaylistID.map { TrackRenameTarget.music(playlistID: $0, trackID: track.id) }
        let volumeTarget = vm.selectedMusicPlaylistID.map { TrackVolumeTarget.music(playlistID: $0, trackID: track.id) }
        let hotkey = action.flatMap { hotkeyStore.hotkey(for: $0) }
        let isHovered = hoveredMusicTrackID == track.id
        let isDragging = draggingMusicTrackID == track.id
        let controlsOpacity = (isHovered || isDragging) ? 1.0 : 0.0
        let controlsEnabled = isHovered || isDragging
        return HStack(spacing: 8) {
            Text(track.title)
                .font(.callout)
                .foregroundStyle(theme.textPrimary)
                .lineLimit(1)

            Spacer(minLength: 6)

            if let hotkey {
                hotkeyBadge(hotkey)
            }

            if vm.currentTrackID == track.id {
                Image(systemName: vm.isPlaying ? "speaker.wave.2.fill" : "pause.circle")
                    .foregroundStyle(theme.accent)
            }

            trackDragHandle
                .opacity(controlsOpacity)
                .allowsHitTesting(controlsEnabled)
                .onDrag {
                    draggingMusicTrackID = track.id
                    return NSItemProvider(object: track.id.uuidString as NSString)
                }

            if let action, let renameTarget, let volumeTarget {
                trackActionsMenu(action: action, renameTarget: renameTarget, volumeTarget: volumeTarget)
                    .opacity(controlsOpacity)
                    .allowsHitTesting(controlsEnabled)
            }

            Button(role: .destructive) {
                vm.removeMusicTrack(track)
            } label: {
                Image(systemName: "trash")
            }
            .buttonStyle(.borderless)
            .foregroundStyle(theme.danger)
        }
        .padding(.vertical, metrics.sectionSpacing * 0.8)
        .padding(.horizontal, metrics.panelPadding * 0.85)
        .background(
            RoundedRectangle(cornerRadius: metricsCornerRadius * 0.75)
                .fill(
                    isSelected
                        ? theme.accentSoft.opacity(0.45)
                        : (vm.currentTrackID == track.id ? theme.cardCurrent : theme.card.opacity(0.65))
                )
        )
        .overlay(
            RoundedRectangle(cornerRadius: metricsCornerRadius * 0.75)
                .stroke(
                    isSelected ? theme.accent.opacity(0.75) : (vm.currentTrackID == track.id ? theme.accent.opacity(0.45) : theme.divider.opacity(0.45)),
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
        .onHover { hovered in
            if hovered {
                hoveredMusicTrackID = track.id
            } else if hoveredMusicTrackID == track.id {
                hoveredMusicTrackID = nil
            }
        }
        .contextMenu {
            Button("action.play") {
                vm.playMusicTrack(track)
            }

            if let renameTarget {
                Button("action.rename") {
                    presentTrackRename(renameTarget, currentName: track.title)
                }
            }

            if let volumeTarget {
                Button("track.volume") {
                    trackVolumeTarget = volumeTarget
                }
            }

            if let action {
                Button("hotkeys.assign") {
                    hotkeyStore.beginCapture(for: action)
                }

                if hotkeyStore.hotkey(for: action) != nil {
                    Button("hotkeys.clear") {
                        hotkeyStore.clear(action)
                    }
                }
            }

            Button("action.delete", role: .destructive) {
                let ids = selectedMusicTrackIDs.contains(track.id) ? selectedMusicTrackIDs : [track.id]
                vm.removeMusicTracks(ids)
                selectedMusicTrackIDs.subtract(ids)
            }
        }
        .onDrop(
            of: [UTType.plainText.identifier],
            delegate: MusicTrackDropDelegate(
                targetID: track.id,
                vm: vm,
                draggingID: $draggingMusicTrackID
            )
        )
        .help(track.path)
    }

    private func compactEffectTile(_ track: Track) -> some View {
        let isSelected = selectedEffectTrackIDs.contains(track.id)
        let action = vm.selectedEffectPlaylistID.map { HotkeyAction.playEffect(playlistID: $0, trackID: track.id) }
        let renameTarget = vm.selectedEffectPlaylistID.map { TrackRenameTarget.effect(playlistID: $0, trackID: track.id) }
        let volumeTarget = vm.selectedEffectPlaylistID.map { TrackVolumeTarget.effect(playlistID: $0, trackID: track.id) }
        let hotkey = action.flatMap { hotkeyStore.hotkey(for: $0) }
        let isHovered = hoveredEffectTrackID == track.id
        let isDragging = draggingEffectTrackID == track.id
        let controlsOpacity = (isHovered || isDragging) ? 1.0 : 0.0
        let controlsEnabled = isHovered || isDragging
        return HStack(spacing: 8) {
            Text(track.title)
                .font(.callout)
                .foregroundStyle(theme.textPrimary)
                .lineLimit(1)

            Spacer(minLength: 6)

            if let hotkey {
                hotkeyBadge(hotkey)
            }

            trackDragHandle
                .opacity(controlsOpacity)
                .allowsHitTesting(controlsEnabled)
                .onDrag {
                    draggingEffectTrackID = track.id
                    return NSItemProvider(object: track.id.uuidString as NSString)
                }

            if let action, let renameTarget, let volumeTarget {
                trackActionsMenu(action: action, renameTarget: renameTarget, volumeTarget: volumeTarget)
                    .opacity(controlsOpacity)
                    .allowsHitTesting(controlsEnabled)
            }

            Button(role: .destructive) {
                let ids = selectedEffectTrackIDs.contains(track.id) ? selectedEffectTrackIDs : [track.id]
                vm.removeEffectTracks(ids)
                selectedEffectTrackIDs.subtract(ids)
            } label: {
                Image(systemName: "trash")
            }
            .buttonStyle(.borderless)
            .foregroundStyle(theme.danger)
        }
        .padding(.vertical, metrics.sectionSpacing * 0.8)
        .padding(.horizontal, metrics.panelPadding * 0.85)
        .background(
            RoundedRectangle(cornerRadius: metricsCornerRadius * 0.75)
                .fill(isSelected ? theme.accentSoft.opacity(0.45) : theme.card.opacity(0.65))
        )
        .overlay(
            RoundedRectangle(cornerRadius: metricsCornerRadius * 0.75)
                .stroke(isSelected ? theme.accent.opacity(0.75) : theme.divider.opacity(0.45), lineWidth: 1)
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
        .onHover { hovered in
            if hovered {
                hoveredEffectTrackID = track.id
            } else if hoveredEffectTrackID == track.id {
                hoveredEffectTrackID = nil
            }
        }
        .contextMenu {
            Button("action.play") {
                vm.playEffect(track)
            }

            if let renameTarget {
                Button("action.rename") {
                    presentTrackRename(renameTarget, currentName: track.title)
                }
            }

            if let volumeTarget {
                Button("track.volume") {
                    trackVolumeTarget = volumeTarget
                }
            }

            if let action {
                Button("hotkeys.assign") {
                    hotkeyStore.beginCapture(for: action)
                }

                if hotkeyStore.hotkey(for: action) != nil {
                    Button("hotkeys.clear") {
                        hotkeyStore.clear(action)
                    }
                }
            }

            Button("action.delete", role: .destructive) {
                let ids = selectedEffectTrackIDs.contains(track.id) ? selectedEffectTrackIDs : [track.id]
                vm.removeEffectTracks(ids)
                selectedEffectTrackIDs.subtract(ids)
            }
        }
        .onDrop(
            of: [UTType.plainText.identifier],
            delegate: EffectTrackDropDelegate(
                targetID: track.id,
                vm: vm,
                draggingID: $draggingEffectTrackID
            )
        )
        .help(track.path)
    }

    private func hotkeyBadge(_ hotkey: Hotkey) -> some View {
        Text(hotkey.displayText)
            .font(.caption2.monospaced())
            .foregroundStyle(theme.textSecondary)
            .padding(.horizontal, 5)
            .padding(.vertical, 2)
            .background(theme.panelAlt.opacity(theme.panelOpacity * 0.75))
            .clipShape(RoundedRectangle(cornerRadius: 5))
    }

    private var trackDragHandle: some View {
        Image(systemName: "line.3.horizontal")
            .foregroundStyle(theme.textSecondary)
            .frame(width: 18, height: 18)
            .contentShape(Rectangle())
            .help("track.drag_handle")
    }

    private func trackActionsMenu(action: HotkeyAction, renameTarget: TrackRenameTarget, volumeTarget: TrackVolumeTarget) -> some View {
        Menu {
            Button("action.rename") {
                presentTrackRename(renameTarget, currentName: trackTitle(for: renameTarget))
            }

            Button("track.volume") {
                trackVolumeTarget = volumeTarget
            }

            Button("hotkeys.assign") {
                hotkeyStore.beginCapture(for: action)
            }

            if hotkeyStore.hotkey(for: action) != nil {
                Button("hotkeys.clear") {
                    hotkeyStore.clear(action)
                }
            }
        } label: {
            Image(systemName: "gearshape")
                .frame(width: 18, height: 18)
        }
        .menuStyle(.borderlessButton)
        .help("track.actions")
    }

    private func trackVolumePopover(for target: TrackVolumeTarget) -> some View {
        let volume = trackVolumeBinding(for: target)
        return VStack(alignment: .leading, spacing: 12) {
            Text("track.volume")
                .font(.headline)
                .foregroundStyle(theme.textPrimary)

            Text(trackVolumeTitle(for: target))
                .font(.caption)
                .foregroundStyle(theme.textSecondary)
                .lineLimit(1)

            Slider(value: volume, in: Track.minimumVolumeMultiplier...Track.maximumVolumeMultiplier)

            HStack {
                Text("\(Int((volume.wrappedValue * 100).rounded()))%")
                    .foregroundStyle(theme.textSecondary)
                Spacer()
                Button("track.volume.reset") {
                    volume.wrappedValue = Track.defaultVolumeMultiplier
                }
            }
        }
        .padding(14)
        .frame(width: 270)
        .background(theme.panel.opacity(theme.panelOpacity))
    }

    private func trackVolumeBinding(for target: TrackVolumeTarget) -> Binding<Double> {
        Binding(
            get: {
                switch target {
                case .music(let playlistID, let trackID):
                    return vm.musicTrackVolumeMultiplier(playlistID: playlistID, trackID: trackID)
                case .effect(let playlistID, let trackID):
                    return vm.effectTrackVolumeMultiplier(playlistID: playlistID, trackID: trackID)
                }
            },
            set: { value in
                switch target {
                case .music(let playlistID, let trackID):
                    vm.setMusicTrackVolumeMultiplier(value, playlistID: playlistID, trackID: trackID)
                case .effect(let playlistID, let trackID):
                    vm.setEffectTrackVolumeMultiplier(value, playlistID: playlistID, trackID: trackID)
                }
            }
        )
    }

    private func trackVolumeTitle(for target: TrackVolumeTarget) -> String {
        switch target {
        case .music(let playlistID, let trackID):
            let playlist = vm.musicPlaylists.first { $0.id == playlistID }
            let track = playlist?.tracks.first { $0.id == trackID }
            if let track, let playlist {
                return "\(track.title) · \(playlist.name)"
            }
            return L10n.tr("hotkeys.action.missing_track")
        case .effect(let playlistID, let trackID):
            let playlist = vm.effectPlaylists.first { $0.id == playlistID }
            let track = playlist?.effects.first { $0.id == trackID }
            if let track, let playlist {
                return "\(track.title) · \(playlist.name)"
            }
            return L10n.tr("hotkeys.action.missing_track")
        }
    }

    private func emptyState(title: String, icon: String) -> some View {
        VStack(spacing: 8) {
            Image(systemName: icon)
                .font(.system(size: 30))
                .foregroundStyle(theme.accent)

            Text(title)
                .font(.headline)
                .foregroundStyle(theme.textPrimary)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
    }

    // MARK: - Нижняя панель

    private var playerBar: some View {
        VStack(spacing: metrics.sectionSpacing) {
            HStack {
                VStack(alignment: .leading, spacing: 4) {
                    Text(vm.currentTrack?.title ?? L10n.tr("player.nothing_playing"))
                        .font(.headline)
                        .foregroundStyle(theme.textPrimary)

                    Text(vm.playbackMusicPlaylist?.name ?? vm.selectedMusicPlaylist?.name ?? "")
                        .font(.caption)
                        .foregroundStyle(theme.textSecondary)
                }

                Spacer()

                Toggle(isOn: $vm.isShuffleEnabled) {
                    Image(systemName: vm.isShuffleEnabled ? "shuffle.circle.fill" : "shuffle.circle")
                }
                .toggleStyle(.button)
                .foregroundStyle(theme.accent)
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

            HStack(spacing: metrics.sectionSpacing + 2) {
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
                    .foregroundStyle(theme.textPrimary)
                    .frame(width: 110, alignment: .trailing)

                Image(systemName: "speaker.fill")
                    .foregroundStyle(theme.textPrimary)

                Slider(value: $vm.volume, in: 0...1)
                    .frame(width: metrics.controlWidth)

                Image(systemName: "waveform.path")
                    .foregroundStyle(theme.textPrimary)

                Slider(value: $vm.effectsVolume, in: 0...1)
                    .frame(width: metrics.controlWidth)
            }
            .buttonStyle(.bordered)
        }
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

    private func installHotkeyMonitorIfNeeded() {
        guard hotkeyMonitor == nil else { return }
        hotkeyMonitor = NSEvent.addLocalMonitorForEvents(matching: .keyDown) { event in
            handleKeyDown(event)
        }
    }

    private func removeHotkeyMonitor() {
        if let hotkeyMonitor {
            NSEvent.removeMonitor(hotkeyMonitor)
            self.hotkeyMonitor = nil
        }
    }

    private func handleKeyDown(_ event: NSEvent) -> NSEvent? {
        if let captureAction = hotkeyStore.captureAction {
            if event.keyCode == 53 {
                hotkeyStore.cancelCapture()
                return nil
            }

            guard let hotkey = Hotkey.from(event: event) else {
                return nil
            }
            assignCapturedHotkey(hotkey, to: captureAction)
            return nil
        }

        guard !isTextInputActive(), let hotkey = Hotkey.from(event: event) else {
            return event
        }

        guard let action = hotkeyStore.action(for: hotkey) else {
            return event
        }

        executeHotkeyAction(action)
        return nil
    }

    private func isTextInputActive() -> Bool {
        guard let responder = NSApp.keyWindow?.firstResponder else { return false }
        return responder is NSTextView || responder is NSText
    }

    private func assignCapturedHotkey(_ hotkey: Hotkey, to action: HotkeyAction) {
        hotkeyStore.cancelCapture()
        if let conflict = hotkeyStore.assign(hotkey, to: action, resolvingConflicts: false) {
            let alert = NSAlert()
            alert.messageText = L10n.tr("hotkeys.conflict.title")
            alert.informativeText = L10n.tr(
                "hotkeys.conflict.message",
                hotkey.displayText,
                hotkeyActionTitle(conflict.existingAction, vm: vm)
            )
            alert.addButton(withTitle: L10n.tr("hotkeys.conflict.replace"))
            alert.addButton(withTitle: L10n.tr("action.cancel"))
            if alert.runModal() == .alertFirstButtonReturn {
                _ = hotkeyStore.assign(hotkey, to: action, resolvingConflicts: true)
            }
        }
    }

    private func executeHotkeyAction(_ action: HotkeyAction) {
        switch action {
        case .stopAll:
            vm.stop()
        case .stopEffects:
            vm.stopEffects()
        case .playPause:
            vm.playPause()
        case .musicVolumeUp:
            vm.adjustMusicVolume(by: 0.05)
        case .musicVolumeDown:
            vm.adjustMusicVolume(by: -0.05)
        case .effectsVolumeUp:
            vm.adjustEffectsVolume(by: 0.05)
        case .effectsVolumeDown:
            vm.adjustEffectsVolume(by: -0.05)
        case .playMusicTrack(let playlistID, let trackID):
            if !vm.playMusicTrack(playlistID: playlistID, trackID: trackID) {
                hotkeyStore.clear(action)
            }
        case .playEffect(let playlistID, let trackID):
            if !vm.playEffect(playlistID: playlistID, trackID: trackID) {
                hotkeyStore.clear(action)
            }
        }
    }

    private func presentPlaylistRename(_ target: PlaylistActionTarget, currentName: String) {
        playlistRenameName = currentName
        playlistRenameTarget = target
    }

    private func renamePlaylist(_ target: PlaylistActionTarget, to newName: String) {
        switch target {
        case .music(let id):
            vm.renameMusicPlaylist(id, to: newName)
        case .effect(let id):
            vm.renameEffectPlaylist(id, to: newName)
        }
    }

    private func presentTrackRename(_ target: TrackRenameTarget, currentName: String) {
        trackRenameName = currentName
        trackRenameTarget = target
    }

    private func renameTrack(_ target: TrackRenameTarget, to newName: String) {
        switch target {
        case .music(let playlistID, let trackID):
            vm.renameMusicTrack(playlistID: playlistID, trackID: trackID, to: newName)
        case .effect(let playlistID, let trackID):
            vm.renameEffectTrack(playlistID: playlistID, trackID: trackID, to: newName)
        }
    }

    private func trackTitle(for target: TrackRenameTarget) -> String {
        switch target {
        case .music(let playlistID, let trackID):
            return vm.musicPlaylists
                .first { $0.id == playlistID }?
                .tracks
                .first { $0.id == trackID }?
                .title ?? ""
        case .effect(let playlistID, let trackID):
            return vm.effectPlaylists
                .first { $0.id == playlistID }?
                .effects
                .first { $0.id == trackID }?
                .title ?? ""
        }
    }

    private func trackRenameTitleKey(for target: TrackRenameTarget) -> LocalizedStringKey {
        switch target {
        case .music:
            return "track.rename.title"
        case .effect:
            return "effect.rename.title"
        }
    }

    private func trackRenamePlaceholderKey(for target: TrackRenameTarget) -> LocalizedStringKey {
        switch target {
        case .music:
            return "track.name"
        case .effect:
            return "effect.name"
        }
    }

    private func deletePlaylist(_ target: PlaylistActionTarget) {
        switch target {
        case .music(let id):
            vm.deleteMusicPlaylist(id)
        case .effect(let id):
            vm.deleteEffectPlaylist(id)
        }
    }

    private enum PlaylistActionTarget: Identifiable, Equatable {
        case music(UUID)
        case effect(UUID)

        var id: String {
            switch self {
            case .music(let id): return "music-\(id.uuidString)"
            case .effect(let id): return "effect-\(id.uuidString)"
            }
        }
    }

    private enum TrackRenameTarget: Identifiable, Equatable {
        case music(playlistID: UUID, trackID: UUID)
        case effect(playlistID: UUID, trackID: UUID)

        var id: String {
            switch self {
            case .music(let playlistID, let trackID):
                return "rename-music-\(playlistID.uuidString)-\(trackID.uuidString)"
            case .effect(let playlistID, let trackID):
                return "rename-effect-\(playlistID.uuidString)-\(trackID.uuidString)"
            }
        }
    }

    private enum TrackVolumeTarget: Identifiable, Equatable {
        case music(playlistID: UUID, trackID: UUID)
        case effect(playlistID: UUID, trackID: UUID)

        var id: String {
            switch self {
            case .music(let playlistID, let trackID):
                return "music-\(playlistID.uuidString)-\(trackID.uuidString)"
            case .effect(let playlistID, let trackID):
                return "effect-\(playlistID.uuidString)-\(trackID.uuidString)"
            }
        }
    }

    private enum DropTarget {
        case music
        case effect
    }

    private func handleFileDrop(providers: [NSItemProvider], target: DropTarget) -> Bool {
        let group = DispatchGroup()
        let collected = DroppedURLCollector()
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
                    collected.append(url)
                }
            }
        }

        group.notify(queue: .main) {
            switch target {
            case .music:
                vm.importDroppedMusicURLs(collected.snapshot())
            case .effect:
                vm.importDroppedEffectURLs(collected.snapshot())
            }
        }
        return true
    }

}

private final class DroppedURLCollector: @unchecked Sendable {
    private let lock = NSLock()
    private var urls: [URL] = []

    func append(_ url: URL) {
        lock.lock()
        urls.append(url)
        lock.unlock()
    }

    func snapshot() -> [URL] {
        lock.lock()
        defer { lock.unlock() }
        return urls
    }
}

@MainActor
func hotkeyActionTitle(_ action: HotkeyAction, vm: PlayerViewModel) -> String {
    switch action {
    case .stopAll:
        return L10n.tr("hotkeys.action.stop_all")
    case .stopEffects:
        return L10n.tr("hotkeys.action.stop_effects")
    case .playPause:
        return L10n.tr("hotkeys.action.play_pause")
    case .musicVolumeUp:
        return L10n.tr("hotkeys.action.music_volume_up")
    case .musicVolumeDown:
        return L10n.tr("hotkeys.action.music_volume_down")
    case .effectsVolumeUp:
        return L10n.tr("hotkeys.action.effects_volume_up")
    case .effectsVolumeDown:
        return L10n.tr("hotkeys.action.effects_volume_down")
    case .playMusicTrack(let playlistID, let trackID):
        let playlist = vm.musicPlaylists.first { $0.id == playlistID }
        let track = playlist?.tracks.first { $0.id == trackID }
        guard let track else { return L10n.tr("hotkeys.action.missing_track") }
        if let playlist {
            return "\(track.title) · \(playlist.name)"
        }
        return track.title
    case .playEffect(let playlistID, let trackID):
        let playlist = vm.effectPlaylists.first { $0.id == playlistID }
        let track = playlist?.effects.first { $0.id == trackID }
        guard let track else { return L10n.tr("hotkeys.action.missing_track") }
        if let playlist {
            return "\(track.title) · \(playlist.name)"
        }
        return track.title
    }
}

struct HotkeyCaptureOverlay: View {
    let actionTitle: String
    let onCancel: () -> Void

    var body: some View {
        VStack(spacing: 12) {
            Text("hotkeys.capture.title")
                .font(.headline)
            Text(actionTitle)
                .font(.subheadline)
                .multilineTextAlignment(.center)
            Text("hotkeys.capture.hint")
                .font(.caption)
                .foregroundStyle(.secondary)

            Button("action.cancel") {
                onCancel()
            }
            .keyboardShortcut(.cancelAction)
        }
        .padding(20)
        .frame(width: 340)
        .background(.regularMaterial)
        .clipShape(RoundedRectangle(cornerRadius: 12))
        .shadow(radius: 18)
    }
}

private struct MusicPlaylistDropDelegate: DropDelegate {
    let targetID: UUID
    let vm: PlayerViewModel
    @Binding var draggingID: UUID?

    func validateDrop(info: DropInfo) -> Bool {
        draggingID != nil
    }

    func dropUpdated(info: DropInfo) -> DropProposal? {
        DropProposal(operation: .move)
    }

    func dropEntered(info: DropInfo) {
        guard let draggingID else { return }
        vm.moveMusicPlaylist(draggingID, to: targetID)
    }

    func performDrop(info: DropInfo) -> Bool {
        draggingID = nil
        return true
    }
}

private struct EffectPlaylistDropDelegate: DropDelegate {
    let targetID: UUID
    let vm: PlayerViewModel
    @Binding var draggingID: UUID?

    func validateDrop(info: DropInfo) -> Bool {
        draggingID != nil
    }

    func dropUpdated(info: DropInfo) -> DropProposal? {
        DropProposal(operation: .move)
    }

    func dropEntered(info: DropInfo) {
        guard let draggingID else { return }
        vm.moveEffectPlaylist(draggingID, to: targetID)
    }

    func performDrop(info: DropInfo) -> Bool {
        draggingID = nil
        return true
    }
}

private struct MusicTrackDropDelegate: DropDelegate {
    let targetID: UUID
    let vm: PlayerViewModel
    @Binding var draggingID: UUID?

    func validateDrop(info: DropInfo) -> Bool {
        draggingID != nil
    }

    func dropUpdated(info: DropInfo) -> DropProposal? {
        DropProposal(operation: .move)
    }

    func dropEntered(info: DropInfo) {
        guard let draggingID else { return }
        vm.moveMusicTrack(draggingID, to: targetID)
    }

    func performDrop(info: DropInfo) -> Bool {
        draggingID = nil
        return true
    }
}

private struct EffectTrackDropDelegate: DropDelegate {
    let targetID: UUID
    let vm: PlayerViewModel
    @Binding var draggingID: UUID?

    func validateDrop(info: DropInfo) -> Bool {
        draggingID != nil
    }

    func dropUpdated(info: DropInfo) -> DropProposal? {
        DropProposal(operation: .move)
    }

    func dropEntered(info: DropInfo) {
        guard let draggingID else { return }
        vm.moveEffectTrack(draggingID, to: targetID)
    }

    func performDrop(info: DropInfo) -> Bool {
        draggingID = nil
        return true
    }
}

private struct RenameItemSheet: View {
    let titleKey: LocalizedStringKey
    let placeholderKey: LocalizedStringKey
    @Binding var name: String
    let onSave: () -> Void
    let onCancel: () -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: 14) {
            Text(titleKey)
                .font(.headline)

            TextField(placeholderKey, text: $name)
                .textFieldStyle(.roundedBorder)

            HStack {
                Spacer()
                Button("action.cancel") {
                    onCancel()
                }
                Button("action.save") {
                    onSave()
                }
                .keyboardShortcut(.defaultAction)
                .disabled(name.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty)
            }
        }
        .padding(20)
        .frame(width: 360)
    }
}
