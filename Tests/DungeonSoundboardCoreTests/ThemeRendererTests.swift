import XCTest
@testable import DungeonSoundboardCore

final class ThemeRendererTests: XCTestCase {
    func testThemeRoundTripPreservesValues() throws {
        let original = ThemeRenderer.theme(for: .forestMist)
        let data = try JSONEncoder().encode(original)
        let decoded = try JSONDecoder().decode(AppTheme.self, from: data)

        XCTAssertEqual(decoded, original)
    }

    func testThemeDecodeFallsBackForMissingFields() throws {
        let json = #"{"preset":"classicDungeon","palette":{"accent":{"red":0.2,"green":0.4,"blue":0.6,"alpha":1}}}"#
        let decoded = try JSONDecoder().decode(AppTheme.self, from: Data(json.utf8))

        XCTAssertEqual(decoded.preset, .classicDungeon)
        XCTAssertEqual(decoded.palette.accent, ThemeColor(red: 0.2, green: 0.4, blue: 0.6))
        XCTAssertEqual(decoded.palette.backgroundTop, ThemeRenderer.defaultTheme.palette.backgroundTop)
        XCTAssertEqual(decoded.chrome.cornerRadius, ThemeRenderer.defaultTheme.chrome.cornerRadius)
    }

    func testPresetApplicationProvidesExpectedIdentity() {
        let tavern = ThemeRenderer.theme(for: .tavernEmber)

        XCTAssertEqual(tavern.preset, .tavernEmber)
        XCTAssertEqual(tavern.chrome.panelBlurStrength, .medium)
        XCTAssertEqual(tavern.chrome.density, .normal)
    }

    func testSanitizeClampsUnsafeNumericValues() {
        var theme = ThemeRenderer.defaultTheme
        theme.background.opacity = 99
        theme.background.dimOverlay = -10
        theme.background.blurRadius = 100
        theme.chrome.accentIntensity = -4
        theme.chrome.panelOpacity = 0.1
        theme.chrome.cornerRadius = 50

        let sanitized = ThemeRenderer.sanitize(theme)

        XCTAssertEqual(sanitized.background.opacity, 1)
        XCTAssertEqual(sanitized.background.dimOverlay, 0.15)
        XCTAssertEqual(sanitized.background.blurRadius, 24)
        XCTAssertEqual(sanitized.chrome.accentIntensity, 0)
        XCTAssertEqual(sanitized.chrome.panelOpacity, 0.55)
        XCTAssertEqual(sanitized.chrome.cornerRadius, 24)
    }

    func testReadabilityGuardrailsRaiseContrast() {
        let surface = ThemeColor(red: 0.15, green: 0.15, blue: 0.15)
        let text = ThemeColor(red: 0.16, green: 0.16, blue: 0.16)

        let improved = ThemeRenderer.makeReadable(text, against: surface, minimumContrast: 4.5)

        XCTAssertGreaterThanOrEqual(improved.contrastRatio(against: surface), 4.5)
    }

    func testBackgroundConfigSurvivesCoding() throws {
        let config = BackgroundConfig(
            mode: .image,
            imageBookmarkData: Data([0x01, 0x02]),
            imageOriginalPath: "/tmp/bg.png",
            layoutMode: .tile,
            opacity: 0.5,
            dimOverlay: 0.4,
            blurRadius: 6
        )

        let data = try JSONEncoder().encode(config)
        let decoded = try JSONDecoder().decode(BackgroundConfig.self, from: data)

        XCTAssertEqual(decoded, config)
    }
}
