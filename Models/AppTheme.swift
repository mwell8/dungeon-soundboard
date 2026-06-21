import Foundation

struct ThemePalette: Codable, Equatable {
    var backgroundTop: ThemeColor
    var backgroundBottom: ThemeColor
    var surfacePrimary: ThemeColor
    var surfaceSecondary: ThemeColor
    var card: ThemeColor
    var cardCurrent: ThemeColor
    var accent: ThemeColor
    var danger: ThemeColor
    var textPrimary: ThemeColor
    var textSecondary: ThemeColor

    private enum CodingKeys: String, CodingKey {
        case backgroundTop
        case backgroundBottom
        case surfacePrimary
        case surfaceSecondary
        case card
        case cardCurrent
        case accent
        case danger
        case textPrimary
        case textSecondary
    }

    init(
        backgroundTop: ThemeColor,
        backgroundBottom: ThemeColor,
        surfacePrimary: ThemeColor,
        surfaceSecondary: ThemeColor,
        card: ThemeColor,
        cardCurrent: ThemeColor,
        accent: ThemeColor,
        danger: ThemeColor,
        textPrimary: ThemeColor,
        textSecondary: ThemeColor
    ) {
        self.backgroundTop = backgroundTop
        self.backgroundBottom = backgroundBottom
        self.surfacePrimary = surfacePrimary
        self.surfaceSecondary = surfaceSecondary
        self.card = card
        self.cardCurrent = cardCurrent
        self.accent = accent
        self.danger = danger
        self.textPrimary = textPrimary
        self.textSecondary = textSecondary
    }

    init(from decoder: Decoder) throws {
        let defaults = ThemeRenderer.defaultTheme.palette
        let container = try decoder.container(keyedBy: CodingKeys.self)
        backgroundTop = try container.decodeIfPresent(ThemeColor.self, forKey: .backgroundTop) ?? defaults.backgroundTop
        backgroundBottom = try container.decodeIfPresent(ThemeColor.self, forKey: .backgroundBottom) ?? defaults.backgroundBottom
        surfacePrimary = try container.decodeIfPresent(ThemeColor.self, forKey: .surfacePrimary) ?? defaults.surfacePrimary
        surfaceSecondary = try container.decodeIfPresent(ThemeColor.self, forKey: .surfaceSecondary) ?? defaults.surfaceSecondary
        card = try container.decodeIfPresent(ThemeColor.self, forKey: .card) ?? defaults.card
        cardCurrent = try container.decodeIfPresent(ThemeColor.self, forKey: .cardCurrent) ?? defaults.cardCurrent
        accent = try container.decodeIfPresent(ThemeColor.self, forKey: .accent) ?? defaults.accent
        danger = try container.decodeIfPresent(ThemeColor.self, forKey: .danger) ?? defaults.danger
        textPrimary = try container.decodeIfPresent(ThemeColor.self, forKey: .textPrimary) ?? defaults.textPrimary
        textSecondary = try container.decodeIfPresent(ThemeColor.self, forKey: .textSecondary) ?? defaults.textSecondary
    }
}

enum BackgroundMode: String, CaseIterable, Codable {
    case none
    case image
}

enum BackgroundLayoutMode: String, CaseIterable, Codable, Identifiable {
    case fill
    case fit
    case center
    case tile

    var id: String { rawValue }
}

struct BackgroundConfig: Codable, Equatable {
    var mode: BackgroundMode
    var imageBookmarkData: Data?
    var imageOriginalPath: String?
    var layoutMode: BackgroundLayoutMode
    var opacity: Double
    var dimOverlay: Double
    var blurRadius: Double

    private enum CodingKeys: String, CodingKey {
        case mode
        case imageBookmarkData
        case imageOriginalPath
        case layoutMode
        case opacity
        case dimOverlay
        case blurRadius
    }

    init(
        mode: BackgroundMode,
        imageBookmarkData: Data?,
        imageOriginalPath: String?,
        layoutMode: BackgroundLayoutMode,
        opacity: Double,
        dimOverlay: Double,
        blurRadius: Double
    ) {
        self.mode = mode
        self.imageBookmarkData = imageBookmarkData
        self.imageOriginalPath = imageOriginalPath
        self.layoutMode = layoutMode
        self.opacity = opacity
        self.dimOverlay = dimOverlay
        self.blurRadius = blurRadius
    }

