using AccountsService.Domain.Exceptions;
using AccountsService.Domain.Models;
using AccountsService.DTOs;
using AccountsService.Infrastructure.Repositories;
using AccountsService.Integration;
using AccountsService.Utils;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using AccountsService.Domain.Enums;

namespace AccountsService.Services;

/// <summary>
/// Core domain service responsible for the Account lifecycle (Creation, Modification, State Transitions).
/// </summary>
public class AccountService : IAccountService
{
    private readonly IAccountRepository _repository;
    private readonly IAadharClient _aadharClient;
    private readonly ICompanyClient _companyClient;
    private readonly INotificationClient _notificationClient;
    private readonly EncryptionManager _encryptionManager;
    private readonly ILogger<AccountService> _logger;
    private readonly IMemoryCache _cache;
    private readonly AccountListCache _listCache;
    private readonly IPinLockoutService _pinLockout;

    public AccountService(
        IAccountRepository repository,
        IAadharClient aadharClient,
        ICompanyClient companyClient,
        INotificationClient notificationClient,
        EncryptionManager encryptionManager,
        ILogger<AccountService> logger,
        IMemoryCache cache,
        AccountListCache listCache,
        IPinLockoutService pinLockout)
    {
        _pinLockout = pinLockout;
        _repository = repository;
        _aadharClient = aadharClient;
        _companyClient = companyClient;
        _notificationClient = notificationClient;
        _encryptionManager = encryptionManager;
        _logger = logger;
        _cache = cache;
        _listCache = listCache;
    }

    public async Task<Account> CreateSavingsAccountAsync(SavingsAccountCreate request, CancellationToken ct = default)
    {
        Validators.ValidatePin(request.Pin);
        
        var aadharHash = _encryptionManager.GenerateBlindIndex(request.AadharNumber);
        var existing = await _repository.GetByAadharHashAsync(aadharHash, ct)
            // Rows written before the blind-index key split carry the legacy hash until the startup
            // upgrade pass re-indexes them; check it too so a duplicate enrolment is never missed.
            ?? await _repository.GetByAadharHashAsync(_encryptionManager.GenerateLegacyBlindIndex(request.AadharNumber), ct);
        if (existing != null && existing.Status == AccountStatus.ACTIVE)
        {
            throw new ValidationError("aadhar_number", "An active account already exists for this Aadhar number");
        }

        var verifyResult = await _aadharClient.VerifyAadharAsync(request.AadharNumber, ct);
        if (!verifyResult.TryGetValue("is_valid", out var isValid) || !(isValid is JsonElement el && el.GetBoolean()))
        {
            var msg = verifyResult.TryGetValue("message", out var m) ? m.ToString() : "Invalid Aadhar number";
            await _notificationClient.SendNotificationAsync(request.PhoneNo, 
                $"Account opening failed: The provided Aadhar number {request.AadharNumber.Substring(0, 4)}... is invalid. {msg}", 
                "ERROR", ct: ct);
            throw new ValidationError("aadhar_number", msg ?? "Invalid Aadhar number");
        }

        var pinHash = BCrypt.Net.BCrypt.HashPassword(request.Pin);
        var encryptedAadhar = _encryptionManager.EncryptData(request.AadharNumber);

        var details = new SavingsDetails(request.DateOfBirth, request.Gender, request.PhoneNo, encryptedAadhar, aadharHash);
        var initialBalance = new Money(request.InitialBalance);
        var account = SavingsAccount.Open(request.Name, request.Privilege, pinHash, details, initialBalance, DateTime.UtcNow, request.BankName, request.BankBranch, request.IfscCode);

        await _repository.SaveAsync(account, ct);
        _listCache.Invalidate();

        await _notificationClient.SendNotificationAsync(account.AccountNumber!.Value.ToString()!,
            $"Welcome {request.Name}! Your Savings account {account.AccountNumber.Value} is now active.",
            "SUCCESS", "WELCOME", ct);

        return account;
    }

    public async Task<Account> CreateCurrentAccountAsync(CurrentAccountCreate request, CancellationToken ct = default)
    {
        Validators.ValidatePin(request.Pin);

        var verifyResult = await _companyClient.VerifyRegistrationAsync(request.RegistrationNo, ct);
        if (!verifyResult.TryGetValue("is_valid", out var isValid) || !(isValid is JsonElement el && el.GetBoolean()))
        {
            var msg = verifyResult.TryGetValue("message", out var m) ? m.ToString() : "Invalid Registration Number";
            await _notificationClient.SendNotificationAsync(request.Name, 
                $"Current Account opening failed: The Registration Number {request.RegistrationNo.Substring(0, Math.Min(4, request.RegistrationNo.Length))}... is invalid. {msg}", 
                "ERROR", ct: ct);
            throw new ValidationError("registration_no", msg ?? "Invalid Registration Number");
        }

        var pinHash = BCrypt.Net.BCrypt.HashPassword(request.Pin);
        var details = new CurrentDetails(request.CompanyName, request.RegistrationNo, request.Website);
        var account = CurrentAccount.Open(request.Name, request.Privilege, pinHash, details, request.BankName, request.BankBranch, request.IfscCode);

        await _repository.SaveAsync(account, ct);
        _listCache.Invalidate();

        await _notificationClient.SendNotificationAsync(account.AccountNumber!.Value.ToString()!,
            $"Welcome {request.CompanyName}! Your Current account {account.AccountNumber.Value} is now active.",
            "SUCCESS", "WELCOME", ct);

        return account;
    }

