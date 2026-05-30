import XCTest
@testable import DungeonSoundboardCore

final class RepeatModeTests: XCTestCase {
    func testFromStoredValueSupportsCurrentValues() {
        XCTAssertEqual(RepeatMode.fromStoredValue("off"), .off)
        XCTAssertEqual(RepeatMode.fromStoredValue("one"), .one)
        XCTAssertEqual(RepeatMode.fromStoredValue("all"), .all)
    }

    func testFromStoredValueSupportsLegacyLocalizedValues() {
        XCTAssertEqual(RepeatMode.fromStoredValue("Без повтора"), .off)
        XCTAssertEqual(RepeatMode.fromStoredValue("Повтор трека"), .one)
        XCTAssertEqual(RepeatMode.fromStoredValue("Повтор плейлиста"), .all)
    }

    func testLocalizedKeysAreStable() {
        XCTAssertEqual(RepeatMode.off.localizedKey, "repeat.off")
        XCTAssertEqual(RepeatMode.one.localizedKey, "repeat.one")
        XCTAssertEqual(RepeatMode.all.localizedKey, "repeat.all")
    }
}