    init(from decoder: Decoder) throws {
        let defaults = ThemeRenderer.defaultTheme.background
        let container = try decoder.container(keyedBy: CodingKeys.self)
        mode = try container.decodeIfPresent(BackgroundMode.self, forKey: .mode) ?? defaults.mode
        imageBookmarkData = try container.decodeIfPresent(Data.self, forKey: .imageBookmarkData)
        imageOriginalPath = try container.decodeIfPresent(String.self, forKey: .imageOriginalPath)
        layoutMode = try container.decodeIfPresent(BackgroundLayoutMode.self, forKey: .layoutMode) ?? defaults.layoutMode
        opacity = try container.decodeIfPresent(Double.self, forKey: .opacity) ?? defaults.opacity
        dimOverlay = try container.decodeIfPresent(Double.self, forKey: .dimOverlay) ?? defaults.dimOverlay
        blurRadius = try container.decodeIfPresent(Double.self, forKey: .blurRadius) ?? defaults.blurRadius
    }
}

enum InterfaceDensity: String, CaseIterable, Codable, Identifiable {
    case compact
    case normal
    case spacious

    var id: String { rawValue }
}

enum PanelBlurStrength: String, CaseIterable, Codable, Identifiable {
    case low
    case medium
    case high

    var id: String { rawValue }
}

struct UIChromeConfig: Codable, Equatable {
    var accentIntensity: Double
    var panelOpacity: Double
    var panelBlurStrength: PanelBlurStrength
    var cornerRadius: Double
    var density: InterfaceDensity

    private enum CodingKeys: String, CodingKey {
        case accentIntensity
        case panelOpacity
        case panelBlurStrength
        case cornerRadius
        case density
    }

    init(
        accentIntensity: Double,
        panelOpacity: Double,
        panelBlurStrength: PanelBlurStrength,
        cornerRadius: Double,
        density: InterfaceDensity
    ) {
        self.accentIntensity = accentIntensity
        self.panelOpacity = panelOpacity
        self.panelBlurStrength = panelBlurStrength
        self.cornerRadius = cornerRadius
        self.density = density
    }

    init(from decoder: Decoder) throws {
        let defaults = ThemeRenderer.defaultTheme.chrome
        let container = try decoder.container(keyedBy: CodingKeys.self)
        accentIntensity = try container.decodeIfPresent(Double.self, forKey: .accentIntensity) ?? defaults.accentIntensity
        panelOpacity = try container.decodeIfPresent(Double.self, forKey: .panelOpacity) ?? defaults.panelOpacity
        panelBlurStrength = try container.decodeIfPresent(PanelBlurStrength.self, forKey: .panelBlurStrength) ?? defaults.panelBlurStrength
        cornerRadius = try container.decodeIfPresent(Double.self, forKey: .cornerRadius) ?? defaults.cornerRadius
        density = try container.decodeIfPresent(InterfaceDensity.self, forKey: .density) ?? defaults.density
    }
}

enum ThemePreset: String, CaseIterable, Codable, Identifiable {
    case classicDungeon
    case tavernEmber
    case moonlitCrypt
    case forestMist

    var id: String { rawValue }
}

struct CustomThemePreset: Codable, Equatable, Identifiable {
    var id: UUID
    var name: String
    var theme: AppTheme

    init(id: UUID = UUID(), name: String, theme: AppTheme) {
        self.id = id
        self.name = name
        self.theme = theme
    }
}

struct AppTheme: Codable, Equatable {
    var preset: ThemePreset?
    var palette: ThemePalette
    var background: BackgroundConfig
    var chrome: UIChromeConfig

    private enum CodingKeys: String, CodingKey {
        case preset
        case palette
        case background
        case chrome
    }

    init(
        preset: ThemePreset?,
        palette: ThemePalette,
        background: BackgroundConfig,
        chrome: UIChromeConfig
    ) {
        self.preset = preset
        self.palette = palette
        self.background = background
        self.chrome = chrome
    }

    init(from decoder: Decoder) throws {
        let defaults = ThemeRenderer.defaultTheme
        let container = try decoder.container(keyedBy: CodingKeys.self)
        preset = try container.decodeIfPresent(ThemePreset.self, forKey: .preset) ?? defaults.preset
        palette = try container.decodeIfPresent(ThemePalette.self, forKey: .palette) ?? defaults.palette
        background = try container.decodeIfPresent(BackgroundConfig.self, forKey: .background) ?? defaults.background
        chrome = try container.decodeIfPresent(UIChromeConfig.self, forKey: .chrome) ?? defaults.chrome
    }
}
