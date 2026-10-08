using System.Text.Json.Serialization;

namespace AccountsService.DTOs;

public class PinVerifyResponse
{
    [JsonPropertyName("valid")]
    public bool Valid { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}
