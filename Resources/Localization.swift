import Foundation

enum AppLanguage: String, CaseIterable, Identifiable {
    case english = "en"
    case russian = "ru"

    static let userDefaultsKey = "app_language"
    static let defaultLanguage: AppLanguage = .english

    var id: String { rawValue }

    var locale: Locale {
        Locale(identifier: rawValue)
    }

    var displayNameKey: String {
        switch self {
        case .english:
            return "settings.language.english"
        case .russian:
            return "settings.language.russian"
        }
    }

    static var current: AppLanguage {
        guard let rawValue = UserDefaults.standard.string(forKey: userDefaultsKey),
              let language = AppLanguage(rawValue: rawValue) else {
            return defaultLanguage
        }
        return language
    }
}

enum L10n {
    static func tr(_ key: String) -> String {
        tr(key, language: AppLanguage.current)
    }

    static func tr(_ key: String, language: AppLanguage) -> String {
        let activeBundle = bundle(for: language) ?? .main
        let localized = activeBundle.localizedString(forKey: key, value: nil, table: "Localizable")
        if localized != key {
            return localized
        }
        return englishBundle.localizedString(forKey: key, value: key, table: "Localizable")
    }

    static func tr(_ key: String, _ arguments: CVarArg...) -> String {
        let format = tr(key)
        return String(format: format, locale: AppLanguage.current.locale, arguments: arguments)
    }

    static func translations(for key: String) -> Set<String> {
        Set(AppLanguage.allCases.map { tr(key, language: $0) })
    }

    private static var englishBundle: Bundle {
        bundle(for: .english) ?? .main
    }

    private static func bundle(for language: AppLanguage) -> Bundle? {
        guard let path = Bundle.main.path(forResource: language.rawValue, ofType: "lproj") else {
            return nil
        }
        return Bundle(path: path)
    }
}
