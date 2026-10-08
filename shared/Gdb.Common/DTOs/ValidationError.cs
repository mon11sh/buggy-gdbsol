using System.Text.Json.Serialization;

namespace Gdb.Common.DTOs;

public class ValidationError
{
    [JsonPropertyName("loc")]
    public object[] Loc { get; set; } = Array.Empty<object>();

    [JsonPropertyName("msg")]
    public string Msg { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
}
