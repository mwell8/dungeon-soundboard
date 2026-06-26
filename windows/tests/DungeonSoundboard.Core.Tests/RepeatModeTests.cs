using DungeonSoundboard.Core.Models;
using Xunit;

namespace DungeonSoundboard.Core.Tests;

public sealed class RepeatModeTests
{
    [Fact]
    public void FromStoredValueSupportsCurrentValues()
    {
        Assert.Equal(RepeatMode.Off, RepeatModeExtensions.FromStoredValue("off"));
        Assert.Equal(RepeatMode.One, RepeatModeExtensions.FromStoredValue("one"));
        Assert.Equal(RepeatMode.All, RepeatModeExtensions.FromStoredValue("all"));
    }

    [Fact]
    public void FromStoredValueSupportsLegacyLocalizedValues()
    {
        Assert.Equal(RepeatMode.Off, RepeatModeExtensions.FromStoredValue("Без повтора"));
        Assert.Equal(RepeatMode.One, RepeatModeExtensions.FromStoredValue("Повтор трека"));
        Assert.Equal(RepeatMode.All, RepeatModeExtensions.FromStoredValue("Повтор плейлиста"));
    }

    [Fact]
    public void LocalizedKeysAreStable()
    {
        Assert.Equal("repeat.off", RepeatMode.Off.LocalizedKey());
        Assert.Equal("repeat.one", RepeatMode.One.LocalizedKey());
        Assert.Equal("repeat.all", RepeatMode.All.LocalizedKey());
    }
}
