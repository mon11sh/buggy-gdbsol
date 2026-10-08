using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace CompanyCrvService.DTOs;

public record CompanyVerificationRequest
{
    private string _registrationNumber = string.Empty;

    [JsonPropertyName("registration_number")]
    [Required]
    [StringLength(21, MinimumLength = 21, ErrorMessage = "Registration number must be exactly 21 characters")]
    [RegularExpression("^[a-zA-Z0-9]+$", ErrorMessage = "Registration number must be alphanumeric")]
    public string RegistrationNumber 
    { 
        get => _registrationNumber; 
        init => _registrationNumber = value?.ToUpper() ?? string.Empty; 
    }
}

public record CompanyVerificationResponse
{
    [JsonPropertyName("registration_number")]
    public string RegistrationNumber { get; init; } = null!;

    [JsonPropertyName("is_valid")]
    public bool IsValid { get; init; }

    [JsonPropertyName("status")]
    public string Status { get; init; } = null!;

    [JsonPropertyName("message")]
    public string Message { get; init; } = null!;

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; init; }

    [JsonPropertyName("company_name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CompanyName { get; init; }

    [JsonPropertyName("type")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Type { get; init; }

    [JsonPropertyName("address")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Address { get; init; }

    [JsonPropertyName("email")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Email { get; init; }

    [JsonPropertyName("phone")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Phone { get; init; }

    [JsonPropertyName("website")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Website { get; init; }

    [JsonPropertyName("incorporation_date")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? IncorporationDate { get; init; }

    [JsonPropertyName("paid_up_capital")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PaidUpCapital { get; init; }

    [JsonPropertyName("directors")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Directors { get; init; }
}
