import Foundation

#if canImport(AppKit)
import AppKit
#endif

#if canImport(SwiftUI)
import SwiftUI
#endif

struct ThemeColor: Codable, Equatable {
    var red: Double
    var green: Double
    var blue: Double
    var alpha: Double

    init(red: Double, green: Double, blue: Double, alpha: Double = 1) {
        self.red = ThemeColor.clampUnit(red)
        self.green = ThemeColor.clampUnit(green)
        self.blue = ThemeColor.clampUnit(blue)
        self.alpha = ThemeColor.clampUnit(alpha)
    }

    private enum CodingKeys: String, CodingKey {
        case red
        case green
        case blue
        case alpha
    }

    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        let red = try container.decodeIfPresent(Double.self, forKey: .red) ?? 0
        let green = try container.decodeIfPresent(Double.self, forKey: .green) ?? 0
        let blue = try container.decodeIfPresent(Double.self, forKey: .blue) ?? 0
        let alpha = try container.decodeIfPresent(Double.self, forKey: .alpha) ?? 1
        self.init(red: red, green: green, blue: blue, alpha: alpha)
    }

    func encode(to encoder: Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(red, forKey: .red)
        try container.encode(green, forKey: .green)
        try container.encode(blue, forKey: .blue)
        try container.encode(alpha, forKey: .alpha)
    }

    func blended(with color: ThemeColor, amount: Double) -> ThemeColor {
        let ratio = ThemeColor.clampUnit(amount)
        return ThemeColor(
            red: red + (color.red - red) * ratio,
            green: green + (color.green - green) * ratio,
            blue: blue + (color.blue - blue) * ratio,
            alpha: alpha + (color.alpha - alpha) * ratio
        )
    }

    func withAlpha(_ alpha: Double) -> ThemeColor {
        ThemeColor(red: red, green: green, blue: blue, alpha: alpha)
    }

    var relativeLuminance: Double {
        func transform(_ component: Double) -> Double {
            if component <= 0.03928 {
                return component / 12.92
            }
            return pow((component + 0.055) / 1.055, 2.4)
        }

        let r = transform(red)
        let g = transform(green)
        let b = transform(blue)
        return 0.2126 * r + 0.7152 * g + 0.0722 * b
    }

    func contrastRatio(against other: ThemeColor) -> Double {
        let lhs = relativeLuminance + 0.05
        let rhs = other.relativeLuminance + 0.05
        return max(lhs, rhs) / min(lhs, rhs)
    }

    static func clampUnit(_ value: Double) -> Double {
        guard value.isFinite else { return 0 }
        return min(max(value, 0), 1)
    }
}

extension ThemeColor {
    static let white = ThemeColor(red: 1, green: 1, blue: 1)
    static let black = ThemeColor(red: 0, green: 0, blue: 0)
}

#if canImport(AppKit)
extension ThemeColor {
    var nsColor: NSColor {
        NSColor(
            red: red,
            green: green,
            blue: blue,
            alpha: alpha
        )
    }
}
#endif

#if canImport(SwiftUI)
extension ThemeColor {
    var color: Color {
        Color(red: red, green: green, blue: blue, opacity: alpha)
    }
}
#endif
