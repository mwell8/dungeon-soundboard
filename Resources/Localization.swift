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

    private static var activeBundle: Bundle {
        bundle(for: AppLanguage.current) ?? .main
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
