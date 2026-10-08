using Microsoft.EntityFrameworkCore;
using TransactionsService.Infrastructure.Data;

namespace TransactionsService.Infrastructure.Repositories;

public interface IIdempotencyRepository
{
    Task<IdempotencyKeyEntity?> GetKeyAsync(string key, CancellationToken ct = default);
    Task<bool> TryReserveKeyAsync(string key, CancellationToken ct = default);
    Task CompleteKeyAsync(string key, string responseBody, int statusCode, CancellationToken ct = default);
    Task ReleaseKeyAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Deletes reservations (status 202) older than <paramref name="olderThanUtc"/>. A reservation
    /// outlives its request only when the process died mid-flight; without this sweep such a key
    /// would block the client's retry forever. Returns the number of rows removed.
    /// </summary>
    Task<int> ReleaseStaleReservationsAsync(DateTime olderThanUtc, CancellationToken ct = default);
}

public class IdempotencyRepository : IIdempotencyRepository
{
    private readonly AppDbContext _context;

    public IdempotencyRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IdempotencyKeyEntity?> GetKeyAsync(string key, CancellationToken ct = default)
    {
        return await _context.IdempotencyKeys.FirstOrDefaultAsync(k => k.Key == key, ct);
    }

    public async Task<bool> TryReserveKeyAsync(string key, CancellationToken ct = default)
    {
        try
        {
            _context.IdempotencyKeys.Add(new IdempotencyKeyEntity
            {
                Key = key,
                ResponseBody = "PROCESSING",
                StatusCode = 202
            });
            await _context.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException)
        {
            return false; // Unique constraint violation
        }
    }

    public async Task CompleteKeyAsync(string key, string responseBody, int statusCode, CancellationToken ct = default)
    {
        var existing = await _context.IdempotencyKeys.FirstOrDefaultAsync(k => k.Key == key, ct);
        if (existing != null)
        {
            existing.ResponseBody = responseBody;
            existing.StatusCode = statusCode;
            await _context.SaveChangesAsync(ct);
        }
    }

    public async Task ReleaseKeyAsync(string key, CancellationToken ct = default)
    {
        var existing = await _context.IdempotencyKeys.FirstOrDefaultAsync(k => k.Key == key, ct);
        if (existing != null)
        {
            _context.IdempotencyKeys.Remove(existing);
            await _context.SaveChangesAsync(ct);
        }
    }

    public async Task<int> ReleaseStaleReservationsAsync(DateTime olderThanUtc, CancellationToken ct = default)
    {
        // CONCEPT: bulk delete - ExecuteDeleteAsync issues ONE DELETE statement (no entities loaded,
        // no change tracker); the InMemory provider has no bulk support, so it falls back to load-then-remove.
        if (_context.Database.IsRelational())
            return await _context.IdempotencyKeys
                .Where(k => k.StatusCode == 202 && k.CreatedAt < olderThanUtc)
                .ExecuteDeleteAsync(ct);

        var stale = await _context.IdempotencyKeys
            .Where(k => k.StatusCode == 202 && k.CreatedAt < olderThanUtc)
            .ToListAsync(ct);
        if (stale.Count == 0)
            return 0;

        _context.IdempotencyKeys.RemoveRange(stale);
        await _context.SaveChangesAsync(ct);
        return stale.Count;
    }
}
