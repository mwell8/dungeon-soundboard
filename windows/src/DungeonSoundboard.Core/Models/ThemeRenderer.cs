namespace DungeonSoundboard.Core.Models;

public sealed record ResolvedTheme(
    ThemePreset? Preset,
    ThemeColor BackgroundTop,
    ThemeColor BackgroundBottom,
    ThemeColor Panel,
    ThemeColor PanelAlt,
    ThemeColor Card,
    ThemeColor CardCurrent,
    ThemeColor Accent,
    ThemeColor AccentSoft,
    ThemeColor Divider,
    ThemeColor TextPrimary,
    ThemeColor TextSecondary,
    ThemeColor Danger,
    double PanelOpacity,
    double CornerRadius,
    InterfaceDensity Density,
    PanelBlurStrength BlurStrength,
    double BackgroundOpacity,
    double BackgroundDimOverlay,
    double BackgroundBlurRadius);

public static class ThemeRenderer
{
    public static AppTheme DefaultTheme => ThemeFor(ThemePreset.ClassicDungeon);

    public static AppTheme ThemeFor(ThemePreset preset)
    {
        return preset switch
        {
            ThemePreset.TavernEmber => new AppTheme
            {
                Preset = preset,
                Palette = new ThemePalette
                {
                    BackgroundTop = new ThemeColor(0.16, 0.09, 0.07),
                    BackgroundBottom = new ThemeColor(0.27, 0.14, 0.10),
                    SurfacePrimary = new ThemeColor(0.24, 0.14, 0.11),
                    SurfaceSecondary = new ThemeColor(0.30, 0.17, 0.13),
                    Card = new ThemeColor(0.36, 0.20, 0.15),
                    CardCurrent = new ThemeColor(0.45, 0.25, 0.16),
                    Accent = new ThemeColor(0.92, 0.58, 0.23),
                    Danger = new ThemeColor(0.76, 0.28, 0.24),
                    TextPrimary = new ThemeColor(0.97, 0.91, 0.84),
                    TextSecondary = new ThemeColor(0.84, 0.73, 0.64)
                },
                Background = new BackgroundConfig { Opacity = 0.75, DimOverlay = 0.28 },
                Chrome = new UIChromeConfig { AccentIntensity = 0.72, PanelOpacity = 0.84, CornerRadius = 18 }
            },
            ThemePreset.MoonlitCrypt => new AppTheme
            {
                Preset = preset,
                Palette = new ThemePalette
                {
                    BackgroundTop = new ThemeColor(0.05, 0.08, 0.14),
                    BackgroundBottom = new ThemeColor(0.10, 0.12, 0.20),
                    SurfacePrimary = new ThemeColor(0.10, 0.13, 0.20),
                    SurfaceSecondary = new ThemeColor(0.13, 0.16, 0.24),
                    Card = new ThemeColor(0.16, 0.19, 0.29),
                    CardCurrent = new ThemeColor(0.20, 0.24, 0.35),
                    Accent = new ThemeColor(0.60, 0.76, 0.95),
                    Danger = new ThemeColor(0.82, 0.34, 0.37),
                    TextPrimary = new ThemeColor(0.92, 0.96, 0.99),
                    TextSecondary = new ThemeColor(0.73, 0.80, 0.90)
                },
                Background = new BackgroundConfig { Opacity = 0.76, DimOverlay = 0.24 },
                Chrome = new UIChromeConfig { AccentIntensity = 0.56, PanelOpacity = 0.82, CornerRadius = 14, PanelBlurStrength = PanelBlurStrength.High }
            },
            ThemePreset.ForestMist => new AppTheme
            {
                Preset = preset,
                Palette = new ThemePalette
                {
                    BackgroundTop = new ThemeColor(0.07, 0.12, 0.10),
                    BackgroundBottom = new ThemeColor(0.11, 0.18, 0.14),
                    SurfacePrimary = new ThemeColor(0.12, 0.18, 0.15),
                    SurfaceSecondary = new ThemeColor(0.16, 0.22, 0.18),
                    Card = new ThemeColor(0.20, 0.26, 0.21),
                    CardCurrent = new ThemeColor(0.24, 0.31, 0.24),
                    Accent = new ThemeColor(0.56, 0.82, 0.60),
                    Danger = new ThemeColor(0.79, 0.34, 0.30),
                    TextPrimary = new ThemeColor(0.93, 0.97, 0.92),
                    TextSecondary = new ThemeColor(0.72, 0.82, 0.74)
                },
                Background = new BackgroundConfig { Opacity = 0.72, DimOverlay = 0.22 },
                Chrome = new UIChromeConfig { AccentIntensity = 0.50, PanelOpacity = 0.80, CornerRadius = 20, PanelBlurStrength = PanelBlurStrength.High }
            },
            _ => new AppTheme()
        };
    }

