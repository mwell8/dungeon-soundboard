import SwiftUI
import AppKit
import UniformTypeIdentifiers

struct ThemeSettingsView: View {
    @ObservedObject var vm: PlayerViewModel
    @ObservedObject var hotkeyStore: HotkeyStore
    @Binding var appLanguageRawValue: String
    @Binding var isPresented: Bool
    let onCaptureHotkey: (HotkeyAction) -> Void
    @EnvironmentObject private var themeStore: ThemeStore
    @State private var customPresetName = ""
    @State private var selectedPane: SettingsPane = .audio

    private var theme: ResolvedTheme {
        themeStore.resolvedTheme
    }

    private var appLanguage: AppLanguage {
        AppLanguage(rawValue: appLanguageRawValue) ?? .defaultLanguage
    }

    var body: some View {
        VStack(spacing: 0) {
            HStack {
                Text("settings.title")
                    .font(.title2)
                    .bold()
                    .foregroundStyle(theme.textPrimary)
                Spacer()
            }
            .padding(.horizontal, 20)
            .padding(.top, 16)
            .padding(.bottom, 12)

            Picker("settings.title", selection: $selectedPane) {
                ForEach(SettingsPane.allCases) { pane in
                    Text(LocalizedStringKey(pane.localizedKey)).tag(pane)
                }
            }
            .pickerStyle(.segmented)
            .labelsHidden()
            .padding(.horizontal, 20)
            .padding(.bottom, 16)

            Divider()

            ScrollView {
                VStack(alignment: .leading, spacing: 18) {
                    selectedSettingsPaneContent
                }
                .padding(20)
                .frame(maxWidth: .infinity, alignment: .topLeading)
            }

            Divider()

            HStack {
                Spacer()
                Button("action.done") {
                    isPresented = false
                }
                .keyboardShortcut(.defaultAction)
            }
            .padding(16)
        }
        .frame(minWidth: 660, minHeight: 560)
        .background(theme.panel.opacity(theme.panelOpacity))
        .environment(\.locale, appLanguage.locale)
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
    }

    @ViewBuilder
    private var selectedSettingsPaneContent: some View {
        switch selectedPane {
        case .audio:
            playbackSection
        case .interface:
            presetsSection
            colorsSection
            backgroundSection
            interfaceSection
            visualResetSection
        case .hotkeys:
            hotkeysSection
        case .system:
            languageSection
            telemetrySection
            aboutSection
        }
    }

    private var presetsSection: some View {
        settingsSection("theme.presets.title") {
            HStack(spacing: 10) {
                Text("theme.presets.builtin")
                    .foregroundStyle(theme.textPrimary)
                Menu {
                    ForEach(ThemePreset.allCases) { preset in
                        Button {
                            themeStore.applyPreset(preset)
                        } label: {
                            Text(LocalizedStringKey(preset.localizedKey))
                        }
                    }
                } label: {
                    HStack(spacing: 8) {
                        Text(currentPresetTitle)
                        Image(systemName: "chevron.down")
                            .font(.caption)
                    }
                    .frame(minWidth: 180, alignment: .leading)
                }
                .menuStyle(.borderedButton)
                Spacer()
            }

            HStack(spacing: 8) {
                TextField("theme.presets.custom_name", text: $customPresetName)
                    .textFieldStyle(.roundedBorder)
                Button("theme.presets.save_custom") {
                    themeStore.saveCurrentAsCustomPreset(named: customPresetName)
                    customPresetName = ""
                }
                .disabled(customPresetName.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty)
            }

            if !themeStore.customPresets.isEmpty {
                LazyVGrid(columns: presetColumns, alignment: .leading, spacing: 8) {
                    ForEach(themeStore.customPresets) { preset in
                        customPresetRow(preset)
                    }
                }
            }
        }
    }

    private var colorsSection: some View {
        settingsSection("theme.colors.title") {
            LazyVGrid(columns: colorColumns, alignment: .leading, spacing: 10) {
                colorPicker("theme.color.background_top", value: themeStore.theme.palette.backgroundTop)
                colorPicker("theme.color.background_bottom", value: themeStore.theme.palette.backgroundBottom)
                colorPicker("theme.color.surface_primary", value: themeStore.theme.palette.surfacePrimary)
                colorPicker("theme.color.surface_secondary", value: themeStore.theme.palette.surfaceSecondary)
                colorPicker("theme.color.card", value: themeStore.theme.palette.card)
                colorPicker("theme.color.card_current", value: themeStore.theme.palette.cardCurrent)
                colorPicker("theme.color.accent", value: themeStore.theme.palette.accent)
                colorPicker("theme.color.text_primary", value: themeStore.theme.palette.textPrimary)
                colorPicker("theme.color.text_secondary", value: themeStore.theme.palette.textSecondary)
                colorPicker("theme.color.danger", value: themeStore.theme.palette.danger)
            }
        }
    }

