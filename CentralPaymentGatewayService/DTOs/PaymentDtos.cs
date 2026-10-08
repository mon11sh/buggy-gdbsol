using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace CentralPaymentGatewayService.DTOs;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PaymentMode
{
    NEFT,
    RTGS,
    IMPS,
    UPI,
    CHEQUE
}

public record PaymentRequest
{
    [JsonPropertyName("source_account_id")]
    [Range(1, int.MaxValue, ErrorMessage = "ID of the source account must be positive")]
    public int SourceAccountId { get; init; }

    [JsonPropertyName("destination_account_id")]
    [Range(1, int.MaxValue, ErrorMessage = "ID of the destination account must be positive")]
    public int DestinationAccountId { get; init; }

    [JsonPropertyName("amount")]
    [Range(0.01, 10_000_000, ErrorMessage = "Amount must be > 0 and <= 10,000,000")]
    public decimal Amount { get; init; }

    [JsonPropertyName("mode")]
    [Required]
    public PaymentMode Mode { get; init; }

    [JsonPropertyName("reference_id")]
    public string? ReferenceId { get; init; }
}

public record PaymentResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("transaction_id")]
    public string TransactionId { get; init; } = null!;

    [JsonPropertyName("message")]
    public string Message { get; init; } = null!;

    [JsonPropertyName("gateway_ref_id")]
    public string GatewayRefId { get; init; } = null!;
}

public record ValidationRequest
{
    [JsonPropertyName("from_account")]
    [Range(1, int.MaxValue, ErrorMessage = "Source account number must be positive")]
    public int FromAccount { get; init; }

    [JsonPropertyName("to_account")]
    [Range(1, int.MaxValue, ErrorMessage = "Destination account number must be positive")]
    public int ToAccount { get; init; }

    [JsonPropertyName("amount")]
    [Range(0.01, 10_000_000, ErrorMessage = "Amount must be > 0 and <= 10,000,000")]
    public decimal Amount { get; init; }

    [JsonPropertyName("transaction_id")]
    public string? TransactionId { get; init; }
}

public record ValidationResponse
{
    [JsonPropertyName("valid")]
    public bool Valid { get; init; }

    [JsonPropertyName("message")]
    public string Message { get; init; } = null!;

    [JsonPropertyName("transaction_id")]
    public string? TransactionId { get; init; }
}
