using AccountsService.Domain.Enums;
using AccountsService.Domain.Exceptions;
using AccountsService.Domain.Models;
using AccountsService.DTOs;
using AccountsService.Infrastructure.Repositories;
using AccountsService.Mapping;
using AccountsService.Utils;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace AccountsService.Services;

/// <summary>
/// Internal service-to-service account facade (money movement + lookups) used by the
/// <c>[InternalApi]</c> endpoints. Extracted from the former god AccountService.
/// </summary>
public class AccountInternalService : IAccountInternalService
{
    private readonly IAccountRepository _repository;
    private readonly IMemoryCache _cache;
    private readonly AccountResponseMapper _mapper;
    private readonly ILogger<AccountInternalService> _logger;
    private readonly AccountListCache _listCache;
    private readonly IPinLockoutService _pinLockout;

    public AccountInternalService(
        IAccountRepository repository,
        IMemoryCache cache,
        AccountResponseMapper mapper,
        ILogger<AccountInternalService> logger,
        AccountListCache listCache,
        IPinLockoutService pinLockout)
    {
        _repository = repository;
        _cache = cache;
        _mapper = mapper;
        _logger = logger;
        _listCache = listCache;
        _pinLockout = pinLockout;
    }

    private async Task<Account> GetAccountAsync(int accountNumber, CancellationToken ct = default)
    {
        var cacheKey = CacheKeys.Account(accountNumber);
        var account = await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60);
            return await _repository.GetByAccountNumberAsync(accountNumber, ct);
        });

        if (account == null)
            throw new AccountNotFoundError(accountNumber);
        return account;
    }

    public async Task<InternalAccountDetailsResponse> GetAccountDetailsInternalAsync(int accountNumber, CancellationToken ct = default)
    {
        var account = await GetAccountAsync(accountNumber, ct);
        return _mapper.ToInternalDetails(account);
    }

    public async Task<InternalPrivilegeResponse> GetPrivilegeInternalAsync(int accountNumber, CancellationToken ct = default)
    {
        try
        {
            var account = await GetAccountAsync(accountNumber, ct);
            return new InternalPrivilegeResponse
            {
                AccountNumber = accountNumber,
                Privilege = account.Privilege,
                Status = "SUCCESS"
            };
        }
        catch (AccountException ex)
        {
            return new InternalPrivilegeResponse
            {
                AccountNumber = accountNumber,
                Privilege = null!,
                Status = "FAILED",
                ErrorCode = ex.ErrorCode
            };
        }
    }

    public async Task<InternalActiveResponse> CheckActiveInternalAsync(int accountNumber, CancellationToken ct = default)
    {
        try
        {
            var account = await GetAccountAsync(accountNumber, ct);
            return new InternalActiveResponse
            {
                AccountNumber = accountNumber,
                IsActive = account.Status == AccountStatus.ACTIVE,
                Status = "SUCCESS"
            };
        }
        catch (AccountException ex)
        {
            return new InternalActiveResponse
            {
                AccountNumber = accountNumber,
                IsActive = false,
                Status = "FAILED",
                ErrorCode = ex.ErrorCode
            };
        }
    }

    public async Task<InternalTransactionResponse> DebitAccountInternalAsync(int accountNumber, decimal amount, CancellationToken ct = default)
    {
        try
        {
            // Atomic, concurrency-safe read-modify-write (no stale cache read on the money path).
            var account = await _repository.UpdateBalanceAtomicAsync(accountNumber, a => a.Debit(new Money(amount)), ct);
            _cache.Remove(CacheKeys.Account(accountNumber));
            _listCache.Invalidate();

            return new InternalTransactionResponse
            {
                Success = true,
                AccountNumber = accountNumber,
                AmountDebited = amount,
                NewBalance = account.Balance.Amount,
                Status = "SUCCESS"
            };
        }
        catch (AccountException ex) when (ex is not AccountClosedError)
        {
            return new InternalTransactionResponse
            {
                Success = false,
                AccountNumber = accountNumber,
                Status = "FAILED",
                ErrorCode = ex.ErrorCode,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<InternalTransactionResponse> CreditAccountInternalAsync(int accountNumber, decimal amount, CancellationToken ct = default)
    {
        try
        {
            // Atomic, concurrency-safe read-modify-write (no stale cache read on the money path).
            var account = await _repository.UpdateBalanceAtomicAsync(accountNumber, a => a.Credit(new Money(amount)), ct);
            _cache.Remove(CacheKeys.Account(accountNumber));
            _listCache.Invalidate();

            return new InternalTransactionResponse
            {
                Success = true,
                AccountNumber = accountNumber,
                AmountCredited = amount,
                NewBalance = account.Balance.Amount,
                Status = "SUCCESS"
            };
        }
        catch (AccountException ex) when (ex is not AccountClosedError)
        {
            return new InternalTransactionResponse
            {
                Success = false,
                AccountNumber = accountNumber,
                Status = "FAILED",
                ErrorCode = ex.ErrorCode,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<InternalPinVerifyResponse> VerifyPinInternalAsync(int accountNumber, string pin, CancellationToken ct = default)
    {
        try
        {
            // This is the path every withdrawal/transfer uses: it MUST share the brute-force lockout
            // with the public endpoint (audit Finding #7). A locked account reports ACCOUNT_LOCKED.
            _pinLockout.CheckLocked(accountNumber);

            var account = await GetAccountAsync(accountNumber, ct);
            if (!BCrypt.Net.BCrypt.Verify(pin, account.PinHash))
            {
                _pinLockout.RecordFailure(accountNumber);
                throw new InvalidPinError("PIN verification failed");
            }

            _pinLockout.RecordSuccess(accountNumber);
            return new InternalPinVerifyResponse
            {
                AccountNumber = accountNumber,
                PinValid = true,
                Status = "SUCCESS"
            };
        }
        catch (AccountException ex)
        {
            return new InternalPinVerifyResponse
            {
                AccountNumber = accountNumber,
                PinValid = false,
                Status = "FAILED",
                ErrorCode = ex.ErrorCode
            };
        }
    }
}
