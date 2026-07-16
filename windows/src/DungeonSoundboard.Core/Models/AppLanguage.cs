using System.Text.Json.Serialization;

namespace DungeonSoundboard.Core.Models;

[JsonConverter(typeof(JsonStringEnumConverter<AppLanguage>))]
public enum AppLanguage
{
    [JsonStringEnumMemberName("english")]
    English,

    [JsonStringEnumMemberName("russian")]
    Russian
}
