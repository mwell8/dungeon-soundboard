using System.Text.Json.Serialization;

namespace DungeonSoundboard.Core.Models;

[JsonConverter(typeof(JsonStringEnumConverter<HotkeyModifier>))]
public enum HotkeyModifier
{
    [JsonStringEnumMemberName("none")]
    None,

    [JsonStringEnumMemberName("shift")]
    Shift,

    [JsonStringEnumMemberName("control")]
    Control
}

public sealed record Hotkey(ushort KeyCode, string Label, HotkeyModifier Modifier)
{
    public string Id => $"{Modifier.ToString().ToLowerInvariant()}-{KeyCode}";

    public string DisplayText => $"{DisplayPrefix}{Label}";

    private string DisplayPrefix => Modifier switch
    {
        HotkeyModifier.Shift => "Shift+",
        HotkeyModifier.Control => "Ctrl+",
        _ => ""
    };

    public static Hotkey? Normalized(
        ushort keyCode,
        string? charactersIgnoringModifiers,
        string? characters,
        bool isShiftPressed,
        bool isControlPressed,
        bool isOptionPressed,
        bool isCommandPressed)
    {
        var normalizedKeyCode = NormalizeKeyCode(keyCode);
        if (isOptionPressed || isCommandPressed || (isShiftPressed && isControlPressed))
        {
            return null;
        }

        var modifier = isControlPressed ? HotkeyModifier.Control : isShiftPressed ? HotkeyModifier.Shift : HotkeyModifier.None;
        var label = LabelFor(normalizedKeyCode, charactersIgnoringModifiers, characters);
        return string.IsNullOrWhiteSpace(label) ? null : new Hotkey(normalizedKeyCode, label, modifier);
    }

    private static ushort NormalizeKeyCode(ushort keyCode) => keyCode == HotkeyConfiguration.ForwardDeleteKeyCode
        ? HotkeyConfiguration.DeleteKeyCode
        : keyCode;

    private static string? LabelFor(ushort keyCode, string? charactersIgnoringModifiers, string? characters)
    {
        return keyCode switch
        {
            HotkeyConfiguration.PlusKeyCode => "+",
            HotkeyConfiguration.MinusKeyCode => "-",
            HotkeyConfiguration.ReturnKeyCode => "Return",
            HotkeyConfiguration.TabKeyCode => "Tab",
            HotkeyConfiguration.SpaceKeyCode => "Space",
            HotkeyConfiguration.DeleteKeyCode => "Delete",
            HotkeyConfiguration.EscapeKeyCode => "Esc",
            HotkeyConfiguration.LeftKeyCode => "Left",
            HotkeyConfiguration.RightKeyCode => "Right",
            HotkeyConfiguration.DownKeyCode => "Down",
            HotkeyConfiguration.UpKeyCode => "Up",
            _ => (charactersIgnoringModifiers ?? characters ?? "").ToUpperInvariant()
        };
    }
}

public enum HotkeyActionKind
{
    StopAll,
    StopEffects,
    PlayPause,
    MusicVolumeUp,
    MusicVolumeDown,
    EffectsVolumeUp,
    EffectsVolumeDown,
    PlayMusicTrack,
    PlayEffect
}

[JsonConverter(typeof(DungeonSoundboard.Core.Serialization.HotkeyActionJsonConverter))]
public sealed class HotkeyAction : IEquatable<HotkeyAction>
{
    public HotkeyActionKind Kind { get; }
    public Guid? PlaylistId { get; }
    public Guid? TrackId { get; }

    private HotkeyAction(HotkeyActionKind kind, Guid? playlistId = null, Guid? trackId = null)
    {
        Kind = kind;
        PlaylistId = playlistId;
        TrackId = trackId;
    }

    public static HotkeyAction StopAll { get; } = new(HotkeyActionKind.StopAll);
    public static HotkeyAction StopEffects { get; } = new(HotkeyActionKind.StopEffects);
    public static HotkeyAction PlayPause { get; } = new(HotkeyActionKind.PlayPause);
    public static HotkeyAction MusicVolumeUp { get; } = new(HotkeyActionKind.MusicVolumeUp);
    public static HotkeyAction MusicVolumeDown { get; } = new(HotkeyActionKind.MusicVolumeDown);
    public static HotkeyAction EffectsVolumeUp { get; } = new(HotkeyActionKind.EffectsVolumeUp);
    public static HotkeyAction EffectsVolumeDown { get; } = new(HotkeyActionKind.EffectsVolumeDown);

    public static IReadOnlyList<HotkeyAction> SystemActions { get; } =
    [
        StopEffects,
        StopAll,
        PlayPause,
        MusicVolumeUp,
        MusicVolumeDown,
        EffectsVolumeUp,
        EffectsVolumeDown
    ];

    public static HotkeyAction PlayMusicTrack(Guid playlistId, Guid trackId) =>
        new(HotkeyActionKind.PlayMusicTrack, playlistId, trackId);

    public static HotkeyAction PlayEffect(Guid playlistId, Guid trackId) =>
        new(HotkeyActionKind.PlayEffect, playlistId, trackId);

    public string Id => Kind switch
    {
        HotkeyActionKind.StopAll => "system.stopAll",
        HotkeyActionKind.StopEffects => "system.stopEffects",
        HotkeyActionKind.PlayPause => "system.playPause",
        HotkeyActionKind.MusicVolumeUp => "system.musicVolumeUp",
        HotkeyActionKind.MusicVolumeDown => "system.musicVolumeDown",
        HotkeyActionKind.EffectsVolumeUp => "system.effectsVolumeUp",
        HotkeyActionKind.EffectsVolumeDown => "system.effectsVolumeDown",
        HotkeyActionKind.PlayMusicTrack => $"music.{PlaylistId}.{TrackId}",
        HotkeyActionKind.PlayEffect => $"effect.{PlaylistId}.{TrackId}",
        _ => "system.unknown"
    };

