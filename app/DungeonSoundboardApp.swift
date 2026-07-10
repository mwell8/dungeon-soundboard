import SwiftUI

@main
struct DungeonSoundboardApp: App {
    // Локаль на уровне приложения даёт мгновенное переключение языка в SwiftUI.
    @AppStorage(AppLanguage.userDefaultsKey) private var appLanguageRawValue: String = AppLanguage.defaultLanguage.rawValue
    @StateObject private var themeStore = ThemeStore()
    @StateObject private var playerViewModel = PlayerViewModel()
    @StateObject private var hotkeyStore = HotkeyStore()

    init() {
        AppTelemetry.shared.installCrashHandlers()
    }

    private var appLanguage: AppLanguage {
        AppLanguage(rawValue: appLanguageRawValue) ?? .defaultLanguage
    }

    var body: some Scene {
        Window("app.title", id: "main") {
            ContentView(vm: playerViewModel, hotkeyStore: hotkeyStore)
                .environmentObject(themeStore)
                .tint(themeStore.resolvedTheme.accent)
                .environment(\.locale, appLanguage.locale)
        }
    }
}
