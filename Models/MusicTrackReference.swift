import Foundation

/// Stable playback identity. A track ID alone is not enough when playback can
/// move independently from the playlist selected in the sidebar.
struct MusicTrackReference: Codable, Hashable {
    let playlistID: UUID
    let trackID: UUID
}
