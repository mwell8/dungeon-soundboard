import Foundation

#if canImport(SwiftUI)
import SwiftUI
#endif

struct ResolvedTheme {
    var preset: ThemePreset?
    var backgroundTop: Color
    var backgroundBottom: Color
    var panel: Color
    var panelAlt: Color
    var card: Color
    var cardCurrent: Color
    var accent: Color
    var accentSoft: Color
    var divider: Color
    var textPrimary: Color
    var textSecondary: Color
    var danger: Color
    var panelOpacity: Double
    var cornerRadius: Double
    var density: InterfaceDensity
    var blurStrength: PanelBlurStrength
    var backgroundOpacity: Double
    var backgroundDimOverlay: Double
    var backgroundBlurRadius: Double
}

struct ThemeRenderer {
    static let defaultTheme = theme(for: .classicDungeon)

    static func theme(for preset: ThemePreset) -> AppTheme {
        switch preset {
        case .classicDungeon:
            return AppTheme(
                preset: preset,
                palette: ThemePalette(
                    backgroundTop: ThemeColor(red: 0.10, green: 0.08, blue: 0.07),
                    backgroundBottom: ThemeColor(red: 0.16, green: 0.12, blue: 0.10),
                    surfacePrimary: ThemeColor(red: 0.18, green: 0.14, blue: 0.12),
                    surfaceSecondary: ThemeColor(red: 0.22, green: 0.17, blue: 0.14),
                    card: ThemeColor(red: 0.24, green: 0.19, blue: 0.16),
                    cardCurrent: ThemeColor(red: 0.32, green: 0.24, blue: 0.17),
                    accent: ThemeColor(red: 0.82, green: 0.67, blue: 0.32),
                    danger: ThemeColor(red: 0.72, green: 0.28, blue: 0.24),
                    textPrimary: ThemeColor(red: 0.95, green: 0.91, blue: 0.84),
                    textSecondary: ThemeColor(red: 0.74, green: 0.69, blue: 0.62)
                ),
                background: BackgroundConfig(
                    mode: .none,
                    imageBookmarkData: nil,
                    imageOriginalPath: nil,
                    layoutMode: .fill,
                    opacity: 0.72,
                    dimOverlay: 0.30,
                    blurRadius: 0
                ),
                chrome: UIChromeConfig(
                    accentIntensity: 0.60,
                    panelOpacity: 0.86,
                    panelBlurStrength: .medium,
                    cornerRadius: 16,
                    density: .normal
                )
            )
        case .tavernEmber:
            return AppTheme(
                preset: preset,
                palette: ThemePalette(
                    backgroundTop: ThemeColor(red: 0.16, green: 0.09, blue: 0.07),
                    backgroundBottom: ThemeColor(red: 0.27, green: 0.14, blue: 0.10),
                    surfacePrimary: ThemeColor(red: 0.24, green: 0.14, blue: 0.11),
                    surfaceSecondary: ThemeColor(red: 0.30, green: 0.17, blue: 0.13),
                    card: ThemeColor(red: 0.36, green: 0.20, blue: 0.15),
                    cardCurrent: ThemeColor(red: 0.45, green: 0.25, blue: 0.16),
                    accent: ThemeColor(red: 0.92, green: 0.58, blue: 0.23),
                    danger: ThemeColor(red: 0.76, green: 0.28, blue: 0.24),
                    textPrimary: ThemeColor(red: 0.97, green: 0.91, blue: 0.84),
                    textSecondary: ThemeColor(red: 0.84, green: 0.73, blue: 0.64)
                ),
                background: BackgroundConfig(
                    mode: .none,
                    imageBookmarkData: nil,
                    imageOriginalPath: nil,
                    layoutMode: .fill,
                    opacity: 0.75,
                    dimOverlay: 0.28,
                    blurRadius: 0
                ),
                chrome: UIChromeConfig(
                    accentIntensity: 0.72,
                    panelOpacity: 0.84,
                    panelBlurStrength: .medium,
                    cornerRadius: 18,
                    density: .normal
                )
            )
        case .moonlitCrypt:
            return AppTheme(
                preset: preset,
                palette: ThemePalette(
                    backgroundTop: ThemeColor(red: 0.05, green: 0.08, blue: 0.14),
                    backgroundBottom: ThemeColor(red: 0.10, green: 0.12, blue: 0.20),
                    surfacePrimary: ThemeColor(red: 0.10, green: 0.13, blue: 0.20),
                    surfaceSecondary: ThemeColor(red: 0.13, green: 0.16, blue: 0.24),
                    card: ThemeColor(red: 0.16, green: 0.19, blue: 0.29),
                    cardCurrent: ThemeColor(red: 0.20, green: 0.24, blue: 0.35),
                    accent: ThemeColor(red: 0.60, green: 0.76, blue: 0.95),
                    danger: ThemeColor(red: 0.82, green: 0.34, blue: 0.37),
                    textPrimary: ThemeColor(red: 0.92, green: 0.96, blue: 0.99),
                    textSecondary: ThemeColor(red: 0.73, green: 0.80, blue: 0.90)
                ),
                background: BackgroundConfig(
                    mode: .none,
                    imageBookmarkData: nil,
                    imageOriginalPath: nil,
                    layoutMode: .fill,
                    opacity: 0.76,
                    dimOverlay: 0.24,
                    blurRadius: 0
                ),
                chrome: UIChromeConfig(
                    accentIntensity: 0.56,
                    panelOpacity: 0.82,
                    panelBlurStrength: .high,
                    cornerRadius: 14,
                    density: .normal
                )
            )
        case .forestMist:
            return AppTheme(
                preset: preset,
                palette: ThemePalette(
                    backgroundTop: ThemeColor(red: 0.07, green: 0.12, blue: 0.10),
                    backgroundBottom: ThemeColor(red: 0.11, green: 0.18, blue: 0.14),
                    surfacePrimary: ThemeColor(red: 0.12, green: 0.18, blue: 0.15),
                    surfaceSecondary: ThemeColor(red: 0.16, green: 0.22, blue: 0.18),
                    card: ThemeColor(red: 0.20, green: 0.26, blue: 0.21),
                    cardCurrent: ThemeColor(red: 0.24, green: 0.31, blue: 0.24),
                    accent: ThemeColor(red: 0.56, green: 0.82, blue: 0.60),
                    danger: ThemeColor(red: 0.79, green: 0.34, blue: 0.30),
                    textPrimary: ThemeColor(red: 0.93, green: 0.97, blue: 0.92),
                    textSecondary: ThemeColor(red: 0.72, green: 0.82, blue: 0.74)
                ),
                background: BackgroundConfig(
                    mode: .none,
                    imageBookmarkData: nil,
                    imageOriginalPath: nil,
                    layoutMode: .fill,
                    opacity: 0.72,
                    dimOverlay: 0.22,
                    blurRadius: 0
                ),
                chrome: UIChromeConfig(
                    accentIntensity: 0.50,
                    panelOpacity: 0.80,
                    panelBlurStrength: .high,
                    cornerRadius: 20,
                    density: .normal
                )
            )
        }
    }

