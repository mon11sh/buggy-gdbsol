using Microsoft.EntityFrameworkCore.Storage;
using UsersService.Infrastructure.Data;
using UsersService.Infrastructure.Repositories;

namespace UsersService.Services;

public interface IUnitOfWork
{
    IUserRepository Users { get; }
    IAuditRepository Audit { get; }
    
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default);
    Task CommitAsync(CancellationToken ct = default);
    Task RollbackAsync(CancellationToken ct = default);
}

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;

    public IUserRepository Users { get; }
    public IAuditRepository Audit { get; }

    public UnitOfWork(AppDbContext context, IUserRepository users, IAuditRepository audit)
    {
        _context = context;
        Users = users;
        Audit = audit;
    }

    public async Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default)
    {
        return await _context.Database.BeginTransactionAsync(ct);
    }

    public async Task CommitAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }

    public async Task RollbackAsync(CancellationToken ct = default)
    {
        if (_context.Database.CurrentTransaction != null)
        {
            await _context.Database.CurrentTransaction.RollbackAsync(ct);
        }
    }
}
