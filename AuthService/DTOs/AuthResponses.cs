using System.Text.Json.Serialization;

namespace AuthService.DTOs;

public class TokenVerifyResponse
{
    [JsonPropertyName("valid")]
    public bool Valid { get; set; }

    [JsonPropertyName("user_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? UserId { get; set; }

    [JsonPropertyName("login_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? LoginId { get; set; }

    [JsonPropertyName("role")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Role { get; set; }
}

public class LogoutResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}
