using System.Text.Json;
using DungeonSoundboard.Core.Models;
using DungeonSoundboard.Core.Serialization;
using Xunit;

namespace DungeonSoundboard.Core.Tests;

public sealed class ThemeRendererTests
{
    [Fact]
    public void ThemeRoundTripPreservesValues()
    {
        var original = ThemeRenderer.ThemeFor(ThemePreset.ForestMist);
        var data = JsonSerializer.SerializeToUtf8Bytes(original, JsonDefaults.Options);
        var decoded = JsonSerializer.Deserialize<AppTheme>(data, JsonDefaults.Options);

        Assert.Equal(original, decoded);
    }

    [Fact]
    public void ThemeDecodeFallsBackForMissingFields()
    {
        const string json = """
        {"preset":"classicDungeon","palette":{"accent":{"red":0.2,"green":0.4,"blue":0.6,"alpha":1}}}
        """;

        var decoded = JsonSerializer.Deserialize<AppTheme>(json, JsonDefaults.Options);

        Assert.NotNull(decoded);
        Assert.Equal(ThemePreset.ClassicDungeon, decoded.Preset);
        Assert.Equal(new ThemeColor(0.2, 0.4, 0.6), decoded.Palette.Accent);
        Assert.Equal(ThemeRenderer.DefaultTheme.Palette.BackgroundTop, decoded.Palette.BackgroundTop);
        Assert.Equal(ThemeRenderer.DefaultTheme.Chrome.CornerRadius, decoded.Chrome.CornerRadius);
    }

    [Fact]
    public void PresetApplicationProvidesExpectedIdentity()
    {
        var tavern = ThemeRenderer.ThemeFor(ThemePreset.TavernEmber);

        Assert.Equal(ThemePreset.TavernEmber, tavern.Preset);
        Assert.Equal(PanelBlurStrength.Medium, tavern.Chrome.PanelBlurStrength);
        Assert.Equal(InterfaceDensity.Normal, tavern.Chrome.Density);
    }

    [Fact]
    public void SanitizeClampsUnsafeNumericValues()
    {
        var theme = ThemeRenderer.DefaultTheme;
        theme.Background.Opacity = 99;
        theme.Background.DimOverlay = -10;
        theme.Background.BlurRadius = 100;
        theme.Chrome.AccentIntensity = -4;
        theme.Chrome.PanelOpacity = 0.1;
        theme.Chrome.CornerRadius = 50;

        var sanitized = ThemeRenderer.Sanitize(theme);

        Assert.Equal(1, sanitized.Background.Opacity);
        Assert.Equal(0.15, sanitized.Background.DimOverlay);
        Assert.Equal(24, sanitized.Background.BlurRadius);
        Assert.Equal(0, sanitized.Chrome.AccentIntensity);
        Assert.Equal(0.55, sanitized.Chrome.PanelOpacity);
        Assert.Equal(24, sanitized.Chrome.CornerRadius);
    }

    [Fact]
    public void ReadabilityGuardrailsRaiseContrast()
    {
        var surface = new ThemeColor(0.15, 0.15, 0.15);
        var text = new ThemeColor(0.16, 0.16, 0.16);

        var improved = ThemeRenderer.MakeReadable(text, surface, 4.5);

        Assert.True(improved.ContrastRatio(surface) >= 4.5);
    }

    [Fact]
    public void BackgroundConfigSurvivesCoding()
    {
        var config = new BackgroundConfig
        {
            Mode = BackgroundMode.Image,
            ImageBookmarkData = [0x01, 0x02],
            ImageOriginalPath = "C:\\images\\bg.png",
            LayoutMode = BackgroundLayoutMode.Tile,
            Opacity = 0.5,
            DimOverlay = 0.4,
            BlurRadius = 6
        };

        var data = JsonSerializer.SerializeToUtf8Bytes(config, JsonDefaults.Options);
        var decoded = JsonSerializer.Deserialize<BackgroundConfig>(data, JsonDefaults.Options);

        Assert.Equal(config, decoded);
    }
}
