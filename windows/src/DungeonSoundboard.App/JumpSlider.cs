using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;

namespace DungeonSoundboard.App.Controls;

public sealed class JumpSlider : Slider
{
    private bool _isPointerAdjusting;

    public JumpSlider()
    {
        AddHandler(PointerPressedEvent, OnJumpPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerMovedEvent, OnJumpPointerMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerReleasedEvent, OnJumpPointerReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    internal static double ValueFromPosition(
        double minimum,
        double maximum,
        double position,
        double length,
        bool isDirectionReversed)
    {
        if (!double.IsFinite(minimum)
            || !double.IsFinite(maximum)
            || !double.IsFinite(position)
            || !double.IsFinite(length)
            || length <= 0)
        {
            return minimum;
        }

        var ratio = Math.Clamp(position / length, 0, 1);
        if (isDirectionReversed)
        {
            ratio = 1 - ratio;
        }

        return minimum + ((maximum - minimum) * ratio);
    }

    private void OnJumpPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _isPointerAdjusting = true;
        e.Pointer.Capture(this);
        SetValueFromPointer(e);
        e.Handled = true;
    }

    private void OnJumpPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isPointerAdjusting)
        {
            return;
        }

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            ReleasePointer(e.Pointer);
            return;
        }

        SetValueFromPointer(e);
        e.Handled = true;
    }

    private void OnJumpPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isPointerAdjusting)
        {
            return;
        }

        SetValueFromPointer(e);
        ReleasePointer(e.Pointer);
        e.Handled = true;
    }

    private void SetValueFromPointer(PointerEventArgs e)
    {
        var point = e.GetPosition(this);
        var isVertical = Orientation == Orientation.Vertical;
        var position = isVertical ? point.Y : point.X;
        var length = isVertical ? Bounds.Height : Bounds.Width;
        var reverse = isVertical ? !IsDirectionReversed : IsDirectionReversed;
        SetCurrentValue(ValueProperty, ValueFromPosition(Minimum, Maximum, position, length, reverse));
    }

    private void ReleasePointer(IPointer pointer)
    {
        _isPointerAdjusting = false;
        pointer.Capture(null);
    }
}
