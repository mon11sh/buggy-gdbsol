using AccountsService.Domain.Exceptions;
using AccountsService.Domain.Models;
using AccountsService.DTOs;
using AccountsService.Infrastructure.Data;
using AccountsService.Infrastructure.Data.Entities;
using AccountsService.Mapping;
using Microsoft.EntityFrameworkCore;

namespace AccountsService.Infrastructure.Repositories;

public class AccountRepository : IAccountRepository
{
    private readonly AppDbContext _context;

    public AccountRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<int> GetNextAccountNumberAsync(CancellationToken ct = default)
    {
        var maxAccountNumber = await _context.Accounts.MaxAsync(a => (int?)a.AccountNumber, ct);
        return maxAccountNumber.HasValue && maxAccountNumber.Value >= 1000 
            ? maxAccountNumber.Value + 1 
            : 1000;
    }

    public async Task<Account> SaveAsync(Account account, CancellationToken ct = default)
    {
        if (account.AccountNumber == null)
        {
            // New account
            var nextNum = await GetNextAccountNumberAsync(ct);
            account.AssignNumber(new AccountId(nextNum));

            var entity = AccountMapper.ToEntity(account);

            _context.Accounts.Add(entity);
            
            try 
            {
                await _context.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex)
            {
                if (ex.InnerException?.Message.Contains("UNIQUE constraint failed") == true ||
                    ex.InnerException?.Message.Contains("Duplicate entry") == true ||
                    ex.InnerException?.Message.Contains("duplicate key value") == true)
                {
                    throw new AccountException("Duplicate constraint violation", "DUPLICATE_ERROR");
                }
                throw;
            }
        }
        else
        {
            // Update existing account
            var entity = await _context.Accounts
                .Include(a => a.SavingsDetails)
                .Include(a => a.CurrentDetails)
                .FirstOrDefaultAsync(a => a.AccountNumber == account.AccountNumber.Value, ct);

            if (entity == null)
                throw new AccountNotFoundError(account.AccountNumber.Value);

            var mappedEntity = AccountMapper.ToEntity(account);
            
            entity.Name = mappedEntity.Name;
            entity.Privilege = mappedEntity.Privilege;
            entity.Balance = mappedEntity.Balance;
            entity.IsActive = mappedEntity.IsActive;
            entity.ClosedDate = mappedEntity.ClosedDate;
            entity.PinHash = mappedEntity.PinHash; // Allows updating PIN

            if (account is SavingsAccount && entity.SavingsDetails != null && mappedEntity.SavingsDetails != null)
            {
                entity.SavingsDetails.PhoneNo = mappedEntity.SavingsDetails.PhoneNo;
                // DOB, Gender, Aadhar shouldn't change, but if needed update here
            }
            else if (account is CurrentAccount && entity.CurrentDetails != null && mappedEntity.CurrentDetails != null)
            {
                entity.CurrentDetails.CompanyName = mappedEntity.CurrentDetails.CompanyName;
                entity.CurrentDetails.Website = mappedEntity.CurrentDetails.Website;
            }

            await _context.SaveChangesAsync(ct);
        }

        return account;
    }

    public async Task<Account> UpdateBalanceAtomicAsync(int accountNumber, Action<Account> applyDomainOperation, CancellationToken ct = default)
    {
        const int maxRetries = 3;
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            var entity = await _context.Accounts
                .Include(a => a.SavingsDetails)
                .Include(a => a.CurrentDetails)
                .FirstOrDefaultAsync(a => a.AccountNumber == accountNumber, ct);

            if (entity == null)
                throw new AccountNotFoundError(accountNumber);

            // Rebuild the aggregate from the FRESH row, apply the caller's domain operation
            // (Debit/Credit enforce active-state + funds invariants), then copy the mutated
            // balance/state back onto the tracked entity for persistence.
            var domain = AccountMapper.ToDomain(entity);
            applyDomainOperation(domain);

            var mapped = AccountMapper.ToEntity(domain);
            entity.Balance = mapped.Balance;
            entity.IsActive = mapped.IsActive;
            entity.ClosedDate = mapped.ClosedDate;

            try
            {
                await _context.SaveChangesAsync(ct);
                return domain;
            }
            catch (DbUpdateConcurrencyException)
            {
                // Another transaction changed the row first. Drop the stale tracked state and
                // retry from a fresh read so the operation re-applies against the latest balance.
                foreach (var e in _context.ChangeTracker.Entries().ToList())
                    e.State = EntityState.Detached;

                if (attempt == maxRetries)
                    throw new AccountException("Account was modified concurrently; please retry.", "CONCURRENCY_CONFLICT");
            }
        }

