using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace TransactionsService.DTOs;

public record TransferLimitCreate
{
    [JsonPropertyName("privilege")]
    [Required]
    [RegularExpression("^(SILVER|GOLD|PREMIUM)$", ErrorMessage = "privilege must be SILVER, GOLD or PREMIUM")]
    public string Privilege { get; init; } = "";

    [JsonPropertyName("daily_limit")]
    [Range(1, 999999999.99)]
    public decimal DailyLimit { get; init; }

    [JsonPropertyName("per_transaction_limit")]
    [Range(1, 999999999.99)]
    public decimal PerTransactionLimit { get; init; }
}

public record TransferLimitUpdate : IValidatableObject
{
    [JsonPropertyName("daily_limit")]
    [Range(1, 999999999.99)]
    public decimal DailyLimit { get; init; }

    [JsonPropertyName("transaction_limit")]
    [Range(1, int.MaxValue)]
    public int TransactionLimit { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (TransactionLimit > DailyLimit)
            yield return new ValidationResult("transaction_limit cannot exceed daily_limit", new[] { nameof(TransactionLimit) });
    }
}
