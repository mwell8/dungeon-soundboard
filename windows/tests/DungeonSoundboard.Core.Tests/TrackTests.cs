using System.Text.Json;
using DungeonSoundboard.Core.Models;
using DungeonSoundboard.Core.Serialization;
using Xunit;

namespace DungeonSoundboard.Core.Tests;

public sealed class TrackTests
{
    [Fact]
    public void DecodingLegacyTrackDefaultsVolumeMultiplierToOne()
    {
        const string json = """
        {
          "id": "00000000-0000-0000-0000-000000000001",
          "title": "Rain",
          "path": "C:\\audio\\rain.mp3",
          "role": "music"
        }
        """;

        var track = JsonSerializer.Deserialize<Track>(json, JsonDefaults.Options);

        Assert.NotNull(track);
        Assert.Equal(1.0, track.VolumeMultiplier);
    }

    [Fact]
    public void VolumeMultiplierIsClamped()
    {
        var quiet = new Track("Quiet", "C:\\audio\\quiet.mp3", volumeMultiplier: -2);
        Assert.Equal(0, quiet.VolumeMultiplier);

        quiet.VolumeMultiplier = 3;
        Assert.Equal(2, quiet.VolumeMultiplier);

        var invalid = new Track("Invalid", "C:\\audio\\invalid.mp3", volumeMultiplier: double.NaN);
        Assert.Equal(1, invalid.VolumeMultiplier);
    }

    [Fact]
    public void NormalizedTitleTrimsAndFallsBack()
    {
        Assert.Equal("Tavern Rain", Track.NormalizedTitle("  Tavern Rain  ", "Rain"));
        Assert.Equal("Rain", Track.NormalizedTitle("   ", "Rain"));
    }

    [Fact]
    public void OutputVolumeUsesMasterTrackMultiplierAndDucking()
    {
        Assert.True(Math.Abs(Track.OutputVolume(0.5, 1.5, 0.5) - 0.375) < 0.0001);
        Assert.True(Math.Abs(Track.OutputVolume(1.0, 2.0) - 1.0) < 0.0001);
        Assert.True(Math.Abs(Track.OutputVolume(0.2, 2.0) - 0.4) < 0.0001);
    }

    [Fact]
    public void MoveTrackReordersIds()
    {
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var thirdId = Guid.NewGuid();
        var tracks = new List<Track>
        {
            new("First", "C:\\audio\\first.mp3", id: firstId),
            new("Second", "C:\\audio\\second.mp3", id: secondId),
            new("Third", "C:\\audio\\third.mp3", id: thirdId)
        };

        Assert.True(Track.MoveTrack(tracks, firstId, thirdId));
        Assert.Equal([secondId, thirdId, firstId], tracks.Select(track => track.Id).ToArray());

        Assert.True(Track.MoveTrack(tracks, firstId, secondId));
        Assert.Equal([firstId, secondId, thirdId], tracks.Select(track => track.Id).ToArray());
    }
}
