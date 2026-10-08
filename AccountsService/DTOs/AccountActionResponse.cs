using System.Text.Json.Serialization;

namespace AccountsService.DTOs;

public class AccountActionResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("account_number")]
    public int AccountNumber { get; set; }
}