    static func sanitize(_ theme: AppTheme) -> AppTheme {
        var sanitized = theme
        sanitized.background.opacity = clamp(theme.background.opacity, min: 0, max: 1, fallback: 0.72)
        sanitized.background.dimOverlay = clamp(theme.background.dimOverlay, min: 0.15, max: 0.65, fallback: 0.30)
        sanitized.background.blurRadius = clamp(theme.background.blurRadius, min: 0, max: 24, fallback: 0)
        sanitized.chrome.accentIntensity = clamp(theme.chrome.accentIntensity, min: 0, max: 1, fallback: 0.60)
        sanitized.chrome.panelOpacity = clamp(theme.chrome.panelOpacity, min: 0.55, max: 0.98, fallback: 0.86)
        sanitized.chrome.cornerRadius = clamp(theme.chrome.cornerRadius, min: 8, max: 24, fallback: 16)
        sanitized.palette.textPrimary = makeReadable(theme.palette.textPrimary, against: theme.palette.surfacePrimary, minimumContrast: 4.5)
        sanitized.palette.textSecondary = makeReadable(theme.palette.textSecondary, against: theme.palette.surfacePrimary, minimumContrast: 3.2)
        sanitized.palette.cardCurrent = makeReadable(theme.palette.cardCurrent, against: theme.palette.surfacePrimary, minimumContrast: 1.25)
        return sanitized
    }

