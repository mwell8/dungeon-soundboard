using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using DungeonSoundboard.Core.Models;

namespace DungeonSoundboard.App.ViewModels;

public sealed class ThemePaletteColorOption(
    Func<string> getName,
    Func<ThemeColor> getColor,
    Action commitChange) : ObservableObject
{
    public string Name => getName();

    public double Red
    {
        get => getColor().Red;
        set => SetChannel(ColorChannel.Red, value);
    }

    public double Green
    {
        get => getColor().Green;
        set => SetChannel(ColorChannel.Green, value);
    }

    public double Blue
    {
        get => getColor().Blue;
        set => SetChannel(ColorChannel.Blue, value);
    }

    public Color Color
    {
        get
        {
            var color = getColor();
            return Color.FromArgb(Byte(color.Alpha), Byte(color.Red), Byte(color.Green), Byte(color.Blue));
        }
        set
        {
            var color = getColor();
            var red = value.R / 255d;
            var green = value.G / 255d;
            var blue = value.B / 255d;
            var alpha = value.A / 255d;
            if (Math.Abs(color.Red - red) < 0.0001
                && Math.Abs(color.Green - green) < 0.0001
                && Math.Abs(color.Blue - blue) < 0.0001
                && Math.Abs(color.Alpha - alpha) < 0.0001)
            {
                return;
            }

            color.Red = red;
            color.Green = green;
            color.Blue = blue;
            color.Alpha = alpha;
            commitChange();
        }
    }

    public string RedValueText => ByteText(Red);

    public string GreenValueText => ByteText(Green);

    public string BlueValueText => ByteText(Blue);

    public string RedChannelName => $"{Name} red channel";

    public string GreenChannelName => $"{Name} green channel";

    public string BlueChannelName => $"{Name} blue channel";

    public string HexText => $"#{Byte(getColor().Alpha):X2}{Byte(Red):X2}{Byte(Green):X2}{Byte(Blue):X2}";

    public IBrush SwatchBrush => ThemeSwatchBrush.ToBrush(getColor());

    public void Refresh()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Red));
        OnPropertyChanged(nameof(Green));
        OnPropertyChanged(nameof(Blue));
        OnPropertyChanged(nameof(Color));
        OnPropertyChanged(nameof(RedValueText));
        OnPropertyChanged(nameof(GreenValueText));
        OnPropertyChanged(nameof(BlueValueText));
        OnPropertyChanged(nameof(RedChannelName));
        OnPropertyChanged(nameof(GreenChannelName));
        OnPropertyChanged(nameof(BlueChannelName));
        OnPropertyChanged(nameof(HexText));
        OnPropertyChanged(nameof(SwatchBrush));
    }

    private void SetChannel(ColorChannel channel, double value)
    {
        var clamped = ThemeColor.ClampUnit(value);
        var color = getColor();
        var current = channel switch
        {
            ColorChannel.Red => color.Red,
            ColorChannel.Green => color.Green,
            ColorChannel.Blue => color.Blue,
            _ => 0
        };

        if (Math.Abs(current - clamped) < 0.0001)
        {
            return;
        }

        switch (channel)
        {
            case ColorChannel.Red:
                color.Red = clamped;
                break;
            case ColorChannel.Green:
                color.Green = clamped;
                break;
            case ColorChannel.Blue:
                color.Blue = clamped;
                break;
        }

        commitChange();
    }

    private static string ByteText(double value) => Byte(value).ToString();

    private static byte Byte(double value) => (byte)Math.Round(ThemeColor.ClampUnit(value) * 255);

    private enum ColorChannel
    {
        Red,
        Green,
        Blue
    }
}
