using DungeonSoundboard.Core.Models;

namespace DungeonSoundboard.App.ViewModels;

public sealed record HotkeyDisplayRow(HotkeyAction Action, string Name, string HotkeyText);
