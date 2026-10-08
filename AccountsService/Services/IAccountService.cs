using AccountsService.Domain.Models;
using AccountsService.DTOs;

namespace AccountsService.Services;

public interface IAccountService
{
    Task<Account> CreateSavingsAccountAsync(SavingsAccountCreate request, CancellationToken ct = default);
    Task<Account> CreateCurrentAccountAsync(CurrentAccountCreate request, CancellationToken ct = default);
    Task<Account?> GetAccountAsync(int accountNumber, CancellationToken ct = default);
    Task<List<Account>> ListAccountsAsync(string? accountType = null, CancellationToken ct = default);
    Task<List<Account>> SearchAccountsAsync(string? nameContains, string? privilege, int limit, CancellationToken ct = default);
    Task<AccountSummaryResponse> GetAccountSummaryAsync(int sampleSize = 5, CancellationToken ct = default);
    Task<decimal> GetBalanceAsync(int accountNumber, CancellationToken ct = default);
    Task UpdateAccountAsync(int accountNumber, AccountUpdate request, CancellationToken ct = default);
    Task<bool> ActivateAccountAsync(int accountNumber, CancellationToken ct = default);
    Task<bool> InactivateAccountAsync(int accountNumber, CancellationToken ct = default);
    Task<bool> CloseAccountAsync(int accountNumber, CancellationToken ct = default);
    Task<bool> VerifyPinAsync(int accountNumber, string pin, CancellationToken ct = default);

    // Internal service-to-service operations moved to IAccountInternalService (SRP split).
}
