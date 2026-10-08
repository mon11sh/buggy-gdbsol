using System.Text.Json.Serialization;

namespace AccountsService.DTOs;

public class AccountSummaryResponse
{
    [JsonPropertyName("total_accounts")]
    public int TotalAccounts { get; set; }

    [JsonPropertyName("total_balance")]
    public decimal TotalBalance { get; set; }

    [JsonPropertyName("active_accounts")]
    public int ActiveAccounts { get; set; }

    [JsonPropertyName("by_type")]
    public Dictionary<string, int> ByType { get; set; } = new();

    [JsonPropertyName("by_privilege")]
    public Dictionary<string, int> ByPrivilege { get; set; } = new();

    [JsonPropertyName("recent_accounts")]
    public List<RecentAccountDto> RecentAccounts { get; set; } = new();
}

public class RecentAccountDto
{
    [JsonPropertyName("AccountNumber")]
    public int AccountNumber { get; set; }

    [JsonPropertyName("Name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("AccountType")]
    public string AccountType { get; set; } = string.Empty;
}
