using System.Text.Json.Serialization;

namespace DungeonSoundboard.Core.Models;

public sealed class ThemePalette : IEquatable<ThemePalette>
{
    public ThemeColor BackgroundTop { get; set; } = new(0.102, 0.078, 0.071);
    public ThemeColor BackgroundBottom { get; set; } = new(0.161, 0.122, 0.102);
    public ThemeColor SurfacePrimary { get; set; } = new(0.180, 0.141, 0.122);
    public ThemeColor SurfaceSecondary { get; set; } = new(0.220, 0.169, 0.141);
    public ThemeColor Card { get; set; } = new(0.239, 0.188, 0.161);
    public ThemeColor CardCurrent { get; set; } = new(0.322, 0.239, 0.169);
    public ThemeColor Accent { get; set; } = new(0.820, 0.671, 0.322);
    public ThemeColor Danger { get; set; } = new(0.722, 0.278, 0.239);
    public ThemeColor TextPrimary { get; set; } = new(0.949, 0.910, 0.839);
    public ThemeColor TextSecondary { get; set; } = new(0.741, 0.690, 0.620);

    public bool Equals(ThemePalette? other)
    {
        return other is not null
            && BackgroundTop.Equals(other.BackgroundTop)
            && BackgroundBottom.Equals(other.BackgroundBottom)
            && SurfacePrimary.Equals(other.SurfacePrimary)
            && SurfaceSecondary.Equals(other.SurfaceSecondary)
            && Card.Equals(other.Card)
            && CardCurrent.Equals(other.CardCurrent)
            && Accent.Equals(other.Accent)
            && Danger.Equals(other.Danger)
            && TextPrimary.Equals(other.TextPrimary)
            && TextSecondary.Equals(other.TextSecondary);
    }

    public override bool Equals(object? obj) => Equals(obj as ThemePalette);

    public override int GetHashCode() => HashCode.Combine(BackgroundTop, BackgroundBottom, SurfacePrimary, Accent, TextPrimary);
}

[JsonConverter(typeof(JsonStringEnumConverter<BackgroundMode>))]
public enum BackgroundMode
{
    [JsonStringEnumMemberName("none")]
    None,

    [JsonStringEnumMemberName("image")]
    Image
}

[JsonConverter(typeof(JsonStringEnumConverter<BackgroundLayoutMode>))]
public enum BackgroundLayoutMode
{
    [JsonStringEnumMemberName("fill")]
    Fill,

    [JsonStringEnumMemberName("fit")]
    Fit,

    [JsonStringEnumMemberName("center")]
    Center,

    [JsonStringEnumMemberName("tile")]
    Tile
}

public sealed class BackgroundConfig : IEquatable<BackgroundConfig>
{
    public BackgroundMode Mode { get; set; } = BackgroundMode.None;
    public byte[]? ImageBookmarkData { get; set; }
    public string? ImageOriginalPath { get; set; }
    public BackgroundLayoutMode LayoutMode { get; set; } = BackgroundLayoutMode.Fill;
    public double Opacity { get; set; } = 0.72;
    public double DimOverlay { get; set; } = 0.30;
    public double BlurRadius { get; set; }

    public bool Equals(BackgroundConfig? other)
    {
        if (other is null)
        {
            return false;
        }

        return Mode == other.Mode
            && BytesEqual(ImageBookmarkData, other.ImageBookmarkData)
            && ImageOriginalPath == other.ImageOriginalPath
            && LayoutMode == other.LayoutMode
            && Opacity.Equals(other.Opacity)
            && DimOverlay.Equals(other.DimOverlay)
            && BlurRadius.Equals(other.BlurRadius);
    }

    public override bool Equals(object? obj) => Equals(obj as BackgroundConfig);

    public override int GetHashCode() => HashCode.Combine(Mode, ImageOriginalPath, LayoutMode, Opacity, DimOverlay, BlurRadius);

    private static bool BytesEqual(byte[]? left, byte[]? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        return left.SequenceEqual(right);
    }
}

[JsonConverter(typeof(JsonStringEnumConverter<InterfaceDensity>))]
public enum InterfaceDensity
{
    [JsonStringEnumMemberName("compact")]
    Compact,

    [JsonStringEnumMemberName("normal")]
    Normal,

    [JsonStringEnumMemberName("spacious")]
    Spacious
}

[JsonConverter(typeof(JsonStringEnumConverter<PanelBlurStrength>))]
public enum PanelBlurStrength
{
    [JsonStringEnumMemberName("low")]
    Low,

    [JsonStringEnumMemberName("medium")]
    Medium,

    [JsonStringEnumMemberName("high")]
    High
}

public sealed class UIChromeConfig : IEquatable<UIChromeConfig>
{
    public double AccentIntensity { get; set; } = 0.60;
    public double PanelOpacity { get; set; } = 0.86;
    public PanelBlurStrength PanelBlurStrength { get; set; } = PanelBlurStrength.Medium;
    public double CornerRadius { get; set; } = 16;
    public InterfaceDensity Density { get; set; } = InterfaceDensity.Normal;

