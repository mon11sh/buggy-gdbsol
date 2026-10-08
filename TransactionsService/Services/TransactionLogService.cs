using TransactionsService.Infrastructure.Repositories;
using TransactionsService.DTOs;
using TransactionsService.Domain.Models;
using TransactionsService.Mapping;
using Microsoft.Extensions.Caching.Memory;
using TransactionsService.Utils;

namespace TransactionsService.Services;

public class TransactionLogService : ITransactionLogService
{
    private readonly ITransactionLogRepository _repository;
    private readonly ITransactionRepository _transfers;
    private readonly IMemoryCache _cache;
    private readonly CacheInvalidator _invalidator;

    public TransactionLogService(
        ITransactionLogRepository repository,
        ITransactionRepository transfers,
        IMemoryCache cache,
        CacheInvalidator invalidator)
    {
        _repository = repository;
        _transfers = transfers;
        _cache = cache;
        _invalidator = invalidator;
    }

    public async Task<PagedTransactionResponse> GetTransactionLogsAsync(int accountId, int skip, int limit, DateTime? startDate, DateTime? endDate, string? type, CancellationToken ct = default)
    {
        var cacheKey = $"TxHistory_{accountId}_v{_invalidator.VersionOf(accountId)}_{skip}_{limit}_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}_{type}";

        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60);

            var logs = await _repository.GetLogsAsync(accountId, skip, limit, startDate, endDate, type, ct);
            var formattedLogs = logs.Select(TransactionResponseMapper.ToLogItem).ToList();

            return new PagedTransactionResponse
            {
                AccountNumber = accountId,
                Logs = formattedLogs,
                Skip = skip,
                Limit = limit,
                HasMore = logs.Count == limit,
                TotalCount = logs.Count
            };
        }) ?? throw new InvalidOperationException("Failed to generate transaction logs");
    }

    public async Task<PagedTransactionResponse> GetAllTransactionsAsync(int skip, int limit, string? type, DateTime? startDate, DateTime? endDate, CancellationToken ct = default)
    {
        // The global feed is built from TWO sources so each item carries the fields the UI needs:
        //   - fund_transfers  -> one TRANSFER item per transfer, with from_account + to_account set
        //   - transaction_logging -> DEPOSIT/WITHDRAWAL items (account_number set; from/to null)
        // The two TRANSFER log legs are intentionally excluded (the fund_transfers row represents
        // the transfer), so a transfer shows once as "From X -> To Y" instead of "To #undefined".
        bool isTransferFilter = !string.IsNullOrEmpty(type) && type.Equals("TRANSFER", StringComparison.OrdinalIgnoreCase);
        bool wantTransfers = string.IsNullOrEmpty(type) || isTransferFilter;
        bool wantLogs = string.IsNullOrEmpty(type) || !isTransferFilter;

        // Fetch enough from each source to satisfy the requested page after the merge/sort.
        int window = skip + limit;
        var items = new List<TransactionLogItem>();

        if (wantLogs)
        {
            var logs = await _repository.GetAllLogsAsync(0, window, type, startDate, endDate, ct);
            foreach (var log in logs)
            {
                if (log.TransactionType == TransactionType.TRANSFER) continue; // surfaced via fund_transfers
                items.Add(TransactionResponseMapper.ToLogItem(log)); // from_account/to_account stay null
            }
        }

        if (wantTransfers)
        {
            var transfers = await _repository.GetAllTransfersAsync(0, window, startDate, endDate, ct);
            foreach (var t in transfers)
            {
                items.Add(new TransactionLogItem
                {
                    Id = t.Id,
                    TransactionId = t.Id,
                    AccountNumber = null,
                    FromAccount = t.SourceAccountId,
                    ToAccount = t.DestinationAccountId,
                    Amount = t.Amount,
                    TransactionType = "TRANSFER",
                    Mode = t.TransferMode.ToString(),
                    // Completed transfers read as SUCCESS to the UI (matches the reference); a failed
                    // transfer keeps its real status.
                    Status = string.Equals(t.Status, "COMPLETED", StringComparison.OrdinalIgnoreCase) ? "SUCCESS" : t.Status,
                    CreatedAt = t.CreatedAt,
                    UpdatedAt = t.CreatedAt
                });
            }
        }

        var page = items.OrderByDescending(i => i.CreatedAt).Skip(skip).Take(limit).ToList();

        return new PagedTransactionResponse
        {
            Logs = page,
            Skip = skip,
            Limit = limit,
            Total = items.Count,
            HasMore = items.Count > skip + limit
        };
    }

    public async Task<TransferDetailsResponse?> GetTransferDetailsAsync(int transferId, CancellationToken ct = default)
    {
        var transfer = await _transfers.GetTransferWithLegsAsync(transferId, ct);
        if (transfer is null) return null;

        return new TransferDetailsResponse
        {
            TransactionId = transfer.Id,
            FromAccount = transfer.SourceAccountId,
            ToAccount = transfer.DestinationAccountId,
            Amount = transfer.Amount,
            Mode = transfer.TransferMode.ToString(),
            Status = transfer.Status,
            FailureReason = transfer.FailureReason,
            CreatedAt = transfer.CreatedAt,
            Legs = transfer.Legs.Select(TransactionResponseMapper.ToLogItem).ToList(),
        };
    }

    public async Task<TransactionDetailsResponse> GetTransactionDetailsAsync(int transactionId, CancellationToken ct = default)
    {
        var log = await _repository.GetLogByIdAsync(transactionId, ct);
        // Preserves the previous contract (AutoMapper mapped a missing row to null; the controller turns that into 404).
        return log is null ? null! : TransactionResponseMapper.ToDetails(log);
    }
}
