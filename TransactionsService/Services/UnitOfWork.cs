using System.Transactions;
using Microsoft.EntityFrameworkCore;
using TransactionsService.Infrastructure.Data;
using TransactionsService.Infrastructure.Repositories;

namespace TransactionsService.Services;

public interface IUnitOfWork
{
    ITransactionRepository Transactions { get; }
    ITransactionLogRepository TransactionLogs { get; }
    IIdempotencyRepository Idempotency { get; }

    /// <summary>
    /// Runs <paramref name="work"/> so that every repository write inside it commits atomically or
    /// not at all, on either data-access path. The delegate may be executed MORE THAN ONCE when the
    /// provider's retrying execution strategy re-runs it after a transient failure, so it must only
    /// contain database writes (no HTTP calls, no side effects outside the transaction).
    /// </summary>
    Task ExecuteInTransactionAsync(Func<Task> work, CancellationToken ct = default);
}

/// <summary>EF Core path: one <see cref="DbContext"/> transaction shared by the EF repositories.</summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;

    public ITransactionRepository Transactions { get; }
    public ITransactionLogRepository TransactionLogs { get; }
    public IIdempotencyRepository Idempotency { get; }

    public UnitOfWork(
        AppDbContext context,
        ITransactionRepository transactions,
        ITransactionLogRepository transactionLogs,
        IIdempotencyRepository idempotency)
    {
        _context = context;
        Transactions = transactions;
        TransactionLogs = transactionLogs;
        Idempotency = idempotency;
    }

    public async Task ExecuteInTransactionAsync(Func<Task> work, CancellationToken ct = default)
    {
        // The in-memory provider has no transactions (BeginTransaction throws); it is a dev/test
        // convenience, so writes there are simply not atomic.
        if (_context.Database.IsInMemory())
        {
            await work();
            return;
        }

        // EnableRetryOnFailure forbids user-initiated transactions outside the execution strategy:
        // the strategy owns the retry, and re-runs the whole unit (transaction included).
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _context.Database.BeginTransactionAsync(ct);
            await work();
            await tx.CommitAsync(ct); // disposing without commit rolls back
        });
    }
}

/// <summary>
/// ADO.NET path. The AdoNet repositories each open their own short-lived <c>SqlConnection</c>, so
/// the unit of work is an ambient <see cref="TransactionScope"/>: every connection opened inside it
/// auto-enlists, and because the repositories open connections sequentially (never two at once)
/// SqlClient hands back the SAME pooled physical connection for the whole scope — one local SQL
/// Server transaction, no MSDTC promotion, no repository changes.
/// </summary>
/// <remarks>
/// ponytail: sequential-use only. Opening two connections concurrently inside one scope would
/// promote to a distributed transaction; if that is ever needed, thread an explicit
/// SqlConnection/SqlTransaction through the repositories instead.
/// </remarks>
public class AdoNetUnitOfWork : IUnitOfWork
{
    public ITransactionRepository Transactions { get; }
    public ITransactionLogRepository TransactionLogs { get; }
    public IIdempotencyRepository Idempotency { get; }

    public AdoNetUnitOfWork(
        ITransactionRepository transactions,
        ITransactionLogRepository transactionLogs,
        IIdempotencyRepository idempotency)
    {
        Transactions = transactions;
        TransactionLogs = transactionLogs;
        Idempotency = idempotency;
    }

    public async Task ExecuteInTransactionAsync(Func<Task> work, CancellationToken ct = default)
    {
        using var scope = new TransactionScope(
            TransactionScopeOption.Required,
            new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted, Timeout = TimeSpan.FromSeconds(30) },
            TransactionScopeAsyncFlowOption.Enabled);

        await work();
        scope.Complete(); // disposing an un-completed scope rolls back
    }
}
