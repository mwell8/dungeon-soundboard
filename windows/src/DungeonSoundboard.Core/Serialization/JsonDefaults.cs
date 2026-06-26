using System.Text.Json;
using System.Text.Json.Serialization;

namespace DungeonSoundboard.Core.Serialization;

public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        options.Converters.Add(new JsonStringEnumConverter());
        options.Converters.Add(new HotkeyActionJsonConverter());
        return options;
    }
}