        // Unreachable: the loop either returns or throws.
        throw new AccountException("Account was modified concurrently; please retry.", "CONCURRENCY_CONFLICT");
    }

    // CONCEPT: compiled query — the hottest read in the service (every balance/PIN/details call).
    // EF Core normally re-translates the expression tree to SQL on every call and caches by shape;
    // compiling once skips that lookup entirely. The delegate is static: one compilation per process.
    private static readonly Func<AppDbContext, int, CancellationToken, Task<AccountEntity?>> ByAccountNumber =
        EF.CompileAsyncQuery((AppDbContext db, int accountNumber, CancellationToken ct) =>
            db.Accounts
                .AsNoTracking()
                .Include(a => a.SavingsDetails)
                .Include(a => a.CurrentDetails)
                .FirstOrDefault(a => a.AccountNumber == accountNumber));

    public async Task<Account?> GetByAccountNumberAsync(int accountNumber, CancellationToken ct = default)
    {
        var entity = await ByAccountNumber(_context, accountNumber, ct);

        if (entity == null) return null;

        return AccountMapper.ToDomain(entity);
    }

    public async Task<List<Account>> SearchAsync(string? nameContains, string? privilege, int limit, CancellationToken ct = default)
    {
        IQueryable<AccountEntity> query;

        if (_context.Database.IsRelational() && !string.IsNullOrWhiteSpace(nameContains))
        {
            // CONCEPT: FromSqlInterpolated — the interpolated value becomes a DbParameter (never
            // concatenated into the SQL), so a search term like "'; DROP TABLE" is just a string.
            // Standard SQL only (UPPER/LIKE) so the same statement runs on every relational provider.
            var pattern = $"%{nameContains.Trim().ToUpperInvariant()}%";
            query = _context.Accounts.FromSqlInterpolated($"SELECT * FROM accounts WHERE UPPER(name) LIKE {pattern}");

            if (!string.IsNullOrWhiteSpace(privilege))
                query = query.Where(a => a.Privilege == privilege); // composed on top: EF wraps the raw SQL in a subquery
        }
        else if (_context.Database.IsRelational() && !string.IsNullOrWhiteSpace(privilege))
        {
            // CONCEPT: FromSqlRaw — positional placeholders are turned into parameters by EF; the
            // SQL text itself is fixed (never build it from user input).
            query = _context.Accounts.FromSqlRaw("SELECT * FROM accounts WHERE privilege = {0}", privilege.ToUpperInvariant());
        }
        else
        {
            // Non-relational provider (in-memory teaching stack) cannot run SQL: same filter via LINQ.
            query = _context.Accounts.AsQueryable();
            if (!string.IsNullOrWhiteSpace(nameContains))
                query = query.Where(a => a.Name.ToUpper().Contains(nameContains.Trim().ToUpperInvariant()));
            if (!string.IsNullOrWhiteSpace(privilege))
                query = query.Where(a => a.Privilege == privilege.ToUpperInvariant());
        }

        var entities = await query
            .AsNoTracking()
            .Include(a => a.SavingsDetails)
            .Include(a => a.CurrentDetails)
            .OrderBy(a => a.AccountNumber)
            .Take(limit)
            .ToListAsync(ct);

        return entities.Select(e => AccountMapper.ToDomain(e)).ToList();
    }

    public async Task<Account?> GetByAadharHashAsync(string aadharHash, CancellationToken ct = default)
    {
        var savingsDetails = await _context.SavingsAccountDetails
            .AsNoTracking()
            .Include(s => s.Account)
            .FirstOrDefaultAsync(s => s.AadharHash == aadharHash, ct);

        if (savingsDetails?.Account == null) return null;

        return AccountMapper.ToDomain(savingsDetails.Account, savingsDetails, null);
    }

    public async Task<Account?> GetByRegistrationNoAsync(string registrationNo, CancellationToken ct = default)
    {
        var currentDetails = await _context.CurrentAccountDetails
            .AsNoTracking()
            .Include(c => c.Account)
            .FirstOrDefaultAsync(c => c.RegistrationNo == registrationNo, ct);

        if (currentDetails?.Account == null) return null;

        return AccountMapper.ToDomain(currentDetails.Account, null, currentDetails);
    }

    public async Task<List<Account>> GetAllAsync(string? accountType = null, CancellationToken ct = default)
    {
        var query = _context.Accounts
            .AsNoTracking()
            .Include(a => a.SavingsDetails)
            .Include(a => a.CurrentDetails)
            .AsQueryable();

        if (!string.IsNullOrEmpty(accountType))
        {
            query = query.Where(a => a.AccountType == accountType);
        }

        var entities = await query.OrderBy(a => a.AccountNumber).ToListAsync(ct);
        
        return entities.Select(e => AccountMapper.ToDomain(e)).ToList();
    }

    public async Task<AccountSummaryResponse> GetSummaryAsync(CancellationToken ct = default)
    {
        var totalAccounts = await _context.Accounts.CountAsync(ct);
        var totalBalance = await _context.Accounts.SumAsync(a => a.Balance, ct);
        var activeCount = await _context.Accounts.CountAsync(a => a.IsActive, ct);
        
        var typeBreakdown = await _context.Accounts
            .GroupBy(a => a.AccountType)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToDictionaryAsync(k => k.Type, v => v.Count, ct);
            
        var privilegeBreakdown = await _context.Accounts
            .GroupBy(a => a.Privilege)
            .Select(g => new { Privilege = g.Key, Count = g.Count() })
            .ToDictionaryAsync(k => k.Privilege, v => v.Count, ct);
            
        var sample = await _context.Accounts
            .OrderByDescending(a => a.CreatedAt)
            .Take(5)
            .Select(a => new RecentAccountDto { AccountNumber = a.AccountNumber, Name = a.Name, AccountType = a.AccountType })
            .ToListAsync(ct);

        return new AccountSummaryResponse
        {
            TotalAccounts = totalAccounts,
            TotalBalance = totalBalance,
            ActiveAccounts = activeCount,
            ByType = typeBreakdown,
            ByPrivilege = privilegeBreakdown,
            RecentAccounts = sample
        };
    }
}
