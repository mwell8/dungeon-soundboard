namespace DungeonSoundboard.Core.Models;

public sealed record LayoutMetrics(
    double PanelPadding,
    double SectionSpacing,
    double RowHeight,
    double ControlWidth,
    double CornerCompaction);

public static class InterfaceDensityExtensions
{
    public static LayoutMetrics LayoutMetrics(this InterfaceDensity density)
    {
        return density switch
        {
            InterfaceDensity.Compact => new LayoutMetrics(10, 8, 30, 108, 0.9),
            InterfaceDensity.Spacious => new LayoutMetrics(16, 14, 40, 132, 1.08),
            _ => new LayoutMetrics(12, 10, 34, 120, 1.0)
        };
    }
}
