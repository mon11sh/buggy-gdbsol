using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace AadharService.DTOs;

public record AadharVerificationRequest
{
    [JsonPropertyName("aadhar_number")]
    [Required]
    [StringLength(12, MinimumLength = 12, ErrorMessage = "Aadhar number must be exactly 12 digits")]
    [RegularExpression("^[0-9]+$", ErrorMessage = "Aadhar number must contain only digits")]
    public string AadharNumber { get; init; } = null!;
}

public record AadharVerificationResponse
{
    [JsonPropertyName("aadhar_number")]
    public string AadharNumber { get; init; } = null!;

    [JsonPropertyName("is_valid")]
    public bool IsValid { get; init; }

    [JsonPropertyName("status")]
    public string Status { get; init; } = null!;

    [JsonPropertyName("message")]
    public string Message { get; init; } = null!;

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; init; }

    [JsonPropertyName("name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; init; }

    [JsonPropertyName("mobile_no")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MobileNo { get; init; }

    [JsonPropertyName("address")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Address { get; init; }

    [JsonPropertyName("gender")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Gender { get; init; }

    [JsonPropertyName("date_of_birth")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DateOfBirth { get; init; } // Format: YYYY-MM-DD

    [JsonPropertyName("photo_url")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PhotoUrl { get; init; }
}
