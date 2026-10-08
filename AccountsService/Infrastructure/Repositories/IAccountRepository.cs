using AccountsService.Domain.Models;
using AccountsService.DTOs;

namespace AccountsService.Infrastructure.Repositories;

public interface IAccountRepository
{
    Task<int> GetNextAccountNumberAsync(CancellationToken ct = default);
    Task<Account> SaveAsync(Account account, CancellationToken ct = default);

    /// <summary>
    /// Atomic, concurrency-safe read-modify-write of an account. Loads a FRESH tracked row,
    /// applies the domain operation (e.g. Debit/Credit — which enforces active-state and
    /// sufficient-funds invariants), persists it under an optimistic-concurrency token, and
    /// retries on a concurrent conflict. Prevents the lost-update race on account balances.
    /// Domain-invariant exceptions thrown by <paramref name="applyDomainOperation"/> propagate.
    /// </summary>
    Task<Account> UpdateBalanceAtomicAsync(int accountNumber, Action<Account> applyDomainOperation, CancellationToken ct = default);

    Task<Account?> GetByAccountNumberAsync(int accountNumber, CancellationToken ct = default);
    Task<Account?> GetByAadharHashAsync(string aadharHash, CancellationToken ct = default);
    Task<Account?> GetByRegistrationNoAsync(string registrationNo, CancellationToken ct = default);
    Task<List<Account>> GetAllAsync(string? accountType = null, CancellationToken ct = default);
    /// <summary>Holder-name and/or privilege search (raw-SQL backed on relational providers), capped at <paramref name="limit"/>.</summary>
    Task<List<Account>> SearchAsync(string? nameContains, string? privilege, int limit, CancellationToken ct = default);
    Task<AccountSummaryResponse> GetSummaryAsync(CancellationToken ct = default);
}