    private var backgroundSection: some View {
        settingsSection("theme.background.title") {
            HStack {
                Button("theme.background.choose") {
                    chooseBackgroundImage()
                }
                Button("theme.background.clear") {
                    themeStore.clearBackground()
                }
                .disabled(themeStore.theme.background.mode == .none)
                Spacer()
            }

            if let path = themeStore.theme.background.imageOriginalPath, themeStore.theme.background.mode == .image {
                Text(path)
                    .font(.caption)
                    .foregroundStyle(theme.textSecondary)
                    .textSelection(.enabled)
            }

            Picker("theme.background.layout", selection: Binding(
                get: { themeStore.theme.background.layoutMode },
                set: { value in
                    themeStore.updateBackground { $0.layoutMode = value }
                }
            )) {
                ForEach(BackgroundLayoutMode.allCases) { mode in
                    Text(LocalizedStringKey(mode.localizedKey)).tag(mode)
                }
            }
            .pickerStyle(.segmented)

            sliderRow("theme.background.opacity", value: themeStore.theme.background.opacity, range: 0...1) { value in
                themeStore.updateBackground { $0.opacity = value }
            }

            sliderRow("theme.background.dim", value: themeStore.theme.background.dimOverlay, range: 0.15...0.65) { value in
                themeStore.updateBackground { $0.dimOverlay = value }
            }

            sliderRow("theme.background.blur", value: themeStore.theme.background.blurRadius, range: 0...24) { value in
                themeStore.updateBackground { $0.blurRadius = value }
            }
        }
    }

    private var interfaceSection: some View {
        settingsSection("theme.interface.title") {
            Picker("theme.interface.density", selection: Binding(
                get: { themeStore.theme.chrome.density },
                set: { value in
                    themeStore.updateChrome { $0.density = value }
                }
            )) {
                ForEach(InterfaceDensity.allCases) { density in
                    Text(LocalizedStringKey(density.localizedKey)).tag(density)
                }
            }
            .pickerStyle(.segmented)

            Picker("theme.interface.blur", selection: Binding(
                get: { themeStore.theme.chrome.panelBlurStrength },
                set: { value in
                    themeStore.updateChrome { $0.panelBlurStrength = value }
                }
            )) {
                ForEach(PanelBlurStrength.allCases) { blur in
                    Text(LocalizedStringKey(blur.localizedKey)).tag(blur)
                }
            }
            .pickerStyle(.segmented)

            sliderRow("theme.interface.corner_radius", value: themeStore.theme.chrome.cornerRadius, range: 8...24) { value in
                themeStore.updateChrome { $0.cornerRadius = value }
            }

            sliderRow("theme.interface.panel_opacity", value: themeStore.theme.chrome.panelOpacity, range: 0.55...0.98) { value in
                themeStore.updateChrome { $0.panelOpacity = value }
            }

            sliderRow("theme.interface.accent_intensity", value: themeStore.theme.chrome.accentIntensity, range: 0...1) { value in
                themeStore.updateChrome { $0.accentIntensity = value }
            }
        }
    }

    private var playbackSection: some View {
        settingsSection("settings.playback.title") {
            sliderRow("settings.ducking", value: vm.duckingAmount, range: 0.2...1.0) { value in
                vm.duckingAmount = value
            }

            Picker("settings.music_columns", selection: $vm.musicColumnsCount) {
                Text("2").tag(2)
                Text("3").tag(3)
                Text("4").tag(4)
            }
            .pickerStyle(.segmented)

            Picker("settings.effects_columns", selection: $vm.effectsColumnsCount) {
                Text("2").tag(2)
                Text("3").tag(3)
                Text("4").tag(4)
            }
            .pickerStyle(.segmented)

            Toggle("settings.music_fade_out_on_pause", isOn: $vm.isMusicFadeOutOnPauseEnabled)
                .foregroundStyle(theme.textPrimary)
        }
    }

    private var hotkeysSection: some View {
        VStack(alignment: .leading, spacing: 18) {
            settingsSection("hotkeys.system.title") {
                VStack(spacing: 0) {
                    ForEach(HotkeyAction.systemActions) { action in
                        hotkeySettingsRow(action)
                        Divider()
                    }
                }

                Button("hotkeys.restore_defaults") {
                    hotkeyStore.restoreDefaults()
                }
            }

            settingsSection("hotkeys.assigned.title") {
                let bindings = assignedTrackBindings
                if bindings.isEmpty {
                    Text("hotkeys.empty")
                        .foregroundStyle(theme.textSecondary)
                } else {
                    VStack(spacing: 0) {
                        ForEach(bindings) { binding in
                            hotkeySettingsRow(binding.action)
                            Divider()
                        }
                    }
                }
            }
        }
    }

