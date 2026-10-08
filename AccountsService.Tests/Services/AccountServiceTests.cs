using AccountsService.Domain.Exceptions;
using AccountsService.Domain.Models;
using AccountsService.DTOs;
using AccountsService.Infrastructure.Repositories;
using AccountsService.Integration;
using AccountsService.Services;
using AccountsService.Utils;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace AccountsService.Tests.Services;

[TestClass]
/// <summary>
/// Verifies the core Account creation and state-transition logic in isolation.
/// </summary>
/// <remarks>
/// Architectural Intent: This suite strictly tests the AccountService business rules without hitting a real database 
/// or real external dependencies (like AadharService), relying purely on mock objects. 
/// This guarantees the MSTest unit tests remain perfectly isolated from infrastructure availability.
/// </remarks>
public class AccountServiceTests
{
    private Mock<IAccountRepository> _repositoryMock = null!;
    private Mock<IAadharClient> _aadharClientMock = null!;
    private Mock<ICompanyClient> _companyClientMock = null!;
    private Mock<INotificationClient> _notificationClientMock = null!;
    private EncryptionManager _encryptionManager = null!;
    private AccountService _accountService = null!;

    [TestInitialize]
    public void Setup()
    {
        _repositoryMock = new Mock<IAccountRepository>();
        _aadharClientMock = new Mock<IAadharClient>();
        _companyClientMock = new Mock<ICompanyClient>();
        _notificationClientMock = new Mock<INotificationClient>();

        var services = new ServiceCollection();
        services.AddMemoryCache();
        var serviceProvider = services.BuildServiceProvider();
        var cache = serviceProvider.GetRequiredService<IMemoryCache>();

        _encryptionManager = new EncryptionManager(new AccountsService.Config.Settings { PinEncryptionKey = "12345678901234567890123456789012" });
        
        _accountService = new AccountService(
            _repositoryMock.Object,
            _aadharClientMock.Object,
            _companyClientMock.Object,
            _notificationClientMock.Object,
            _encryptionManager,
            new Mock<ILogger<AccountService>>().Object,
            cache,
            new AccountsService.Utils.AccountListCache(),
            new AccountsService.Utils.PinLockoutService(new Microsoft.Extensions.Caching.Distributed.MemoryDistributedCache(Microsoft.Extensions.Options.Options.Create(new Microsoft.Extensions.Caching.Memory.MemoryDistributedCacheOptions())))
        );
    }

    [TestMethod]
    public async Task CreateSavingsAccountAsync_WithValidDetails_ReturnsAccount()
    {
        var request = new SavingsAccountCreate
        {
            Name = "John Doe",
            AadharNumber = "123456789012",
            Pin = "4829",
            PhoneNo = "9876543210",
            DateOfBirth = "1990-01-01",
            Gender = "M",
            Privilege = "PREMIUM",
            InitialBalance = 2500
        };

        _repositoryMock.Setup(r => r.GetByAadharHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Account?)null);

        var verifyResult = new Dictionary<string, object>
        {
            { "is_valid", JsonSerializer.Deserialize<JsonElement>("true") }
        };
        _aadharClientMock.Setup(a => a.VerifyAadharAsync(request.AadharNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(verifyResult);

        _repositoryMock.Setup(r => r.SaveAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()))
            .Callback<Account, CancellationToken>((a, _) => a.AssignNumber(new AccountId(1001)))
            .ReturnsAsync((Account a, CancellationToken _) => a);

        var result = await _accountService.CreateSavingsAccountAsync(request);

        Assert.IsNotNull(result);
        Assert.AreEqual(request.Name, result.Name);
        Assert.AreEqual(request.InitialBalance, result.Balance.Amount);
        _repositoryMock.Verify(r => r.SaveAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task ActivateAccountAsync_WithValidAccount_ActivatesSuccessfully()
    {
        var account = SavingsAccount.RestoreSavings(new AccountId(1001), "Savings", "Jane Doe", "PREMIUM", "hash", new Money(100), new Bank("Bank", "Branch", "IFSC"), AccountsService.Domain.Enums.AccountStatus.ACTIVE, DateTime.UtcNow, null, new SavingsDetails("1990-01-01", "F", "123", "enc", "hash"));
        account.Inactivate();

        _repositoryMock.Setup(r => r.GetByAccountNumberAsync(1001, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var result = await _accountService.ActivateAccountAsync(1001);

        Assert.IsTrue(result);
        Assert.AreEqual(AccountsService.Domain.Enums.AccountStatus.ACTIVE, account.Status);
        _repositoryMock.Verify(r => r.SaveAsync(account, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task CloseAccountAsync_WithValidAccount_ClosesSuccessfully()
    {
        var account = SavingsAccount.RestoreSavings(new AccountId(1001), "Savings", "Jane Doe", "PREMIUM", "hash", new Money(100), new Bank("Bank", "Branch", "IFSC"), AccountsService.Domain.Enums.AccountStatus.ACTIVE, DateTime.UtcNow, null, new SavingsDetails("1990-01-01", "F", "123", "enc", "hash"));

        _repositoryMock.Setup(r => r.GetByAccountNumberAsync(1001, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var result = await _accountService.CloseAccountAsync(1001);

        Assert.IsTrue(result);
        Assert.AreEqual(AccountsService.Domain.Enums.AccountStatus.CLOSED, account.Status);
        Assert.IsNotNull(account.StatusUpdatedDate);
        _repositoryMock.Verify(r => r.SaveAsync(account, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task CreateSavingsAccountAsync_WithInvalidAadhar_ThrowsValidationError()
    {
        var request = new SavingsAccountCreate
        {
            Name = "John Doe",
            AadharNumber = "invalid",
            Pin = "4829",
            PhoneNo = "9876543210",
            DateOfBirth = "1990-01-01",
            Gender = "M",
            Privilege = "PREMIUM",
            InitialBalance = 1000
        };

        _repositoryMock.Setup(r => r.GetByAadharHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Account?)null);

        var verifyResult = new Dictionary<string, object>
        {
            { "is_valid", JsonSerializer.Deserialize<JsonElement>("false") },
            { "message", "Invalid Aadhar" }
        };
        _aadharClientMock.Setup(a => a.VerifyAadharAsync(request.AadharNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(verifyResult);

        try
        {
            await _accountService.CreateSavingsAccountAsync(request);
            Assert.Fail("Expected ValidationError was not thrown.");
        }
        catch (ValidationError)
        {
            // Passed
        }
    }
}
