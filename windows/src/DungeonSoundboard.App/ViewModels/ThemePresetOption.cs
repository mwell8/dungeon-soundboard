using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using DungeonSoundboard.Core.Models;

namespace DungeonSoundboard.App.ViewModels;

public sealed class ThemePresetOption(ThemePreset preset, Func<string> getName) : ObservableObject
{
    public ThemePreset Preset { get; } = preset;

    public string Name => getName();

    private ResolvedTheme ResolvedTheme => ThemeRenderer.Resolve(ThemeRenderer.ThemeFor(Preset));

    public IBrush SwatchBackgroundBrush => ThemeSwatchBrush.ToBrush(ResolvedTheme.BackgroundBottom);

    public IBrush SwatchPanelBrush => ThemeSwatchBrush.ToBrush(ResolvedTheme.Panel);

    public IBrush SwatchAccentBrush => ThemeSwatchBrush.ToBrush(ResolvedTheme.Accent);

    public void Refresh()
    {
        OnPropertyChanged(nameof(Name));
    }
}

public sealed record CustomThemePresetOption(Guid Id, string Name, CustomThemePreset Preset)
{
    private ResolvedTheme ResolvedTheme => ThemeRenderer.Resolve(Preset.Theme);

    public IBrush SwatchBackgroundBrush => ThemeSwatchBrush.ToBrush(ResolvedTheme.BackgroundBottom);

    public IBrush SwatchPanelBrush => ThemeSwatchBrush.ToBrush(ResolvedTheme.Panel);

    public IBrush SwatchAccentBrush => ThemeSwatchBrush.ToBrush(ResolvedTheme.Accent);
}

internal static class ThemeSwatchBrush
{
    public static SolidColorBrush ToBrush(ThemeColor color)
    {
        static byte Byte(double value) => (byte)Math.Round(ThemeColor.ClampUnit(value) * 255);
        return new SolidColorBrush(Color.FromArgb(Byte(color.Alpha), Byte(color.Red), Byte(color.Green), Byte(color.Blue)));
    }
}
