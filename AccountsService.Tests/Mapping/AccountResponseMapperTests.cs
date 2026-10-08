using AccountsService.Domain.Enums;
using AccountsService.Domain.Models;
using AccountsService.DTOs;
using AccountsService.Mapping;
using AccountsService.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace AccountsService.Tests.Mapping;

/// <summary>
/// The hand-written mapper replaced AutoMapper; these tests pin the contract it must keep:
/// every DTO field populated from the domain, bank details flattened, Aadhaar decrypted then
/// masked, undecryptable Aadhaar returned empty (fail-closed), and closed_date only when CLOSED.
/// </summary>
[TestClass]
public class AccountResponseMapperTests
{
    private EncryptionManager _encryption = null!;
    private AccountResponseMapper _mapper = null!;

    [TestInitialize]
    public void Setup()
    {
        _encryption = new EncryptionManager(new AccountsService.Config.Settings { PinEncryptionKey = "12345678901234567890123456789012" });
        _mapper = new AccountResponseMapper(_encryption, new Mock<ILogger<AccountResponseMapper>>().Object);
    }

    private static readonly DateTime Activated = new(2026, 1, 15, 10, 30, 0, DateTimeKind.Utc);
    private static readonly DateTime Closed = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);

    private static SavingsAccount Savings(string storedAadhaar, AccountStatus status = AccountStatus.ACTIVE, DateTime? statusUpdated = null) =>
        SavingsAccount.RestoreSavings(
            new AccountId(1001), "SAVINGS", "Sneha Reddy", "GOLD", "hash", new Money(7500m),
            new Bank("Global Digital Bank", "Main Branch", "GDB0000001"), status, Activated, statusUpdated,
            new SavingsDetails("1995-06-20", "F", "9876543210", storedAadhaar, "idx"));

    private static CurrentAccount Current(AccountStatus status = AccountStatus.ACTIVE, DateTime? statusUpdated = null) =>
        CurrentAccount.RestoreCurrent(
            new AccountId(2002), "CURRENT", "Priya Sharma", "PREMIUM", "hash", new Money(120000m),
            new Bank("Global Digital Bank", "City Branch", "GDB0000002"), status, Activated, statusUpdated,
            new CurrentDetails("Acme Traders Pvt Ltd", "REG-778899", "https://acme.example"));

    [TestMethod]
    public void ToResponse_Savings_MapsEveryField_AndMasksDecryptedAadhaar()
    {
        var stored = _encryption.EncryptData("123456789012");

        var dto = _mapper.ToResponse(Savings(stored));

        var savings = dto as SavingsAccountResponse;
        Assert.IsNotNull(savings, "runtime type must be the Savings DTO so type-specific fields serialize");
        Assert.AreEqual(1001, savings.AccountNumber);
        Assert.AreEqual("SAVINGS", savings.AccountType);
        Assert.AreEqual("Sneha Reddy", savings.Name);
        Assert.AreEqual("GOLD", savings.Privilege);
        Assert.AreEqual("Global Digital Bank", savings.BankName);
        Assert.AreEqual("Main Branch", savings.BankBranch);
        Assert.AreEqual("GDB0000001", savings.IfscCode);
        Assert.AreEqual(7500m, savings.Balance);
        Assert.IsTrue(savings.IsActive);
        Assert.AreEqual(Activated, savings.ActivatedDate);
        Assert.IsNull(savings.ClosedDate);
        Assert.AreEqual(1000m, savings.MinimumBalance);
        Assert.AreEqual(0m, savings.MonthlyMaintenanceFee);
        Assert.AreEqual("1995-06-20", savings.DateOfBirth);
        Assert.AreEqual("F", savings.Gender);
        Assert.AreEqual("9876543210", savings.PhoneNo);
        Assert.AreEqual("********9012", savings.AadharNumber, "decrypted then masked to the last four digits");
    }

    [TestMethod]
    public void ToResponse_Savings_UndecryptableAadhaar_IsEmpty_NeverCiphertext()
    {
        var dto = (SavingsAccountResponse)_mapper.ToResponse(Savings("v1:not-real-ciphertext"));

        Assert.AreEqual(string.Empty, dto.AadharNumber);
    }

    [TestMethod]
    public void ToResponse_Current_MapsCompanyFields_AndClosedDateOnlyWhenClosed()
    {
        var dto = _mapper.ToResponse(Current(AccountStatus.CLOSED, Closed));

        var current = dto as CurrentAccountResponse;
        Assert.IsNotNull(current);
        Assert.AreEqual(2002, current.AccountNumber);
        Assert.AreEqual("Acme Traders Pvt Ltd", current.CompanyName);
        Assert.AreEqual("REG-778899", current.RegistrationNo);
        Assert.AreEqual("https://acme.example", current.Website);
        Assert.AreEqual("City Branch", current.BankBranch);
        Assert.IsFalse(current.IsActive);
        Assert.AreEqual(Closed, current.ClosedDate);
        Assert.AreEqual(5000m, current.MinimumBalance);
        Assert.AreEqual(500m, current.MonthlyMaintenanceFee);

        // A status change that is not CLOSED (e.g. SUSPENDED) must not surface as closed_date.
        var suspended = (CurrentAccountResponse)_mapper.ToResponse(Current(AccountStatus.SUSPENDED, Closed));
        Assert.IsNull(suspended.ClosedDate);
        Assert.IsFalse(suspended.IsActive);
    }

    [TestMethod]
    public void ToInternalDetails_UsesFixedDateFormat_AndOmitsPersonalData()
    {
        var dto = _mapper.ToInternalDetails(Savings(_encryption.EncryptData("123456789012"), AccountStatus.CLOSED, Closed));

        Assert.AreEqual(1001, dto.AccountNumber);
        Assert.AreEqual("SAVINGS", dto.AccountType);
        Assert.AreEqual("Sneha Reddy", dto.Name);
        Assert.AreEqual(7500m, dto.Balance);
        Assert.AreEqual("GOLD", dto.Privilege);
        Assert.IsFalse(dto.IsActive);
        Assert.AreEqual("2026-01-15T10:30:00", dto.ActivatedDate);
        Assert.AreEqual("2026-03-01T09:00:00", dto.ClosedDate);
    }
}
