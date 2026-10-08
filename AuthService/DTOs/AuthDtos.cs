using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace AuthService.DTOs;

public record LoginRequest
{
    [JsonPropertyName("login_id")]
    [Required]
    public string LoginId { get; init; } = null!;

    [JsonPropertyName("password")]
    [Required]
    public string Password { get; init; } = null!;
}

public record TokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; init; } = null!;

    [JsonPropertyName("token_type")]
    public string TokenType { get; init; } = null!;

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; init; }

    [JsonPropertyName("user_id")]
    public int UserId { get; init; }

    [JsonPropertyName("login_id")]
    public string LoginId { get; init; } = null!;

    [JsonPropertyName("role")]
    public string Role { get; init; } = null!;
}

/// <summary>Body form of a refresh for non-browser clients; browsers rely on the httpOnly cookie instead.</summary>
public record RefreshRequest
{
    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; init; }
}
