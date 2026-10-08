using Microsoft.EntityFrameworkCore;
using TransactionsService.Domain.Models;
using TransactionsService.Infrastructure.Data;
using TransactionsService.Mapping;

namespace TransactionsService.Infrastructure.Repositories;

public interface ITransactionRepository
{
    Task<FundTransferEntity> CreateTransferRecordAsync(int sourceAccountId, int destAccountId, decimal amount, TransferMode mode, CancellationToken ct = default);
    Task UpdateTransferStatusAsync(int transferId, TransferStatus status, string? failureReason = null, CancellationToken ct = default);
    Task<decimal> GetDailyTransferTotalAsync(int accountId, CancellationToken ct = default);

    /// <summary>
    /// Transfers still in <paramref name="status"/> that were created before <paramref name="olderThanUtc"/> —
    /// the reconciliation worker's input (stuck PENDING rows, COMPENSATION_FAILED rows).
    /// </summary>
    Task<List<FundTransferEntity>> GetStaleTransfersAsync(TransferStatus status, DateTime olderThanUtc, int limit, CancellationToken ct = default);

    /// <summary>The transfer with both ledger legs eagerly loaded (null when unknown).</summary>
    Task<FundTransferEntity?> GetTransferWithLegsAsync(int transferId, CancellationToken ct = default);

    /// <summary>Explicitly loads <see cref="FundTransferEntity.Legs"/> onto an already-materialised transfer.</summary>
    Task LoadLegsAsync(FundTransferEntity transfer, CancellationToken ct = default);
}

public class TransactionRepository : ITransactionRepository
{
    private readonly AppDbContext _context;

    public TransactionRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<FundTransferEntity> CreateTransferRecordAsync(int sourceAccountId, int destAccountId, decimal amount, TransferMode mode, CancellationToken ct = default)
    {
        var domain = FundTransfer.Create(new AccountId(sourceAccountId), new AccountId(destAccountId), new Money(amount), mode);
        var transfer = TransactionMapper.ToEntity(domain);
        _context.FundTransfers.Add(transfer);
        await _context.SaveChangesAsync(ct);
        return transfer;
    }

    public async Task UpdateTransferStatusAsync(int transferId, TransferStatus status, string? failureReason = null, CancellationToken ct = default)
    {
        var transfer = await _context.FundTransfers.FindAsync(new object[] { transferId }, ct);
        if (transfer != null)
        {
            // Drive the transition through the domain aggregate (invariants + base status kept in
            // sync) rather than stamping a magic string, and preserve the REAL failure reason.
            var domain = TransactionMapper.ToDomain(transfer);
            switch (status)
            {
                case TransferStatus.COMPLETED: domain.MarkCompleted(); break;
                case TransferStatus.FAILED: domain.MarkFailed(failureReason ?? "Transfer failed"); break;
                case TransferStatus.PROCESSING: domain.MarkProcessing(); break;
                case TransferStatus.COMPENSATION_FAILED: domain.MarkCompensationFailed(failureReason ?? "Compensation failed"); break;
            }

            var updatedEntity = TransactionMapper.ToEntity(domain);
            transfer.Status = updatedEntity.Status;
            transfer.FailureReason = updatedEntity.FailureReason;

            await _context.SaveChangesAsync(ct);
        }
    }

    public async Task<List<FundTransferEntity>> GetStaleTransfersAsync(TransferStatus status, DateTime olderThanUtc, int limit, CancellationToken ct = default)
    {
        var statusText = status.ToString();
        return await _context.FundTransfers
            .AsNoTracking()
            .Where(t => t.Status == statusText && t.CreatedAt < olderThanUtc)
            .OrderBy(t => t.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);
    }

    public Task<FundTransferEntity?> GetTransferWithLegsAsync(int transferId, CancellationToken ct = default) =>
        // CONCEPT: eager loading + split query - Include pulls the legs in the same call; AsSplitQuery
        // sends one SELECT per collection instead of one JOIN that repeats the parent row per leg.
        _context.FundTransfers
            .AsNoTracking()
            .Include(t => t.Legs.OrderBy(l => l.Id))
            .AsSplitQuery()
            .FirstOrDefaultAsync(t => t.Id == transferId, ct);

    public async Task LoadLegsAsync(FundTransferEntity transfer, CancellationToken ct = default)
    {
        // CONCEPT: explicit loading - the navigation is loaded on demand for an entity we already hold.
        // Entry(...) needs a tracked entity: Attach puts an untracked one in as Unchanged (no write).
        if (_context.Entry(transfer).State == EntityState.Detached)
            _context.Attach(transfer);
        await _context.Entry(transfer).Collection(t => t.Legs).LoadAsync(ct);
    }

    public async Task<decimal> GetDailyTransferTotalAsync(int accountId, CancellationToken ct = default)
    {
        var today = DateTime.UtcNow.Date;
        var total = await _context.FundTransfers
            .Where(t => t.SourceAccountId == accountId && t.CreatedAt >= today && t.Status == "COMPLETED")
            .SumAsync(t => t.Amount, ct);
        return total;
    }
}
