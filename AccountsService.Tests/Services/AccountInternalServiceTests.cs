using AccountsService.Domain.Enums;
using AccountsService.Domain.Models;
using AccountsService.Infrastructure.Repositories;
using AccountsService.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace AccountsService.Tests.Services;

/// <summary>
/// Tests the internal service-to-service facade (debit/credit/verify-pin/lookup) that was
/// extracted from the former god AccountService — previously untested logic.
/// </summary>
[TestClass]
public class AccountInternalServiceTests
{
    private Mock<IAccountRepository> _repoMock = null!;
    private IMemoryCache _cache = null!;
    private AccountInternalService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _repoMock = new Mock<IAccountRepository>();
        _cache = new ServiceCollection().AddMemoryCache().BuildServiceProvider().GetRequiredService<IMemoryCache>();
        _service = new AccountInternalService(
            _repoMock.Object, _cache,
            new AccountsService.Mapping.AccountResponseMapper(
                new AccountsService.Utils.EncryptionManager(new AccountsService.Config.Settings { PinEncryptionKey = "12345678901234567890123456789012" }),
                new Mock<ILogger<AccountsService.Mapping.AccountResponseMapper>>().Object),
            new Mock<ILogger<AccountInternalService>>().Object,
            new AccountsService.Utils.AccountListCache(), new AccountsService.Utils.PinLockoutService(new Microsoft.Extensions.Caching.Distributed.MemoryDistributedCache(Microsoft.Extensions.Options.Options.Create(new Microsoft.Extensions.Caching.Memory.MemoryDistributedCacheOptions()))));
    }

    private static SavingsAccount ActiveAccount(int number, decimal balance, string pinHash = "hash") =>
        SavingsAccount.RestoreSavings(
            new AccountId(number), "Savings", "Jane Doe", "PREMIUM", pinHash, new Money(balance),
            new Bank("Bank", "Branch", "IFSC"), AccountStatus.ACTIVE, DateTime.UtcNow, null,
            new SavingsDetails("1990-01-01", "F", "123", "enc", "hash"));

    // Have the mocked atomic update actually apply the domain operation, so Debit/Credit
    // invariants (active + sufficient funds) are exercised through the real aggregate.
    private void SetupAtomic(int number, Account account) =>
        _repoMock.Setup(r => r.UpdateBalanceAtomicAsync(number, It.IsAny<Action<Account>>(), It.IsAny<CancellationToken>()))
            .Returns<int, Action<Account>, CancellationToken>((_, apply, _) => { apply(account); return Task.FromResult(account); });

    [TestMethod]
    public async Task Debit_SufficientFunds_ReturnsSuccessWithNewBalance()
    {
        var acct = ActiveAccount(1001, 1000m);
        SetupAtomic(1001, acct);

        var result = await _service.DebitAccountInternalAsync(1001, 400m);

        Assert.IsTrue(result.Success);
        Assert.AreEqual("SUCCESS", result.Status);
        Assert.AreEqual(600m, result.NewBalance);
    }

    [TestMethod]
    public async Task Debit_InsufficientFunds_ReturnsFailed()
    {
        var acct = ActiveAccount(1001, 100m);
        SetupAtomic(1001, acct);

        var result = await _service.DebitAccountInternalAsync(1001, 400m);

        Assert.IsFalse(result.Success);
        Assert.AreEqual("FAILED", result.Status);
        Assert.IsNotNull(result.ErrorCode);
    }

    [TestMethod]
    public async Task Credit_AddsToBalance_ReturnsSuccess()
    {
        var acct = ActiveAccount(1001, 1000m);
        SetupAtomic(1001, acct);

        var result = await _service.CreditAccountInternalAsync(1001, 500m);

        Assert.IsTrue(result.Success);
        Assert.AreEqual(1500m, result.NewBalance);
    }

    [TestMethod]
    public async Task CheckActive_ActiveAccount_ReturnsTrue()
    {
        _repoMock.Setup(r => r.GetByAccountNumberAsync(1001, It.IsAny<CancellationToken>())).ReturnsAsync(ActiveAccount(1001, 1000m));

        var result = await _service.CheckActiveInternalAsync(1001);

        Assert.AreEqual("SUCCESS", result.Status);
        Assert.IsTrue(result.IsActive);
    }

    [TestMethod]
    public async Task CheckActive_MissingAccount_ReturnsFailed()
    {
        _repoMock.Setup(r => r.GetByAccountNumberAsync(9999, It.IsAny<CancellationToken>())).ReturnsAsync((Account?)null);

        var result = await _service.CheckActiveInternalAsync(9999);

        Assert.AreEqual("FAILED", result.Status);
        Assert.IsFalse(result.IsActive);
    }

    [TestMethod]
    public async Task VerifyPin_CorrectPin_ReturnsValid()
    {
        var hash = BCrypt.Net.BCrypt.HashPassword("1234");
        _repoMock.Setup(r => r.GetByAccountNumberAsync(1001, It.IsAny<CancellationToken>())).ReturnsAsync(ActiveAccount(1001, 1000m, hash));

        var result = await _service.VerifyPinInternalAsync(1001, "1234");

        Assert.IsTrue(result.PinValid);
        Assert.AreEqual("SUCCESS", result.Status);
    }

    [TestMethod]
    public async Task VerifyPin_WrongPin_ReturnsFailed()
    {
        var hash = BCrypt.Net.BCrypt.HashPassword("1234");
        _repoMock.Setup(r => r.GetByAccountNumberAsync(1001, It.IsAny<CancellationToken>())).ReturnsAsync(ActiveAccount(1001, 1000m, hash));

        var result = await _service.VerifyPinInternalAsync(1001, "9999");

        Assert.IsFalse(result.PinValid);
        Assert.AreEqual("FAILED", result.Status);
    }
}