    public static AppTheme Sanitize(AppTheme theme)
    {
        var sanitized = theme.Clone();
        sanitized.Background.Opacity = Clamp(theme.Background.Opacity, 0, 1, 0.72);
        sanitized.Background.DimOverlay = Clamp(theme.Background.DimOverlay, 0.15, 0.65, 0.30);
        sanitized.Background.BlurRadius = Clamp(theme.Background.BlurRadius, 0, 24, 0);
        sanitized.Chrome.AccentIntensity = Clamp(theme.Chrome.AccentIntensity, 0, 1, 0.60);
        sanitized.Chrome.PanelOpacity = Clamp(theme.Chrome.PanelOpacity, 0.55, 0.98, 0.86);
        sanitized.Chrome.CornerRadius = Clamp(theme.Chrome.CornerRadius, 8, 24, 16);
        sanitized.Palette.TextPrimary = MakeReadable(theme.Palette.TextPrimary, theme.Palette.SurfacePrimary, 4.5);
        sanitized.Palette.TextSecondary = MakeReadable(theme.Palette.TextSecondary, theme.Palette.SurfacePrimary, 3.2);
        sanitized.Palette.CardCurrent = MakeReadable(theme.Palette.CardCurrent, theme.Palette.SurfacePrimary, 1.25);
        return sanitized;
    }

    public static ResolvedTheme Resolve(AppTheme theme)
    {
        var sanitized = Sanitize(theme);
        var accentMix = sanitized.Chrome.AccentIntensity;
        var accentSoft = sanitized.Palette.SurfacePrimary.Blended(sanitized.Palette.Accent, 0.20 + 0.35 * accentMix);
        var currentCard = MakeReadable(
            sanitized.Palette.Card.Blended(sanitized.Palette.Accent, 0.10 + 0.22 * accentMix),
            sanitized.Palette.SurfacePrimary,
            1.18);
        var divider = sanitized.Palette.TextSecondary.WithAlpha(0.24 + 0.20 * accentMix);

        return new ResolvedTheme(
            sanitized.Preset,
            sanitized.Palette.BackgroundTop,
            sanitized.Palette.BackgroundBottom,
            sanitized.Palette.SurfacePrimary,
            sanitized.Palette.SurfaceSecondary,
            sanitized.Palette.Card,
            currentCard.Blended(sanitized.Palette.CardCurrent, 0.45),
            sanitized.Palette.Accent,
            accentSoft,
            divider,
            sanitized.Palette.TextPrimary,
            sanitized.Palette.TextSecondary,
            sanitized.Palette.Danger,
            sanitized.Chrome.PanelOpacity,
            sanitized.Chrome.CornerRadius,
            sanitized.Chrome.Density,
            sanitized.Chrome.PanelBlurStrength,
            sanitized.Background.Opacity,
            sanitized.Background.Mode == BackgroundMode.Image ? sanitized.Background.DimOverlay : 0,
            sanitized.Background.BlurRadius);
    }

    public static double Clamp(double value, double minimum, double maximum, double fallback)
    {
        if (!double.IsFinite(value))
        {
            return fallback;
        }

        return Math.Min(Math.Max(value, minimum), maximum);
    }

    public static ThemeColor MakeReadable(ThemeColor foreground, ThemeColor background, double minimumContrast)
    {
        if (foreground.ContrastRatio(background) >= minimumContrast)
        {
            return foreground;
        }

        var towardWhite = foreground.Blended(ThemeColor.White, 0.08);
        var towardBlack = foreground.Blended(ThemeColor.Black, 0.08);
        var direction = towardWhite.ContrastRatio(background) >= towardBlack.ContrastRatio(background)
            ? ThemeColor.White
            : ThemeColor.Black;

        var candidate = foreground;
        var iterations = 0;
        while (candidate.ContrastRatio(background) < minimumContrast && iterations < 18)
        {
            candidate = candidate.Blended(direction, 0.12);
            iterations++;
        }

        return candidate;
    }
}
