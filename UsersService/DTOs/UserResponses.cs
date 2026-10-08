using System.Text.Json.Serialization;

namespace UsersService.DTOs;

public record UserResponse
{
    [JsonPropertyName("user_id")]
    public int UserId { get; init; }

    [JsonPropertyName("username")]
    public string Username { get; init; } = null!;

    [JsonPropertyName("login_id")]
    public string LoginId { get; init; } = null!;

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; init; }

    [JsonPropertyName("is_active")]
    public bool IsActive { get; init; }

    [JsonPropertyName("role")]
    public string Role { get; init; } = "MANAGER";
}

public record AddUserResponse : UserResponse
{
    [JsonPropertyName("message")]
    public string Message { get; init; } = "User created successfully";
}

public record EditUserResponse : UserResponse
{
    [JsonPropertyName("message")]
    public string Message { get; init; } = "User updated successfully";
}

public record ViewUserResponse : UserResponse
{
}

public record ListUsersResponse
{
    [JsonPropertyName("users")]
    public List<UserResponse> Users { get; init; } = new();

    [JsonPropertyName("total_count")]
    public int TotalCount { get; init; }
}

public record InactivateUserResponse : UserResponse
{
    [JsonPropertyName("message")]
    public string Message { get; init; } = "User inactivated successfully";
}