    static func resolve(_ theme: AppTheme) -> ResolvedTheme {
        let sanitized = sanitize(theme)
        let accentMix = sanitized.chrome.accentIntensity
        let accentSoft = sanitized.palette.surfacePrimary.blended(with: sanitized.palette.accent, amount: 0.20 + 0.35 * accentMix)
        let currentCard = makeReadable(
            sanitized.palette.card.blended(with: sanitized.palette.accent, amount: 0.10 + 0.22 * accentMix),
            against: sanitized.palette.surfacePrimary,
            minimumContrast: 1.18
        )
        let divider = sanitized.palette.textSecondary.withAlpha(0.24 + 0.20 * accentMix)

        return ResolvedTheme(
            preset: sanitized.preset,
            backgroundTop: sanitized.palette.backgroundTop.color,
            backgroundBottom: sanitized.palette.backgroundBottom.color,
            panel: sanitized.palette.surfacePrimary.color,
            panelAlt: sanitized.palette.surfaceSecondary.color,
            card: sanitized.palette.card.color,
            cardCurrent: currentCard.blended(with: sanitized.palette.cardCurrent, amount: 0.45).color,
            accent: sanitized.palette.accent.color,
            accentSoft: accentSoft.color,
            divider: divider.color,
            textPrimary: sanitized.palette.textPrimary.color,
            textSecondary: sanitized.palette.textSecondary.color,
            danger: sanitized.palette.danger.color,
            panelOpacity: sanitized.chrome.panelOpacity,
            cornerRadius: sanitized.chrome.cornerRadius,
            density: sanitized.chrome.density,
            blurStrength: sanitized.chrome.panelBlurStrength,
            backgroundOpacity: sanitized.background.opacity,
            backgroundDimOverlay: sanitized.background.mode == .image ? sanitized.background.dimOverlay : 0,
            backgroundBlurRadius: sanitized.background.blurRadius
        )
    }

    static func clamp(_ value: Double, min minimum: Double, max maximum: Double, fallback: Double) -> Double {
        guard value.isFinite else { return fallback }
        return Swift.min(Swift.max(value, minimum), maximum)
    }

    static func makeReadable(_ foreground: ThemeColor, against background: ThemeColor, minimumContrast: Double) -> ThemeColor {
        guard foreground.contrastRatio(against: background) < minimumContrast else {
            return foreground
        }

        let towardWhite = foreground.blended(with: .white, amount: 0.08)
        let towardBlack = foreground.blended(with: .black, amount: 0.08)

        let whiteDelta = towardWhite.contrastRatio(against: background)
        let blackDelta = towardBlack.contrastRatio(against: background)
        let direction: ThemeColor = whiteDelta >= blackDelta ? .white : .black

        var candidate = foreground
        var iterations = 0
        while candidate.contrastRatio(against: background) < minimumContrast && iterations < 18 {
            candidate = candidate.blended(with: direction, amount: 0.12)
            iterations += 1
        }
        return candidate
    }
}

extension InterfaceDensity {
    var layoutMetrics: LayoutMetrics {
        switch self {
        case .compact:
            return LayoutMetrics(panelPadding: 10, sectionSpacing: 8, rowHeight: 30, controlWidth: 108, cornerCompaction: 0.9)
        case .normal:
            return LayoutMetrics(panelPadding: 12, sectionSpacing: 10, rowHeight: 34, controlWidth: 120, cornerCompaction: 1.0)
        case .spacious:
            return LayoutMetrics(panelPadding: 16, sectionSpacing: 14, rowHeight: 40, controlWidth: 132, cornerCompaction: 1.08)
        }
    }
}

struct LayoutMetrics: Equatable {
    var panelPadding: CGFloat
    var sectionSpacing: CGFloat
    var rowHeight: CGFloat
    var controlWidth: CGFloat
    var cornerCompaction: CGFloat
}
