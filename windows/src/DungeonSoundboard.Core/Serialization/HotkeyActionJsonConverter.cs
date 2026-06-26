using System.Text.Json;
using System.Text.Json.Serialization;
using DungeonSoundboard.Core.Models;

namespace DungeonSoundboard.Core.Serialization;

public sealed class HotkeyActionJsonConverter : JsonConverter<HotkeyAction>
{
    public override HotkeyAction Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("Hotkey action must be an object.");
        }

        var property = root.EnumerateObject().FirstOrDefault();
        if (property.Name is null)
        {
            throw new JsonException("Hotkey action object cannot be empty.");
        }

        return property.Name switch
        {
            "stopAll" => HotkeyAction.StopAll,
            "stopEffects" => HotkeyAction.StopEffects,
            "playPause" => HotkeyAction.PlayPause,
            "musicVolumeUp" => HotkeyAction.MusicVolumeUp,
            "musicVolumeDown" => HotkeyAction.MusicVolumeDown,
            "effectsVolumeUp" => HotkeyAction.EffectsVolumeUp,
            "effectsVolumeDown" => HotkeyAction.EffectsVolumeDown,
            "playMusicTrack" => ReadTrackAction(property.Value, HotkeyAction.PlayMusicTrack),
            "playEffect" => ReadTrackAction(property.Value, HotkeyAction.PlayEffect),
            _ => throw new JsonException($"Unknown hotkey action '{property.Name}'.")
        };
    }

    public override void Write(Utf8JsonWriter writer, HotkeyAction value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WritePropertyName(CaseName(value.Kind));
        writer.WriteStartObject();
        if (value.Kind is HotkeyActionKind.PlayMusicTrack or HotkeyActionKind.PlayEffect)
        {
            writer.WriteString("playlistID", value.PlaylistId?.ToString("D"));
            writer.WriteString("trackID", value.TrackId?.ToString("D"));
        }

        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private static HotkeyAction ReadTrackAction(JsonElement element, Func<Guid, Guid, HotkeyAction> factory)
    {
        var playlistId = ReadGuid(element, "playlistID");
        var trackId = ReadGuid(element, "trackID");
        return factory(playlistId, trackId);
    }

    private static Guid ReadGuid(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var property)
            || property.ValueKind != JsonValueKind.String
            || !Guid.TryParse(property.GetString(), out var id))
        {
            throw new JsonException($"Hotkey action is missing '{name}'.");
        }

        return id;
    }

    private static string CaseName(HotkeyActionKind kind)
    {
        return kind switch
        {
            HotkeyActionKind.StopAll => "stopAll",
            HotkeyActionKind.StopEffects => "stopEffects",
            HotkeyActionKind.PlayPause => "playPause",
            HotkeyActionKind.MusicVolumeUp => "musicVolumeUp",
            HotkeyActionKind.MusicVolumeDown => "musicVolumeDown",
            HotkeyActionKind.EffectsVolumeUp => "effectsVolumeUp",
            HotkeyActionKind.EffectsVolumeDown => "effectsVolumeDown",
            HotkeyActionKind.PlayMusicTrack => "playMusicTrack",
            HotkeyActionKind.PlayEffect => "playEffect",
            _ => throw new JsonException($"Unknown hotkey action kind '{kind}'.")
        };
    }
}
