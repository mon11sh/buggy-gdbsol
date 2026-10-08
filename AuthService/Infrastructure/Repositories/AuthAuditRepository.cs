using System.Threading.Tasks;
using AuthService.Infrastructure.Data;
using AuthService.Domain.Models;
using AuthService.Mapping;

namespace AuthService.Infrastructure.Repositories;

public interface IAuthAuditRepository
{
    Task LogAuditAsync(AuthAuditLog auditLog, CancellationToken ct = default);
}

public class AuthAuditRepository : IAuthAuditRepository
{
    private readonly AppDbContext _context;

    public AuthAuditRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task LogAuditAsync(AuthAuditLog auditLog, CancellationToken ct = default)
    {
        var entity = AuthMapper.ToEntity(auditLog);
        await _context.AuthAuditLogs.AddAsync(entity, ct);
    }
}
