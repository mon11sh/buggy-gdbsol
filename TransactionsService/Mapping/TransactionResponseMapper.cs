using TransactionsService.DTOs;
using TransactionsService.Infrastructure.Data;

namespace TransactionsService.Mapping;

/// <summary>
/// Explicit, compile-checked entity → DTO mapping for the Transactions service (replaces the
/// AutoMapper profile: a renamed property is now a build error, not a silently-null JSON field).
/// </summary>
public static class TransactionResponseMapper
{
    private const string TimestampFormat = "yyyy-MM-ddTHH:mm:ss";

    /// <summary>
    /// Ledger row for a DEPOSIT/WITHDRAWAL: <c>account_number</c> is the affected account; the
    /// transfer endpoints (<c>from_account</c>/<c>to_account</c>) stay null. Callers building a
    /// combined history overwrite those for TRANSFER rows.
    /// </summary>
    public static TransactionLogItem ToLogItem(TransactionLogEntity log) => new()
    {
        Id = log.Id,
        TransactionId = log.Id,
        AccountNumber = log.AccountId,
        Amount = log.Amount,
        TransactionType = log.TransactionType.ToString(),
        Description = log.Description,
        CreatedAt = log.CreatedAt,
        UpdatedAt = log.CreatedAt,
    };

    public static TransactionDetailsResponse ToDetails(TransactionLogEntity log) => new()
    {
        Id = log.Id,
        AccountNumber = log.AccountId,
        TransactionType = log.TransactionType.ToString(),
        Amount = log.Amount,
        BalanceAfter = log.BalanceAfter,
        Description = log.Description,
        Timestamp = log.CreatedAt.ToString(TimestampFormat),
    };
}