    private var telemetrySection: some View {
        settingsSection("settings.telemetry.title") {
            Toggle("settings.telemetry.sentry_enabled", isOn: $vm.isSentryTelemetryEnabled)
                .foregroundStyle(theme.textPrimary)
            TextField("settings.telemetry.sentry_dsn", text: $vm.sentryDSN)
                .textFieldStyle(.roundedBorder)
                .disabled(!vm.isSentryTelemetryEnabled)
        }
    }

    private var languageSection: some View {
        settingsSection("settings.language.title") {
            Picker("settings.language", selection: $appLanguageRawValue) {
                ForEach(AppLanguage.allCases) { language in
                    Text(LocalizedStringKey(language.displayNameKey))
                        .tag(language.rawValue)
                }
            }
            .pickerStyle(.segmented)
        }
    }

    private var visualResetSection: some View {
        settingsSection("theme.advanced.title") {
            HStack {
                Button("theme.reset") {
                    themeStore.resetTheme()
                }
                Button("theme.restore_defaults") {
                    themeStore.restoreVisualDefaults()
                }
                Spacer()
            }
        }
    }

    private var aboutSection: some View {
        settingsSection("settings.about.title") {
            HStack(spacing: 0) {
                Text("Made by MWell on ")
                    .foregroundStyle(theme.textPrimary)
                if let repositoryURL = URL(string: "https://github.com/mwell8/dungeon-soundboard") {
                    Link("github", destination: repositoryURL)
                }
            }

            Text("settings.about.version")
                .foregroundStyle(theme.textSecondary)
        }
    }

    private var colorColumns: [GridItem] {
        [
            GridItem(.flexible(minimum: 220), spacing: 14),
            GridItem(.flexible(minimum: 220), spacing: 14)
        ]
    }

    private var presetColumns: [GridItem] {
        [
            GridItem(.flexible(minimum: 220), spacing: 10),
            GridItem(.flexible(minimum: 220), spacing: 10)
        ]
    }

    private var assignedTrackBindings: [HotkeyBinding] {
        hotkeyStore.bindings
            .filter { !$0.action.isSystemAction }
            .sorted {
                hotkeyActionTitle($0.action, vm: vm).localizedCaseInsensitiveCompare(hotkeyActionTitle($1.action, vm: vm)) == .orderedAscending
            }
    }

    private var currentPresetTitle: LocalizedStringKey {
        if let preset = themeStore.theme.preset {
            return LocalizedStringKey(preset.localizedKey)
        }
        return "theme.preset.custom_current"
    }

    private func settingsSection<Content: View>(_ titleKey: String, @ViewBuilder content: () -> Content) -> some View {
        VStack(alignment: .leading, spacing: 10) {
            Text(LocalizedStringKey(titleKey))
                .font(.headline)
                .foregroundStyle(theme.textPrimary)
            content()
        }
        .padding(14)
        .background(theme.panelAlt.opacity(theme.panelOpacity))
        .clipShape(RoundedRectangle(cornerRadius: CGFloat(theme.cornerRadius)))
    }

    private func hotkeySettingsRow(_ action: HotkeyAction) -> some View {
        let hotkey = hotkeyStore.hotkey(for: action)

        return HStack(spacing: 12) {
            VStack(alignment: .leading, spacing: 4) {
                Text(hotkeyActionTitle(action, vm: vm))
                    .foregroundStyle(theme.textPrimary)
                    .lineLimit(1)

                Text(hotkey?.displayText ?? L10n.tr("hotkeys.unassigned"))
                    .font(.caption)
                    .foregroundStyle(theme.textSecondary)
            }

            Spacer()

            Button {
                onCaptureHotkey(action)
            } label: {
                Text(LocalizedStringKey(hotkey == nil ? "hotkeys.assign" : "hotkeys.change"))
            }

            Button("hotkeys.clear") {
                hotkeyStore.clear(action)
            }
            .disabled(hotkey == nil)
        }
        .padding(.vertical, 8)
    }

