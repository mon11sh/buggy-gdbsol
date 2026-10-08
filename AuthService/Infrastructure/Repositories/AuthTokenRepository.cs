using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AuthService.Infrastructure.Data;
using AuthService.Domain.Models;
using AuthService.Mapping;

namespace AuthService.Infrastructure.Repositories;

public interface IAuthTokenRepository
{
    Task CreateTokenAsync(AuthToken token, CancellationToken ct = default);
    Task RevokeTokenAsync(TokenJti tokenJti, CancellationToken ct = default);
    Task<AuthToken?> GetTokenAsync(TokenJti tokenJti, CancellationToken ct = default);

    /// <summary>Deletes tokens that expired before <paramref name="expiredBeforeUtc"/>; returns the row count.</summary>
    Task<int> PurgeExpiredAsync(DateTime expiredBeforeUtc, CancellationToken ct = default);
}

public class AuthTokenRepository : IAuthTokenRepository
{
    private readonly AppDbContext _context;

    public AuthTokenRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task CreateTokenAsync(AuthToken token, CancellationToken ct = default)
    {
        var entity = AuthMapper.ToEntity(token);
        await _context.AuthTokens.AddAsync(entity, ct);
    }

    public async Task RevokeTokenAsync(TokenJti tokenJti, CancellationToken ct = default)
    {
        var tokenEntity = await _context.AuthTokens.FirstOrDefaultAsync(t => t.TokenJti == tokenJti.Value, ct);
        if (tokenEntity != null)
        {
            tokenEntity.IsRevoked = true;
        }
    }

    public async Task<AuthToken?> GetTokenAsync(TokenJti tokenJti, CancellationToken ct = default)
    {
        var tokenEntity = await _context.AuthTokens.FirstOrDefaultAsync(t => t.TokenJti == tokenJti.Value, ct);
        if (tokenEntity == null) return null;
        
        return AuthMapper.ToDomain(tokenEntity);
    }

    public async Task<int> PurgeExpiredAsync(DateTime expiredBeforeUtc, CancellationToken ct = default)
    {
        // CONCEPT: bulk delete - one DELETE statement, no entities loaded; the InMemory provider cannot
        // translate it, so there we fall back to load-then-remove (EntityState.Deleted on SaveChanges).
        if (_context.Database.IsRelational())
            return await _context.AuthTokens.Where(t => t.ExpiresAt < expiredBeforeUtc).ExecuteDeleteAsync(ct);

        var expired = await _context.AuthTokens.Where(t => t.ExpiresAt < expiredBeforeUtc).ToListAsync(ct);
        _context.AuthTokens.RemoveRange(expired);
        await _context.SaveChangesAsync(ct);
        return expired.Count;
    }
}
