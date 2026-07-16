using System.Globalization;
using DungeonSoundboard.Core.Models;
using DungeonSoundboard.Core.Services;
using Xunit;

namespace DungeonSoundboard.Core.Tests;

public sealed class PlayerPreferencesTests
{
    [Theory]
    [InlineData("ru-RU", AppLanguage.Russian)]
    [InlineData("ru", AppLanguage.Russian)]
    [InlineData("en-US", AppLanguage.English)]
    [InlineData("de-DE", AppLanguage.English)]
    public void DefaultLanguageFollowsSupportedWindowsCulture(string cultureName, AppLanguage expected)
    {
        Assert.Equal(expected, PlayerPreferences.DefaultLanguageForCulture(CultureInfo.GetCultureInfo(cultureName)));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 0)]
    [InlineData(0.55, 0.55)]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    public void DuckingAmountSupportsTheFullAttenuationRange(double value, double expected)
    {
        Assert.Equal(expected, PlayerPreferences.ClampDucking(value));
    }

    [Theory]
    [InlineData(40, 220, 96)]
    [InlineData(260, 220, 260)]
    [InlineData(900, 220, 720)]
    [InlineData(double.NaN, 220, 220)]
    public void SectionHeightIsPersistedWithinUsableBounds(double value, double fallback, double expected)
    {
        Assert.Equal(expected, PlayerPreferences.NormalizeSectionHeight(value, fallback));
    }
}
