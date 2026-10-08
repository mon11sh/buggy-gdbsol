using Microsoft.EntityFrameworkCore;
using TransactionsService.Infrastructure.Data;

namespace TransactionsService.Infrastructure.Repositories;

public interface ITransferLimitRepository
{
    Task<TransferLimitEntity?> GetLimitByPrivilegeAsync(string privilege, CancellationToken ct = default);
    Task<List<TransferLimitEntity>> GetAllLimitsAsync(CancellationToken ct = default);
    Task<TransferLimitEntity> CreateLimitAsync(string privilege, decimal dailyLimit, decimal perTransactionLimit, CancellationToken ct = default);
    Task<TransferLimitEntity?> UpdateLimitAsync(string privilege, decimal dailyLimit, decimal perTransactionLimit, CancellationToken ct = default);
    Task<decimal> GetDailyUsedAmountAsync(int accountNumber, DateTime? date = null, CancellationToken ct = default);
    Task<int> GetDailyTransactionCountAsync(int accountNumber, DateTime? date = null, CancellationToken ct = default);
}

public class TransferLimitRepository : ITransferLimitRepository
{
    private readonly AppDbContext _context;

    public TransferLimitRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<TransferLimitEntity?> GetLimitByPrivilegeAsync(string privilege, CancellationToken ct = default)
    {
        // CONCEPT: many-to-many navigation - Include walks through the join table EF manages.
        return await _context.TransferLimits.Include(l => l.AllowedModes).FirstOrDefaultAsync(l => l.Privilege == privilege, ct);
    }

    public async Task<List<TransferLimitEntity>> GetAllLimitsAsync(CancellationToken ct = default)
    {
        return await _context.TransferLimits.Include(l => l.AllowedModes).OrderBy(l => l.Privilege).ToListAsync(ct);
    }

    public async Task<TransferLimitEntity> CreateLimitAsync(string privilege, decimal dailyLimit, decimal perTransactionLimit, CancellationToken ct = default)
    {
        var limit = new TransferLimitEntity
        {
            Privilege = privilege,
            DailyLimit = dailyLimit,
            PerTransactionLimit = perTransactionLimit
        };
        _context.TransferLimits.Add(limit);
        await _context.SaveChangesAsync(ct);
        return limit;
    }

    public async Task<TransferLimitEntity?> UpdateLimitAsync(string privilege, decimal dailyLimit, decimal perTransactionLimit, CancellationToken ct = default)
    {
        var limit = await GetLimitByPrivilegeAsync(privilege, ct);
        if (limit != null)
        {
            limit.DailyLimit = dailyLimit;
            limit.PerTransactionLimit = perTransactionLimit;
            await _context.SaveChangesAsync(ct);
        }
        return limit;
    }

    public async Task<decimal> GetDailyUsedAmountAsync(int accountNumber, DateTime? date = null, CancellationToken ct = default)
    {
        var targetDate = date ?? DateTime.UtcNow;
        var start = targetDate.Date;
        var end = start.AddDays(1);

        var total = await _context.FundTransfers
            .Where(f => f.SourceAccountId == accountNumber && f.CreatedAt >= start && f.CreatedAt < end)
            .SumAsync(f => f.Amount, ct);
            
        return total;
    }

    public async Task<int> GetDailyTransactionCountAsync(int accountNumber, DateTime? date = null, CancellationToken ct = default)
    {
        var targetDate = date ?? DateTime.UtcNow;
        var start = targetDate.Date;
        var end = start.AddDays(1);

        var count = await _context.FundTransfers
            .Where(f => f.SourceAccountId == accountNumber && f.CreatedAt >= start && f.CreatedAt < end)
            .CountAsync(ct);
            
        return count;
    }
}
