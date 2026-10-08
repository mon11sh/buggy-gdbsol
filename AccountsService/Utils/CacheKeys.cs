namespace AccountsService.Utils;

/// <summary>Cache key formats shared by every class that reads or evicts the same entries.</summary>
public static class CacheKeys
{
    public static string Account(int accountNumber) => $"Account_{accountNumber}";
}
