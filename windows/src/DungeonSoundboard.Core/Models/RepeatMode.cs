using System.Text.Json.Serialization;

namespace DungeonSoundboard.Core.Models;

[JsonConverter(typeof(JsonStringEnumConverter<RepeatMode>))]
public enum RepeatMode
{
    [JsonStringEnumMemberName("off")]
    Off,

    [JsonStringEnumMemberName("one")]
    One,

    [JsonStringEnumMemberName("all")]
    All
}

public static class RepeatModeExtensions
{
    public static string LocalizedKey(this RepeatMode mode)
    {
        return mode switch
        {
            RepeatMode.Off => "repeat.off",
            RepeatMode.One => "repeat.one",
            RepeatMode.All => "repeat.all",
            _ => "repeat.off"
        };
    }

    public static RepeatMode? FromStoredValue(string value)
    {
        return value switch
        {
            "off" => RepeatMode.Off,
            "one" => RepeatMode.One,
            "all" => RepeatMode.All,
            "Без повтора" => RepeatMode.Off,
            "Повтор трека" => RepeatMode.One,
            "Повтор плейлиста" => RepeatMode.All,
            _ => null
        };
    }
}