    private func customPresetRow(_ preset: CustomThemePreset) -> some View {
        HStack(spacing: 8) {
            Button {
                themeStore.applyCustomPreset(preset)
            } label: {
                Text(preset.name)
                    .lineLimit(1)
                    .frame(maxWidth: .infinity, alignment: .leading)
            }
            .buttonStyle(.bordered)

            Button(role: .destructive) {
                themeStore.deleteCustomPreset(preset)
            } label: {
                Image(systemName: "trash")
            }
            .buttonStyle(.borderless)
            .help("theme.presets.delete_custom")
        }
    }

    private func colorPicker(_ key: String, value: ThemeColor) -> some View {
        HStack(spacing: 8) {
            Text(LocalizedStringKey(key))
                .lineLimit(1)
            Spacer()
            ColorPicker(
                LocalizedStringKey(key),
                selection: Binding(
                    get: { value.color },
                    set: { color in
                        let nsColor = NSColor(color)
                        guard let converted = nsColor.usingColorSpace(.deviceRGB) else { return }
                        let newColor = ThemeColor(
                            red: converted.redComponent,
                            green: converted.greenComponent,
                            blue: converted.blueComponent,
                            alpha: converted.alphaComponent
                        )
                        themeStore.updatePalette { palette in
                            switch key {
                            case "theme.color.background_top": palette.backgroundTop = newColor
                            case "theme.color.background_bottom": palette.backgroundBottom = newColor
                            case "theme.color.surface_primary": palette.surfacePrimary = newColor
                            case "theme.color.surface_secondary": palette.surfaceSecondary = newColor
                            case "theme.color.card": palette.card = newColor
                            case "theme.color.card_current": palette.cardCurrent = newColor
                            case "theme.color.accent": palette.accent = newColor
                            case "theme.color.text_primary": palette.textPrimary = newColor
                            case "theme.color.text_secondary": palette.textSecondary = newColor
                            case "theme.color.danger": palette.danger = newColor
                            default: break
                            }
                        }
                    }
                ),
                supportsOpacity: true
            )
            .labelsHidden()
        }
        .frame(minHeight: 28)
        .foregroundStyle(theme.textPrimary)
    }

    private func sliderRow(_ key: String, value: Double, range: ClosedRange<Double>, update: @escaping (Double) -> Void) -> some View {
        VStack(alignment: .leading, spacing: 6) {
            HStack {
                Text(LocalizedStringKey(key))
                    .foregroundStyle(theme.textPrimary)
                Spacer()
                Text(String(format: value > 1 ? "%.0f" : "%.0f%%", value > 1 ? value : value * 100))
                    .foregroundStyle(theme.textSecondary)
            }
            Slider(
                value: Binding(
                    get: { value },
                    set: { update($0) }
                ),
                in: range
            )
        }
    }

    private func chooseBackgroundImage() {
        let panel = NSOpenPanel()
        panel.allowsMultipleSelection = false
        panel.canChooseDirectories = false
        panel.canChooseFiles = true
        panel.allowedContentTypes = [
            .png,
            .jpeg,
            UTType(filenameExtension: "webp"),
            .heic,
            .tiff
        ].compactMap { $0 }
        if panel.runModal() == .OK, let url = panel.url {
            themeStore.setBackgroundImage(url: url)
        }
    }
}

private extension ThemePreset {
    var localizedKey: String {
        switch self {
        case .classicDungeon: return "theme.preset.classic_dungeon"
        case .tavernEmber: return "theme.preset.tavern_ember"
        case .moonlitCrypt: return "theme.preset.moonlit_crypt"
        case .forestMist: return "theme.preset.forest_mist"
        }
    }
}

private enum SettingsPane: String, CaseIterable, Identifiable {
    case audio
    case interface
    case hotkeys
    case system

    var id: String { rawValue }

    var localizedKey: String {
        switch self {
        case .audio: return "settings.group.audio"
        case .interface: return "settings.group.interface"
        case .hotkeys: return "settings.group.hotkeys"
        case .system: return "settings.group.system"
        }
    }
}

private extension BackgroundLayoutMode {
    var localizedKey: String {
        switch self {
        case .fill: return "theme.background.layout.fill"
        case .fit: return "theme.background.layout.fit"
        case .center: return "theme.background.layout.center"
        case .tile: return "theme.background.layout.tile"
        }
    }
}

private extension InterfaceDensity {
    var localizedKey: String {
        switch self {
        case .compact: return "theme.interface.density.compact"
        case .normal: return "theme.interface.density.normal"
        case .spacious: return "theme.interface.density.spacious"
        }
    }
}

private extension PanelBlurStrength {
    var localizedKey: String {
        switch self {
        case .low: return "theme.interface.blur.low"
        case .medium: return "theme.interface.blur.medium"
        case .high: return "theme.interface.blur.high"
        }
    }
}
