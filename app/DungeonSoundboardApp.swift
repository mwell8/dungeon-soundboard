import SwiftUI

@main
struct DungeonSoundboardApp: App {
    // App-level locale injection gives instant runtime language switching in SwiftUI views.
    @AppStorage(AppLanguage.userDefaultsKey) private var appLanguageRawValue: String = AppLanguage.defaultLanguage.rawValue

    init() {
        AppTelemetry.shared.installCrashHandlers()
    }

    private var appLanguage: AppLanguage {
        AppLanguage(rawValue: appLanguageRawValue) ?? .defaultLanguage
    }

    var body: some Scene {
        WindowGroup {
            ContentView()
                .tint(DndTheme.accent)
                .environment(\.locale, appLanguage.locale)
        }
    }
}
