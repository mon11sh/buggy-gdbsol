using AccountsService.DTOs;

namespace AccountsService.Services;

/// <summary>
/// Internal service-to-service account operations (debit/credit/verify-pin/lookup), consumed
/// only by the [InternalApi] endpoints. Split out of the former god AccountService so the
/// customer-facing lifecycle logic and the internal money/lookup facade have single responsibilities.
/// </summary>
public interface IAccountInternalService
{
    Task<InternalAccountDetailsResponse> GetAccountDetailsInternalAsync(int accountNumber, CancellationToken ct = default);
    Task<InternalPrivilegeResponse> GetPrivilegeInternalAsync(int accountNumber, CancellationToken ct = default);
    Task<InternalActiveResponse> CheckActiveInternalAsync(int accountNumber, CancellationToken ct = default);
    Task<InternalTransactionResponse> DebitAccountInternalAsync(int accountNumber, decimal amount, CancellationToken ct = default);
    Task<InternalTransactionResponse> CreditAccountInternalAsync(int accountNumber, decimal amount, CancellationToken ct = default);
    Task<InternalPinVerifyResponse> VerifyPinInternalAsync(int accountNumber, string pin, CancellationToken ct = default);
}
