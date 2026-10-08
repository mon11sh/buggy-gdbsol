using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace TransactionsService.DTOs;

/// <summary>Request contracts for the money endpoints. Every field that reaches the ledger is validated here.</summary>
public record DepositRequest
{
    [JsonPropertyName("account_number")]
    [Range(1, int.MaxValue, ErrorMessage = "account_number is required")]
    public int AccountNumber { get; init; }

    [JsonPropertyName("amount")]
    [Range(0.01, 999999999.99)]
    public decimal Amount { get; init; }
}

public record WithdrawRequest
{
    [JsonPropertyName("account_number")]
    [Range(1, int.MaxValue, ErrorMessage = "account_number is required")]
    public int AccountNumber { get; init; }

    [JsonPropertyName("amount")]
    [Range(0.01, 999999999.99)]
    public decimal Amount { get; init; }

    [JsonPropertyName("pin")]
    [Required]
    [RegularExpression(@"^\d{4,6}$", ErrorMessage = "pin must be 4-6 digits")]
    public string Pin { get; init; } = "";
}

public record TransferRequest : IValidatableObject
{
    [JsonPropertyName("from_account")]
    [Range(1, int.MaxValue, ErrorMessage = "from_account is required")]
    public int FromAccount { get; init; }

    [JsonPropertyName("to_account")]
    [Range(1, int.MaxValue, ErrorMessage = "to_account is required")]
    public int ToAccount { get; init; }

    [JsonPropertyName("amount")]
    [Range(0.01, 999999999.99)]
    public decimal Amount { get; init; }

    [JsonPropertyName("pin")]
    [Required]
    [RegularExpression(@"^\d{4,6}$", ErrorMessage = "pin must be 4-6 digits")]
    public string Pin { get; init; } = "";

    [JsonPropertyName("transfer_mode")]
    [RegularExpression("^(NEFT|RTGS|IMPS|UPI)$", ErrorMessage = "transfer_mode must be NEFT, RTGS, IMPS or UPI")]
    public string? TransferMode { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (FromAccount == ToAccount)
            yield return new ValidationResult("from_account and to_account must differ", new[] { nameof(ToAccount) });
    }
}
