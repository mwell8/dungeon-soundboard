using Avalonia.Input;
using DungeonSoundboard.Core.Models;

namespace DungeonSoundboard.App.Services;

public static class HotkeyMapper
{
    private const ushort FunctionKeyBaseCode = 240;

    public static Hotkey? FromKeyEvent(KeyEventArgs e)
    {
        return FromKey(e.Key, e.KeyModifiers);
    }

    public static Hotkey? FromKey(Key key, KeyModifiers modifiers)
    {
        var keyCode = KeyCodeFor(key);
        if (keyCode is null)
        {
            return null;
        }

        return Hotkey.Normalized(
            keyCode.Value,
            CharactersFor(key),
            CharactersFor(key),
            modifiers.HasFlag(KeyModifiers.Shift),
            modifiers.HasFlag(KeyModifiers.Control),
            modifiers.HasFlag(KeyModifiers.Alt),
            modifiers.HasFlag(KeyModifiers.Meta));
    }

    private static ushort? KeyCodeFor(Key key)
    {
        var keyValue = (int)key;
        if (keyValue >= (int)Key.A && keyValue <= (int)Key.Z)
        {
            return (ushort)(keyValue - (int)Key.A);
        }

        if (keyValue >= (int)Key.D0 && keyValue <= (int)Key.D9)
        {
            return (ushort)(200 + keyValue - (int)Key.D0);
        }

        if (keyValue >= (int)Key.NumPad0 && keyValue <= (int)Key.NumPad9)
        {
            return (ushort)(220 + keyValue - (int)Key.NumPad0);
        }

        if (keyValue >= (int)Key.F1 && keyValue <= (int)Key.F12)
        {
            return (ushort)(FunctionKeyBaseCode + keyValue - (int)Key.F1);
        }

        return key switch
        {
            Key.Space => HotkeyConfiguration.SpaceKeyCode,
            Key.Delete => HotkeyConfiguration.DeleteKeyCode,
            Key.Back => HotkeyConfiguration.DeleteKeyCode,
            Key.Escape => HotkeyConfiguration.EscapeKeyCode,
            Key.Enter => HotkeyConfiguration.ReturnKeyCode,
            Key.Tab => HotkeyConfiguration.TabKeyCode,
            Key.Left => HotkeyConfiguration.LeftKeyCode,
            Key.Right => HotkeyConfiguration.RightKeyCode,
            Key.Down => HotkeyConfiguration.DownKeyCode,
            Key.Up => HotkeyConfiguration.UpKeyCode,
            Key.Add or Key.OemPlus => HotkeyConfiguration.PlusKeyCode,
            Key.Subtract or Key.OemMinus => HotkeyConfiguration.MinusKeyCode,
            _ => null
        };
    }

    private static string? CharactersFor(Key key)
    {
        var keyValue = (int)key;
        if (keyValue >= (int)Key.A && keyValue <= (int)Key.Z)
        {
            return key.ToString().ToUpperInvariant();
        }

        if (keyValue >= (int)Key.D0 && keyValue <= (int)Key.D9)
        {
            return (keyValue - (int)Key.D0).ToString();
        }

        if (keyValue >= (int)Key.NumPad0 && keyValue <= (int)Key.NumPad9)
        {
            return (keyValue - (int)Key.NumPad0).ToString();
        }

        if (keyValue >= (int)Key.F1 && keyValue <= (int)Key.F12)
        {
            return $"F{keyValue - (int)Key.F1 + 1}";
        }

        return key switch
        {
            Key.Space => "Space",
            Key.Delete or Key.Back => "Delete",
            Key.Escape => "Esc",
            Key.Enter => "Return",
            Key.Tab => "Tab",
            Key.Left => "Left",
            Key.Right => "Right",
            Key.Down => "Down",
            Key.Up => "Up",
            Key.Add or Key.OemPlus => "+",
            Key.Subtract or Key.OemMinus => "-",
            _ => null
        };
    }
}