    public async Task<Account?> GetAccountAsync(int accountNumber, CancellationToken ct = default)
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

    public async Task<List<Account>> ListAccountsAsync(string? accountType = null, CancellationToken ct = default)
    {
        // Serve from the process-wide list cache when warm; otherwise load and populate it.
        if (_listCache.TryGet(accountType, out var cached))
            return cached;

        var accounts = await _repository.GetAllAsync(accountType, ct);
        _listCache.Set(accountType, accounts);
        return accounts;
    }

    public Task<List<Account>> SearchAccountsAsync(string? nameContains, string? privilege, int limit, CancellationToken ct = default)
        => _repository.SearchAsync(nameContains, privilege, limit, ct);

    public async Task<AccountSummaryResponse> GetAccountSummaryAsync(int sampleSize = 5, CancellationToken ct = default)
    {
        return await _repository.GetSummaryAsync(ct);
    }

    public async Task<decimal> GetBalanceAsync(int accountNumber, CancellationToken ct = default)
    {
        var account = await GetAccountAsync(accountNumber, ct);
        if (account!.Status != AccountStatus.ACTIVE)
            throw new AccountInactiveError(accountNumber);
        return account.Balance.Amount;
    }

    public async Task UpdateAccountAsync(int accountNumber, AccountUpdate request, CancellationToken ct = default)
    {
        var account = await GetAccountAsync(accountNumber, ct);
        
        var name = request.Name ?? account!.Name;
        var privilege = request.Privilege ?? account!.Privilege;
        Account updatedAccount;
        if (account is SavingsAccount savingsAccount)
        {
            var phone = request.PhoneNo ?? savingsAccount.Details.PhoneNumber;
            updatedAccount = SavingsAccount.RestoreSavings(
                account.AccountNumber!,
                account.AccountType,
                name,
                privilege,
                account.PinHash,
                account.Balance,
                account.BankDetails,
                account.Status,
                account.ActivatedDate,
                account.StatusUpdatedDate,
                new SavingsDetails(savingsAccount.Details.DateOfBirth, savingsAccount.Details.Gender, phone, savingsAccount.Details.Aadhaar, savingsAccount.Details.AadhaarHash)
            );
        }
        else if (account is CurrentAccount currentAccount)
        {
            var company = request.CompanyName ?? currentAccount.Details.AccountHolderName;
            var website = request.Website ?? currentAccount.Details.Website;
            updatedAccount = CurrentAccount.RestoreCurrent(
                account.AccountNumber!,
                account.AccountType,
                name,
                privilege,
                account.PinHash,
                account.Balance,
                account.BankDetails,
                account.Status,
                account.ActivatedDate,
                account.StatusUpdatedDate,
                new CurrentDetails(company, currentAccount.Details.RegistrationNumber, website)
            );
        }
        else
        {
            throw new InvalidOperationException("Unknown account type");
        }

        await _repository.SaveAsync(updatedAccount, ct);
        _cache.Remove(CacheKeys.Account(accountNumber));
        _listCache.Invalidate();
    }

    public async Task<bool> ActivateAccountAsync(int accountNumber, CancellationToken ct = default)
    {
        var account = await GetAccountAsync(accountNumber, ct);
        account!.Activate();
        await _repository.SaveAsync(account, ct);
        _cache.Remove(CacheKeys.Account(accountNumber));
        _listCache.Invalidate();
        return true;
    }

    public async Task<bool> InactivateAccountAsync(int accountNumber, CancellationToken ct = default)
    {
        var account = await GetAccountAsync(accountNumber, ct);
        account!.Inactivate();
        await _repository.SaveAsync(account, ct);
        _cache.Remove(CacheKeys.Account(accountNumber));
        _listCache.Invalidate();
        return true;
    }

    public async Task<bool> CloseAccountAsync(int accountNumber, CancellationToken ct = default)
    {
        var account = await GetAccountAsync(accountNumber, ct);
        account!.Close();
        await _repository.SaveAsync(account, ct);
        _cache.Remove(CacheKeys.Account(accountNumber));
        _listCache.Invalidate();
        return true;
    }

    public async Task<bool> VerifyPinAsync(int accountNumber, string pin, CancellationToken ct = default)
    {
        // Brute-force lockout belongs to the use case, not the controller, so every caller is protected.
        _pinLockout.CheckLocked(accountNumber);

        var account = await GetAccountAsync(accountNumber, ct);
        if (!BCrypt.Net.BCrypt.Verify(pin, account!.PinHash))
        {
            _pinLockout.RecordFailure(accountNumber);
            throw new InvalidPinError("PIN verification failed");
        }

        _pinLockout.RecordSuccess(accountNumber);
        return true;
    }

}
