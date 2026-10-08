// CONCEPT: ADO.NET unit-of-work
// The EF UnitOfWork batches staged changes and flushes them in CommitAsync() via
// DbContext.SaveChangesAsync(). The ADO.NET repositories instead execute every write
// immediately against SQL Server, so there is nothing left to flush: CommitAsync() is a
// no-op here. This keeps AuthenticationService's "repo call + CommitAsync" flow working
// unchanged under either data-access strategy.
namespace AuthService.Infrastructure.Repositories;

public class AdoNetUnitOfWork : IUnitOfWork
{
    public IAuthTokenRepository Tokens { get; }
    public IAuthAuditRepository Audit { get; }

    public AdoNetUnitOfWork(IAuthTokenRepository tokens, IAuthAuditRepository audit)
    {
        Tokens = tokens;
        Audit = audit;
    }

    public Task CommitAsync(CancellationToken ct = default) => Task.CompletedTask;
}
