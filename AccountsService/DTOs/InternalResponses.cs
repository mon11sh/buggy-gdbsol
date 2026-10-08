using System.Text.Json.Serialization;

namespace AccountsService.DTOs;

public class InternalAccountDetailsResponse
{
    [JsonPropertyName("account_number")]
    public int AccountNumber { get; set; }

    [JsonPropertyName("account_type")]
    public string AccountType { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("balance")]
    public decimal Balance { get; set; }

    [JsonPropertyName("privilege")]
    public string Privilege { get; set; } = string.Empty;

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; }

    [JsonPropertyName("activated_date")]
    public string ActivatedDate { get; set; } = string.Empty;

    [JsonPropertyName("closed_date")]
    public string? ClosedDate { get; set; }
}

public class InternalPrivilegeResponse
{
    [JsonPropertyName("account_number")]
    public int AccountNumber { get; set; }

    [JsonPropertyName("privilege")]
    public string Privilege { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("error_code")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ErrorCode { get; set; }
}

public class InternalActiveResponse
{
    [JsonPropertyName("account_number")]
    public int AccountNumber { get; set; }

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("error_code")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ErrorCode { get; set; }
}

public class InternalTransactionResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("account_number")]
    public int AccountNumber { get; set; }

    [JsonPropertyName("amount_debited")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? AmountDebited { get; set; }

    [JsonPropertyName("amount_credited")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? AmountCredited { get; set; }

    [JsonPropertyName("new_balance")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? NewBalance { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("error_code")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ErrorCode { get; set; }

    [JsonPropertyName("error_message")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ErrorMessage { get; set; }
}

public class InternalPinVerifyResponse
{
    [JsonPropertyName("account_number")]
    public int AccountNumber { get; set; }

    [JsonPropertyName("pin_valid")]
    public bool PinValid { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("error_code")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ErrorCode { get; set; }
}