    public bool Equals(UIChromeConfig? other)
    {
        return other is not null
            && AccentIntensity.Equals(other.AccentIntensity)
            && PanelOpacity.Equals(other.PanelOpacity)
            && PanelBlurStrength == other.PanelBlurStrength
            && CornerRadius.Equals(other.CornerRadius)
            && Density == other.Density;
    }

    public override bool Equals(object? obj) => Equals(obj as UIChromeConfig);

    public override int GetHashCode() => HashCode.Combine(AccentIntensity, PanelOpacity, PanelBlurStrength, CornerRadius, Density);
}

[JsonConverter(typeof(JsonStringEnumConverter<ThemePreset>))]
public enum ThemePreset
{
    [JsonStringEnumMemberName("classicDungeon")]
    ClassicDungeon,

    [JsonStringEnumMemberName("tavernEmber")]
    TavernEmber,

    [JsonStringEnumMemberName("moonlitCrypt")]
    MoonlitCrypt,

    [JsonStringEnumMemberName("forestMist")]
    ForestMist
}

public sealed class CustomThemePreset : IEquatable<CustomThemePreset>
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public AppTheme Theme { get; set; } = ThemeRenderer.DefaultTheme;

    public bool Equals(CustomThemePreset? other)
    {
        return other is not null && Id == other.Id && Name == other.Name && Theme.Equals(other.Theme);
    }

    public override bool Equals(object? obj) => Equals(obj as CustomThemePreset);

    public override int GetHashCode() => HashCode.Combine(Id, Name, Theme);
}

public sealed class AppTheme : IEquatable<AppTheme>
{
    public ThemePreset? Preset { get; set; } = ThemePreset.ClassicDungeon;
    public ThemePalette Palette { get; set; } = new();
    public BackgroundConfig Background { get; set; } = new();
    public UIChromeConfig Chrome { get; set; } = new();

    public AppTheme Clone()
    {
        return new AppTheme
        {
            Preset = Preset,
            Palette = new ThemePalette
            {
                BackgroundTop = new ThemeColor(Palette.BackgroundTop.Red, Palette.BackgroundTop.Green, Palette.BackgroundTop.Blue, Palette.BackgroundTop.Alpha),
                BackgroundBottom = new ThemeColor(Palette.BackgroundBottom.Red, Palette.BackgroundBottom.Green, Palette.BackgroundBottom.Blue, Palette.BackgroundBottom.Alpha),
                SurfacePrimary = new ThemeColor(Palette.SurfacePrimary.Red, Palette.SurfacePrimary.Green, Palette.SurfacePrimary.Blue, Palette.SurfacePrimary.Alpha),
                SurfaceSecondary = new ThemeColor(Palette.SurfaceSecondary.Red, Palette.SurfaceSecondary.Green, Palette.SurfaceSecondary.Blue, Palette.SurfaceSecondary.Alpha),
                Card = new ThemeColor(Palette.Card.Red, Palette.Card.Green, Palette.Card.Blue, Palette.Card.Alpha),
                CardCurrent = new ThemeColor(Palette.CardCurrent.Red, Palette.CardCurrent.Green, Palette.CardCurrent.Blue, Palette.CardCurrent.Alpha),
                Accent = new ThemeColor(Palette.Accent.Red, Palette.Accent.Green, Palette.Accent.Blue, Palette.Accent.Alpha),
                Danger = new ThemeColor(Palette.Danger.Red, Palette.Danger.Green, Palette.Danger.Blue, Palette.Danger.Alpha),
                TextPrimary = new ThemeColor(Palette.TextPrimary.Red, Palette.TextPrimary.Green, Palette.TextPrimary.Blue, Palette.TextPrimary.Alpha),
                TextSecondary = new ThemeColor(Palette.TextSecondary.Red, Palette.TextSecondary.Green, Palette.TextSecondary.Blue, Palette.TextSecondary.Alpha)
            },
            Background = new BackgroundConfig
            {
                Mode = Background.Mode,
                ImageBookmarkData = Background.ImageBookmarkData?.ToArray(),
                ImageOriginalPath = Background.ImageOriginalPath,
                LayoutMode = Background.LayoutMode,
                Opacity = Background.Opacity,
                DimOverlay = Background.DimOverlay,
                BlurRadius = Background.BlurRadius
            },
            Chrome = new UIChromeConfig
            {
                AccentIntensity = Chrome.AccentIntensity,
                PanelOpacity = Chrome.PanelOpacity,
                PanelBlurStrength = Chrome.PanelBlurStrength,
                CornerRadius = Chrome.CornerRadius,
                Density = Chrome.Density
            }
        };
    }

    public bool Equals(AppTheme? other)
    {
        return other is not null
            && Preset == other.Preset
            && Palette.Equals(other.Palette)
            && Background.Equals(other.Background)
            && Chrome.Equals(other.Chrome);
    }

    public override bool Equals(object? obj) => Equals(obj as AppTheme);

    public override int GetHashCode() => HashCode.Combine(Preset, Palette, Background, Chrome);
}
