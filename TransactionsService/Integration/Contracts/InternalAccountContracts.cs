using System.Text.Json.Serialization;

namespace TransactionsService.Integration.Contracts;

// Typed contracts for the AccountsService internal API, replacing fragile
// JsonElement.GetProperty("...") access on the money path.

public sealed class InternalAccountDto
{
    [JsonPropertyName("is_active")] public bool IsActive { get; set; }
    [JsonPropertyName("balance")] public decimal Balance { get; set; }
    [JsonPropertyName("privilege")] public string? Privilege { get; set; }
}

public sealed class InternalBalanceResult
{
    [JsonPropertyName("new_balance")] public decimal NewBalance { get; set; }
}
