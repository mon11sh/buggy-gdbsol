using AuthService.Infrastructure.Data;

namespace AuthService.Infrastructure.Repositories;

public interface IUnitOfWork
{
    IAuthTokenRepository Tokens { get; }
    IAuthAuditRepository Audit { get; }
    Task CommitAsync(CancellationToken ct = default);
}

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;

    public IAuthTokenRepository Tokens { get; }
    public IAuthAuditRepository Audit { get; }

    public UnitOfWork(AppDbContext context, IAuthTokenRepository tokens, IAuthAuditRepository audit)
    {
        _context = context;
        Tokens = tokens;
        Audit = audit;
    }

    public async Task CommitAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }
}
