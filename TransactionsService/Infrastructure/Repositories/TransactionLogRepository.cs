using Microsoft.EntityFrameworkCore;
using TransactionsService.Config;
using TransactionsService.Domain.Models;
using TransactionsService.Infrastructure.Data;
using TransactionsService.Infrastructure.Audit;
using System.Text.Json;

namespace TransactionsService.Infrastructure.Repositories;

public interface ITransactionLogRepository
{
    Task<TransactionLogEntity> LogTransactionAsync(int accountId, TransactionType type, decimal amount, decimal balanceAfter, string? referenceId, string? description, CancellationToken ct = default, int? transferId = null);
    /// <summary>Inserts many ledger rows in one round trip (seeding, imports). EF: batched INSERT; ADO.NET: SqlBulkCopy.</summary>
    Task BulkInsertAsync(IReadOnlyList<TransactionLogEntity> logs, CancellationToken ct = default);
    Task<List<TransactionLogEntity>> GetLogsAsync(int accountId, int skip = 0, int limit = 50, DateTime? startDate = null, DateTime? endDate = null, string? type = null, CancellationToken ct = default);
    Task<List<TransactionLogEntity>> GetAllLogsAsync(int skip = 0, int limit = 50, string? type = null, DateTime? startDate = null, DateTime? endDate = null, CancellationToken ct = default);
    Task<List<FundTransferEntity>> GetAllTransfersAsync(int skip = 0, int limit = 50, DateTime? startDate = null, DateTime? endDate = null, CancellationToken ct = default);
    Task<TransactionLogEntity?> GetLogByIdAsync(int transactionId, CancellationToken ct = default);
}

public class TransactionLogRepository : ITransactionLogRepository
{
    private readonly AppDbContext _context;
    private readonly TransactionAuditFileWriter _audit;

    public TransactionLogRepository(AppDbContext context, TransactionAuditFileWriter audit)
    {
        _context = context;
        _audit = audit;
    }

    public async Task<TransactionLogEntity> LogTransactionAsync(int accountId, TransactionType type, decimal amount, decimal balanceAfter, string? referenceId, string? description, CancellationToken ct = default, int? transferId = null)
    {
        // CONCEPT: TPH - the factory returns the subtype for this transaction type; EF writes the matching discriminator.
        var log = TransactionLogEntity.Create(type);
        log.AccountId = accountId;
        log.Amount = amount;
        log.BalanceAfter = balanceAfter;
        log.ReferenceId = referenceId;
        log.Description = description;
        if (log is TransferLegEntity leg) leg.TransferId = transferId;

        _context.TransactionLogs.Add(log);
        await _context.SaveChangesAsync(ct);
        _audit.Enqueue(log);
        return log;
    }

    public async Task BulkInsertAsync(IReadOnlyList<TransactionLogEntity> logs, CancellationToken ct = default)
    {
        if (logs.Count == 0) return;

        // CONCEPT: AutoDetectChanges - DetectChanges scans every tracked entity on Add/SaveChanges; for a
        // large batch that is quadratic work. Switch it off, add everything, detect ONCE, then restore.
        var autoDetect = _context.ChangeTracker.AutoDetectChangesEnabled;
        _context.ChangeTracker.AutoDetectChangesEnabled = false;
        try
        {
            _context.TransactionLogs.AddRange(logs);
            _context.ChangeTracker.DetectChanges();
            await _context.SaveChangesAsync(ct); // EF batches the INSERTs into as few round trips as the provider allows
        }
        finally
        {
            _context.ChangeTracker.AutoDetectChangesEnabled = autoDetect;
        }
    }

    public async Task<List<TransactionLogEntity>> GetLogsAsync(int accountId, int skip = 0, int limit = 50, DateTime? startDate = null, DateTime? endDate = null, string? type = null, CancellationToken ct = default)
    {
        var query = _context.TransactionLogs.AsNoTracking().Where(l => l.AccountId == accountId);

        if (startDate.HasValue) query = query.Where(l => l.CreatedAt >= startDate.Value);
        if (endDate.HasValue) query = query.Where(l => l.CreatedAt <= endDate.Value);
        if (!string.IsNullOrEmpty(type) && Enum.TryParse<TransactionType>(type, true, out var t))
            query = query.Where(l => l.TransactionType == t);

        return await query.OrderByDescending(l => l.CreatedAt).Skip(skip).Take(limit).ToListAsync(ct);
    }

    public async Task<List<TransactionLogEntity>> GetAllLogsAsync(int skip = 0, int limit = 50, string? type = null, DateTime? startDate = null, DateTime? endDate = null, CancellationToken ct = default)
    {
        var query = _context.TransactionLogs.AsNoTracking();

        if (startDate.HasValue) query = query.Where(l => l.CreatedAt >= startDate.Value);
        if (endDate.HasValue) query = query.Where(l => l.CreatedAt <= endDate.Value);
        if (!string.IsNullOrEmpty(type) && Enum.TryParse<TransactionType>(type, true, out var t))
            query = query.Where(l => l.TransactionType == t);

        return await query.OrderByDescending(l => l.CreatedAt).Skip(skip).Take(limit).ToListAsync(ct);
    }

    // Transfers live in their own table (fund_transfers), which carries the real source/destination
    // account numbers. The global feed surfaces one item per transfer from here (with from/to set),
    // rather than the two TRANSFER log legs — mirroring the Python reference contract.
    public async Task<List<FundTransferEntity>> GetAllTransfersAsync(int skip = 0, int limit = 50, DateTime? startDate = null, DateTime? endDate = null, CancellationToken ct = default)
    {
        var query = _context.FundTransfers.AsNoTracking();

        if (startDate.HasValue) query = query.Where(t => t.CreatedAt >= startDate.Value);
        if (endDate.HasValue) query = query.Where(t => t.CreatedAt <= endDate.Value);

        return await query.OrderByDescending(t => t.CreatedAt).Skip(skip).Take(limit).ToListAsync(ct);
    }

    public async Task<TransactionLogEntity?> GetLogByIdAsync(int transactionId, CancellationToken ct = default)
    {
        return await _context.TransactionLogs.AsNoTracking().FirstOrDefaultAsync(l => l.Id == transactionId, ct);
    }
}
