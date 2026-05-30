// swift-tools-version: 6.0
import PackageDescription

let package = Package(
    name: "DungeonSoundboardCore",
    platforms: [
        .macOS(.v13)
    ],
    products: [
        .library(
            name: "DungeonSoundboardCore",
            targets: ["DungeonSoundboardCore"]
        )
    ],
    targets: [
        .target(
            name: "DungeonSoundboardCore",
            path: "Models",
            sources: [
                "Track.swift",
                "Playlist.swift",
                "EffectPlaylist.swift",
                "RepeatMode.swift",
                "PlayerDefaultsKeys.swift",
                "PlaylistMigration.swift"
            ]
        ),
        .testTarget(
            name: "DungeonSoundboardCoreTests",
            dependencies: ["DungeonSoundboardCore"],
            path: "Tests/DungeonSoundboardCoreTests"
        )
    ]
)
