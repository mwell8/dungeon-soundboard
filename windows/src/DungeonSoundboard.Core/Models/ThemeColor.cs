namespace DungeonSoundboard.Core.Models;

public sealed class ThemeColor : IEquatable<ThemeColor>
{
    private double _red;
    private double _green;
    private double _blue;
    private double _alpha = 1;

    public double Red
    {
        get => _red;
        set => _red = ClampUnit(value);
    }

    public double Green
    {
        get => _green;
        set => _green = ClampUnit(value);
    }

    public double Blue
    {
        get => _blue;
        set => _blue = ClampUnit(value);
    }

    public double Alpha
    {
        get => _alpha;
        set => _alpha = ClampUnit(value);
    }

    public ThemeColor()
    {
    }

    public ThemeColor(double red, double green, double blue, double alpha = 1)
    {
        Red = red;
        Green = green;
        Blue = blue;
        Alpha = alpha;
    }

    public static ThemeColor White => new(1, 1, 1);
    public static ThemeColor Black => new(0, 0, 0);

    public ThemeColor Blended(ThemeColor color, double amount)
    {
        var ratio = ClampUnit(amount);
        return new ThemeColor(
            Red + (color.Red - Red) * ratio,
            Green + (color.Green - Green) * ratio,
            Blue + (color.Blue - Blue) * ratio,
            Alpha + (color.Alpha - Alpha) * ratio);
    }

    public ThemeColor WithAlpha(double alpha) => new(Red, Green, Blue, alpha);

    public double RelativeLuminance
    {
        get
        {
            static double Transform(double component)
            {
                return component <= 0.03928
                    ? component / 12.92
                    : Math.Pow((component + 0.055) / 1.055, 2.4);
            }

            return 0.2126 * Transform(Red) + 0.7152 * Transform(Green) + 0.0722 * Transform(Blue);
        }
    }

    public double ContrastRatio(ThemeColor other)
    {
        var lhs = RelativeLuminance + 0.05;
        var rhs = other.RelativeLuminance + 0.05;
        return Math.Max(lhs, rhs) / Math.Min(lhs, rhs);
    }

    public static double ClampUnit(double value)
    {
        if (!double.IsFinite(value))
        {
            return 0;
        }

        return Math.Min(Math.Max(value, 0), 1);
    }

    public bool Equals(ThemeColor? other)
    {
        if (other is null)
        {
            return false;
        }

        return Red.Equals(other.Red)
            && Green.Equals(other.Green)
            && Blue.Equals(other.Blue)
            && Alpha.Equals(other.Alpha);
    }

    public override bool Equals(object? obj) => Equals(obj as ThemeColor);

    public override int GetHashCode() => HashCode.Combine(Red, Green, Blue, Alpha);
}
