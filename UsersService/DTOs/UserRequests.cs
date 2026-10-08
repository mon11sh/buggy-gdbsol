using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace UsersService.DTOs;

public record AddUserRequest
{
    [JsonPropertyName("username")]
    [Required]
    [StringLength(255, MinimumLength = 1)]
    public string Username { get; init; } = null!;

    [JsonPropertyName("login_id")]
    [Required]
    [StringLength(50, MinimumLength = 3)]
    [RegularExpression(@"^[a-zA-Z0-9._-]+$")]
    public string LoginId { get; init; } = null!;

    [JsonPropertyName("password")]
    [Required]
    [MinLength(8)]
    public string Password { get; init; } = null!;

    [JsonPropertyName("role")]
    public string? Role { get; init; }
}

public record EditUserRequest
{
    [JsonPropertyName("username")]
    [StringLength(255, MinimumLength = 1)]
    public string? Username { get; init; }

    [JsonPropertyName("password")]
    [MinLength(8)]
    public string? Password { get; init; }

    [JsonPropertyName("role")]
    public string? Role { get; init; }
}
