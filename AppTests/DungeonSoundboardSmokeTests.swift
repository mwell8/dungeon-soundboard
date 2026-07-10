import XCTest
@testable import Dungeon_Soundboard

final class DungeonSoundboardSmokeTests: XCTestCase {
    func testAppModuleLoads() {
        XCTAssertEqual(RepeatMode.off.rawValue, "off")
    }
}
