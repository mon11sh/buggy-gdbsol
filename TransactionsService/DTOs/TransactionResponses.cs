using System.Text.Json.Serialization;

namespace TransactionsService.DTOs;

public class TransactionResultResponse
{
    [JsonPropertyName("transaction_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? TransactionId { get; set; }

    [JsonPropertyName("account_number")]
    public int AccountNumber { get; set; }

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("new_balance")]
    public decimal NewBalance { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
}

public class TransferResultResponse
{
    [JsonPropertyName("transaction_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? TransactionId { get; set; }

    [JsonPropertyName("from_account")]
    public int FromAccount { get; set; }

    [JsonPropertyName("to_account")]
    public int ToAccount { get; set; }

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
}

public class TransactionDetailsResponse
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
    
    [JsonPropertyName("account_number")]
    public int AccountNumber { get; set; }
    
    [JsonPropertyName("transaction_type")]
    public string TransactionType { get; set; } = string.Empty;
    
    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }
    
    [JsonPropertyName("balance_after")]
    public decimal BalanceAfter { get; set; }
    
    [JsonPropertyName("description")]
    public string? Description { get; set; }
    
    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = string.Empty;
}

public class PagedTransactionResponse
{
    [JsonPropertyName("account_number")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? AccountNumber { get; set; }

    [JsonPropertyName("logs")]
    public List<TransactionLogItem> Logs { get; set; } = new();
    
    [JsonPropertyName("skip")]
    public int Skip { get; set; }
    
    [JsonPropertyName("limit")]
    public int Limit { get; set; }
    
    [JsonPropertyName("has_more")]
    public bool HasMore { get; set; }

    [JsonPropertyName("total_count")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? TotalCount { get; set; }

    [JsonPropertyName("total")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Total { get; set; }
}

public class TransactionLogItem
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("transaction_id")]
    public int TransactionId { get; set; }

    // Nullable: for a TRANSFER the endpoints live in from_account/to_account (account_number is null);
    // for a DEPOSIT/WITHDRAWAL account_number is the affected account and from/to are null.
    [JsonPropertyName("account_number")]
    public int? AccountNumber { get; set; }

    [JsonPropertyName("from_account")]
    public int? FromAccount { get; set; }

    [JsonPropertyName("to_account")]
    public int? ToAccount { get; set; }

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("transaction_type")]
    public string TransactionType { get; set; } = string.Empty;

    [JsonPropertyName("mode")]
    public string? Mode { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "SUCCESS";

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; }
}
