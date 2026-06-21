import Foundation
import AppKit
import Combine
import SwiftUI
import UniformTypeIdentifiers

final class ThemeStore: ObservableObject {
    @Published private(set) var theme: AppTheme {
        didSet {
            resolvedTheme = ThemeRenderer.resolve(theme)
            saveTheme()
        }
    }

    @Published private(set) var resolvedTheme: ResolvedTheme
    @Published private(set) var backgroundImage: NSImage?
    @Published private(set) var customPresets: [CustomThemePreset]
    @Published var errorMessage: String?

    private let defaults: UserDefaults

    init(defaults: UserDefaults = .standard) {
        self.defaults = defaults
        customPresets = ThemeStore.loadCustomPresets(defaults: defaults)
        if let stored = ThemeStore.loadStoredTheme(defaults: defaults) {
            let sanitized = ThemeRenderer.sanitize(stored)
            theme = sanitized
            resolvedTheme = ThemeRenderer.resolve(sanitized)
        } else {
            theme = ThemeRenderer.defaultTheme
            resolvedTheme = ThemeRenderer.resolve(ThemeRenderer.defaultTheme)
        }
        loadBackgroundImageIfNeeded()
    }

    var layoutMetrics: LayoutMetrics {
        resolvedTheme.density.layoutMetrics
    }

    func applyPreset(_ preset: ThemePreset) {
        updateTheme(ThemeRenderer.theme(for: preset))
        syncBackgroundImageAfterThemeChange()
    }

    func applyCustomPreset(_ preset: CustomThemePreset) {
        var updated = preset.theme
        updated.preset = nil
        updateTheme(updated)
        syncBackgroundImageAfterThemeChange()
    }

    func saveCurrentAsCustomPreset(named name: String) {
        let trimmedName = name.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmedName.isEmpty else { return }

        var presetTheme = theme
        presetTheme.preset = nil
        customPresets.append(CustomThemePreset(name: trimmedName, theme: presetTheme))
        saveCustomPresets()
    }

    func deleteCustomPreset(_ preset: CustomThemePreset) {
        customPresets.removeAll { $0.id == preset.id }
        saveCustomPresets()
    }

    func resetTheme() {
        var updated = ThemeRenderer.defaultTheme
        updated.background = theme.background
        updateTheme(updated)
    }

    func restoreVisualDefaults() {
        updateTheme(ThemeRenderer.defaultTheme)
        backgroundImage = nil
    }

    func clearBackground() {
        backgroundImage = nil
        var updated = theme
        updated.background.mode = .none
        updated.background.imageBookmarkData = nil
        updated.background.imageOriginalPath = nil
        updated.background.blurRadius = 0
        updateTheme(updated)
    }

    func updatePalette(_ mutate: (inout ThemePalette) -> Void) {
        var updated = theme
        updated.preset = nil
        mutate(&updated.palette)
        updateTheme(updated)
    }

    func updateChrome(_ mutate: (inout UIChromeConfig) -> Void) {
        var updated = theme
        updated.preset = nil
        mutate(&updated.chrome)
        updateTheme(updated)
    }

    func updateBackground(_ mutate: (inout BackgroundConfig) -> Void) {
        var updated = theme
        updated.preset = nil
        mutate(&updated.background)
        updateTheme(updated)
        if updated.background.mode == .none {
            backgroundImage = nil
        }
    }

    func setBackgroundImage(url: URL) {
        guard let image = NSImage(contentsOf: url) else {
            errorMessage = L10n.tr("theme.background.error.invalid")
            return
        }

        let didStartAccessing = url.startAccessingSecurityScopedResource()
        defer {
            if didStartAccessing {
                url.stopAccessingSecurityScopedResource()
            }
        }

        guard let bookmarkData = try? url.bookmarkData(
            options: [.withSecurityScope, .securityScopeAllowOnlyReadAccess],
            includingResourceValuesForKeys: nil,
            relativeTo: nil
        ) else {
            errorMessage = L10n.tr("theme.background.error.bookmark")
            return
        }

        backgroundImage = image
        var updated = theme
        updated.preset = nil
        updated.background.mode = .image
        updated.background.imageBookmarkData = bookmarkData
        updated.background.imageOriginalPath = url.path
        updateTheme(updated)
    }

    private func updateTheme(_ updated: AppTheme) {
        theme = ThemeRenderer.sanitize(updated)
    }

    private func saveTheme() {
        do {
            let data = try JSONEncoder().encode(theme)
            defaults.set(data, forKey: PlayerDefaultsKeys.theme)
        } catch {
            errorMessage = L10n.tr("theme.error.save")
        }
    }

    private func saveCustomPresets() {
        do {
            let data = try JSONEncoder().encode(customPresets)
            defaults.set(data, forKey: PlayerDefaultsKeys.customThemePresets)
        } catch {
            errorMessage = L10n.tr("theme.error.save")
        }
    }

    private func syncBackgroundImageAfterThemeChange() {
        guard theme.background.mode == .image else {
            backgroundImage = nil
            return
        }
        backgroundImage = nil
        loadBackgroundImageIfNeeded()
    }

    private func loadBackgroundImageIfNeeded() {
        guard theme.background.mode == .image,
              let bookmarkData = theme.background.imageBookmarkData else {
            return
        }

        var isStale = false
        do {
            let url = try URL(
                resolvingBookmarkData: bookmarkData,
                options: [.withSecurityScope, .withoutUI],
                relativeTo: nil,
                bookmarkDataIsStale: &isStale
            )
            let didStartAccessing = url.startAccessingSecurityScopedResource()
            defer {
                if didStartAccessing {
                    url.stopAccessingSecurityScopedResource()
                }
            }

            guard let image = NSImage(contentsOf: url) else {
                clearBrokenBackground(with: L10n.tr("theme.background.error.load"))
                return
            }

            if isStale,
               let refreshed = try? url.bookmarkData(
                    options: [.withSecurityScope, .securityScopeAllowOnlyReadAccess],
                    includingResourceValuesForKeys: nil,
                    relativeTo: nil
               ) {
                var updated = theme
                updated.background.imageBookmarkData = refreshed
                theme = ThemeRenderer.sanitize(updated)
            }

            backgroundImage = image
        } catch {
            clearBrokenBackground(with: L10n.tr("theme.background.error.load"))
        }
    }

    private func clearBrokenBackground(with message: String) {
        backgroundImage = nil
        errorMessage = message
        var updated = theme
        updated.background.mode = .none
        updated.background.imageBookmarkData = nil
        updated.background.imageOriginalPath = nil
        theme = ThemeRenderer.sanitize(updated)
    }

    private static func loadStoredTheme(defaults: UserDefaults) -> AppTheme? {
        guard let data = defaults.data(forKey: PlayerDefaultsKeys.theme) else {
            return nil
        }
        return try? JSONDecoder().decode(AppTheme.self, from: data)
    }

    private static func loadCustomPresets(defaults: UserDefaults) -> [CustomThemePreset] {
        guard let data = defaults.data(forKey: PlayerDefaultsKeys.customThemePresets) else {
            return []
        }
        let presets = (try? JSONDecoder().decode([CustomThemePreset].self, from: data)) ?? []
        return presets.map { preset in
            CustomThemePreset(
                id: preset.id,
                name: preset.name,
                theme: ThemeRenderer.sanitize(preset.theme)
            )
        }
    }
}
