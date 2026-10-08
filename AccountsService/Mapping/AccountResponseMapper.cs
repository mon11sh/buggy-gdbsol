using AccountsService.Domain.Enums;
using AccountsService.Domain.Models;
using AccountsService.DTOs;
using AccountsService.Utils;

namespace AccountsService.Mapping;

/// <summary>
/// Explicit, compile-checked domain → DTO mapping for the Accounts service.
/// </summary>
/// <remarks>
/// Replaces the AutoMapper profile: every field is assigned here by name, so a renamed or added
/// property is a compile error rather than a silently-null JSON field, mappings are trivially
/// debuggable, and the service carries no reflection-based mapper (or its licence/advisory) at all.
/// Aadhaar is stored encrypted; it is decrypted here (fail-closed) and the
/// <see cref="SavingsAccountResponse.AadharNumber"/> setter masks all but the last four digits.
/// </remarks>
public sealed class AccountResponseMapper
{
    private const string InternalDateFormat = "yyyy-MM-ddTHH:mm:ss";

    private readonly EncryptionManager _encryption;
    private readonly ILogger<AccountResponseMapper> _logger;

    public AccountResponseMapper(EncryptionManager encryption, ILogger<AccountResponseMapper> logger)
    {
        _encryption = encryption;
        _logger = logger;
    }

    /// <summary>Maps by the account's runtime type, so callers never branch on <c>AccountType</c> strings.</summary>
    public AccountResponse ToResponse(Account account) => account switch
    {
        SavingsAccount savings => ToSavingsResponse(savings),
        CurrentAccount current => ToCurrentResponse(current),
        _ => throw new NotSupportedException($"No response mapping for account type '{account.GetType().Name}'.")
    };

    public SavingsAccountResponse ToSavingsResponse(SavingsAccount account) => new()
    {
        AccountNumber = account.AccountNumber!.Value,
        AccountType = account.AccountType,
        Name = account.Name,
        Privilege = account.Privilege,
        BankName = account.BankDetails.Name,
        BankBranch = account.BankDetails.Branch,
        IfscCode = account.BankDetails.IfscCode,
        Balance = account.Balance.Amount,
        IsActive = account.Status == AccountStatus.ACTIVE,
        ActivatedDate = account.ActivatedDate,
        ClosedDate = ClosedDateOf(account),
        // Polymorphic per-type figures from the domain (abstract + virtual methods):
        MinimumBalance = account.GetMinimumBalance(),
        MonthlyMaintenanceFee = account.GetMonthlyMaintenanceFee(),
        DateOfBirth = account.Details?.DateOfBirth ?? string.Empty,
        Gender = account.Details?.Gender ?? string.Empty,
        PhoneNo = account.Details?.PhoneNumber ?? string.Empty,
        AadharNumber = DecryptAadhaar(account),
    };

    public CurrentAccountResponse ToCurrentResponse(CurrentAccount account) => new()
    {
        AccountNumber = account.AccountNumber!.Value,
        AccountType = account.AccountType,
        Name = account.Name,
        Privilege = account.Privilege,
        BankName = account.BankDetails.Name,
        BankBranch = account.BankDetails.Branch,
        IfscCode = account.BankDetails.IfscCode,
        Balance = account.Balance.Amount,
        IsActive = account.Status == AccountStatus.ACTIVE,
        ActivatedDate = account.ActivatedDate,
        ClosedDate = ClosedDateOf(account),
        MinimumBalance = account.GetMinimumBalance(),
        MonthlyMaintenanceFee = account.GetMonthlyMaintenanceFee(),
        CompanyName = account.Details?.AccountHolderName ?? string.Empty,
        RegistrationNo = account.Details?.RegistrationNumber ?? string.Empty,
        Website = account.Details?.Website,
    };

    /// <summary>Service-to-service view: no personal details, dates as fixed-format strings.</summary>
    public InternalAccountDetailsResponse ToInternalDetails(Account account) => new()
    {
        AccountNumber = account.AccountNumber!.Value,
        AccountType = account.AccountType,
        Name = account.Name,
        Balance = account.Balance.Amount,
        Privilege = account.Privilege,
        IsActive = account.Status == AccountStatus.ACTIVE,
        ActivatedDate = account.ActivatedDate.ToString(InternalDateFormat),
        ClosedDate = ClosedDateOf(account)?.ToString(InternalDateFormat),
    };

    private static DateTime? ClosedDateOf(Account account) =>
        account.Status == AccountStatus.CLOSED ? account.StatusUpdatedDate : null;

    /// <summary>
    /// Fail-closed: if the stored value cannot be authenticated/decrypted (tampering, key rotation
    /// error, corruption) the field is returned EMPTY and the problem is logged — the ciphertext is
    /// never surfaced as if it were the number, and one bad row does not break the account list.
    /// </summary>
    private string DecryptAadhaar(SavingsAccount account)
    {
        var stored = account.Details?.Aadhaar;
        if (string.IsNullOrEmpty(stored))
            return string.Empty;

        if (_encryption.TryDecrypt(stored, out var plaintext))
            return plaintext;

        _logger.LogError("Aadhaar for account {AccountNumber} could not be decrypted; returning masked-empty.",
            account.AccountNumber?.Value);
        return string.Empty;
    }
}
