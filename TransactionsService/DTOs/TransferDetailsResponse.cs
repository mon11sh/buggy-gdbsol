using System.Text.Json.Serialization;

namespace TransactionsService.DTOs;

/// <summary>A fund transfer with the ledger legs it produced (one debit on the source, one credit on the destination).</summary>
public class TransferDetailsResponse
{
    [JsonPropertyName("transaction_id")]
    public int TransactionId { get; set; }

    [JsonPropertyName("from_account")]
    public int FromAccount { get; set; }

    [JsonPropertyName("to_account")]
    public int ToAccount { get; set; }

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("mode")]
    public string Mode { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("failure_reason")]
    public string? FailureReason { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("legs")]
    public List<TransactionLogItem> Legs { get; set; } = new();
}
