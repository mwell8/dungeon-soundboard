import SwiftUI

@main
struct DungeonSoundboardApp: App {
    // Локаль на уровне приложения даёт мгновенное переключение языка в SwiftUI.
    @AppStorage(AppLanguage.userDefaultsKey) private var appLanguageRawValue: String = AppLanguage.defaultLanguage.rawValue
    @StateObject private var themeStore = ThemeStore()

    init() {
        AppTelemetry.shared.installCrashHandlers()
    }

    private var appLanguage: AppLanguage {
        AppLanguage(rawValue: appLanguageRawValue) ?? .defaultLanguage
    }

    var body: some Scene {
        WindowGroup {
            ContentView()
                .environmentObject(themeStore)
                .tint(themeStore.resolvedTheme.accent)
                .environment(\.locale, appLanguage.locale)
        }
    }
}