    public bool IsSystemAction => Kind is not (HotkeyActionKind.PlayMusicTrack or HotkeyActionKind.PlayEffect);

    public bool Equals(HotkeyAction? other)
    {
        if (other is null)
        {
            return false;
        }

        return Kind == other.Kind && PlaylistId == other.PlaylistId && TrackId == other.TrackId;
    }

    public override bool Equals(object? obj) => Equals(obj as HotkeyAction);

    public override int GetHashCode() => HashCode.Combine(Kind, PlaylistId, TrackId);
}

public sealed record HotkeyBinding(HotkeyAction Action, Hotkey Hotkey)
{
    public string Id => Action.Id;
}

public sealed record HotkeyConflict(HotkeyAction ExistingAction, Hotkey Hotkey);

public sealed class HotkeyConfiguration : IEquatable<HotkeyConfiguration>
{
    public const ushort PlusKeyCode = 24;
    public const ushort MinusKeyCode = 27;
    public const ushort ReturnKeyCode = 36;
    public const ushort TabKeyCode = 48;
    public const ushort SpaceKeyCode = 49;
    public const ushort DeleteKeyCode = 51;
    public const ushort EscapeKeyCode = 53;
    public const ushort ForwardDeleteKeyCode = 117;
    public const ushort LeftKeyCode = 123;
    public const ushort RightKeyCode = 124;
    public const ushort DownKeyCode = 125;
    public const ushort UpKeyCode = 126;

    public static readonly Hotkey DeleteHotkey = new(DeleteKeyCode, "Delete", HotkeyModifier.None);

    public List<HotkeyBinding> Bindings { get; set; } = [];

    public HotkeyConfiguration()
    {
    }

    public HotkeyConfiguration(IEnumerable<HotkeyBinding> bindings)
    {
        Bindings = bindings.ToList();
    }

    public static HotkeyConfiguration Defaults => new(
    [
        new HotkeyBinding(HotkeyAction.StopEffects, DeleteHotkey),
        new HotkeyBinding(HotkeyAction.PlayPause, new Hotkey(SpaceKeyCode, "Space", HotkeyModifier.None)),
        new HotkeyBinding(HotkeyAction.MusicVolumeUp, new Hotkey(PlusKeyCode, "+", HotkeyModifier.None)),
        new HotkeyBinding(HotkeyAction.MusicVolumeDown, new Hotkey(MinusKeyCode, "-", HotkeyModifier.None)),
        new HotkeyBinding(HotkeyAction.EffectsVolumeUp, new Hotkey(PlusKeyCode, "+", HotkeyModifier.Shift)),
        new HotkeyBinding(HotkeyAction.EffectsVolumeDown, new Hotkey(MinusKeyCode, "-", HotkeyModifier.Shift))
    ]);

    public Hotkey? HotkeyFor(HotkeyAction action) => Bindings.FirstOrDefault(binding => binding.Action.Equals(action))?.Hotkey;

    public HotkeyAction? ActionFor(Hotkey hotkey) => Bindings.FirstOrDefault(binding => binding.Hotkey == hotkey)?.Action;

    public HotkeyConflict? ConflictFor(Hotkey hotkey, HotkeyAction excluding)
    {
        var binding = Bindings.FirstOrDefault(candidate => candidate.Hotkey == hotkey && !candidate.Action.Equals(excluding));
        return binding is null ? null : new HotkeyConflict(binding.Action, hotkey);
    }

    public HotkeyConflict? Assign(Hotkey hotkey, HotkeyAction action, bool resolvingConflicts)
    {
        var conflict = ConflictFor(hotkey, action);
        if (conflict is not null && !resolvingConflicts)
        {
            return conflict;
        }

        Bindings.RemoveAll(binding => binding.Action.Equals(action) || (resolvingConflicts && binding.Hotkey == hotkey));
        Bindings.Add(new HotkeyBinding(action, hotkey));
        return null;
    }

    public void Clear(HotkeyAction action)
    {
        Bindings.RemoveAll(binding => binding.Action.Equals(action));
    }

    public void MigrateLegacyDeleteStopAllDefault()
    {
        if (HotkeyFor(HotkeyAction.StopAll) != DeleteHotkey || HotkeyFor(HotkeyAction.StopEffects) is not null)
        {
            return;
        }

        Clear(HotkeyAction.StopAll);
        _ = Assign(DeleteHotkey, HotkeyAction.StopEffects, resolvingConflicts: true);
    }

    public void RemoveMissingTrackBindings(
        ISet<Guid> musicPlaylistIds,
        ISet<Guid> musicTrackIds,
        ISet<Guid> effectPlaylistIds,
        ISet<Guid> effectTrackIds)
    {
        Bindings.RemoveAll(binding =>
        {
            return binding.Action.Kind switch
            {
                HotkeyActionKind.PlayMusicTrack => !musicPlaylistIds.Contains(binding.Action.PlaylistId!.Value)
                    || !musicTrackIds.Contains(binding.Action.TrackId!.Value),
                HotkeyActionKind.PlayEffect => !effectPlaylistIds.Contains(binding.Action.PlaylistId!.Value)
                    || !effectTrackIds.Contains(binding.Action.TrackId!.Value),
                _ => false
            };
        });
    }

    public bool Equals(HotkeyConfiguration? other)
    {
        return other is not null && Bindings.SequenceEqual(other.Bindings);
    }

    public override bool Equals(object? obj) => Equals(obj as HotkeyConfiguration);

    public override int GetHashCode() => Bindings.Aggregate(0, (hash, binding) => HashCode.Combine(hash, binding));
}
