import SwiftUI

@main
struct DndMusicPlayerMacApp: App {
    // App-level locale injection gives instant runtime language switching in SwiftUI views.
    @AppStorage(AppLanguage.userDefaultsKey) private var appLanguageRawValue: String = AppLanguage.defaultLanguage.rawValue

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
