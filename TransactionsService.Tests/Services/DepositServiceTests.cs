using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Moq.Protected;
using TransactionsService.Config;
using TransactionsService.Domain.Models;
using TransactionsService.Infrastructure.Data;
using TransactionsService.Infrastructure.Repositories;
using TransactionsService.Integration;
using TransactionsService.Services;
using TransactionsService.Utils;

namespace TransactionsService.Tests.Services;

[TestClass]
/// <summary>
/// Validates the resilient deposit transaction workflow.
/// </summary>
/// <remarks>
/// Architectural Intent: Mocks external HTTP calls (to AccountsService/NotificationService) and 
/// database repositories to prove that the orchestrator correctly handles idempotency locks 
/// and validates amounts without requiring external infrastructure.
/// </remarks>
public class DepositServiceTests
{
    private Mock<IUnitOfWork> _uowMock = null!;
    private Mock<IIdempotencyRepository> _idempotencyMock = null!;
    private Mock<ITransactionLogRepository> _transactionLogMock = null!;
    private Mock<HttpMessageHandler> _httpMessageHandlerMock = null!;
    
    private DepositService _depositService = null!;

    [TestInitialize]
    public void Setup()
    {
        _uowMock = new Mock<IUnitOfWork>();
        _idempotencyMock = new Mock<IIdempotencyRepository>();
        _transactionLogMock = new Mock<ITransactionLogRepository>();
        _uowMock.Setup(u => u.Idempotency).Returns(_idempotencyMock.Object);
        _uowMock.Setup(u => u.TransactionLogs).Returns(_transactionLogMock.Object);

        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        var httpClient = new HttpClient(_httpMessageHandlerMock.Object);

        var settings = new Settings
        {
            InternalApiKey = "key",
            MinimumDepositAmount = 100,
            MaximumTransactionAmount = 100000
        };

        var httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        var serviceProviderMock = new Mock<IServiceProvider>();

        var accountClient = new AccountServiceClient(httpClient, settings, httpContextAccessorMock.Object, serviceProviderMock.Object);
        var notificationClient = new NotificationClient(httpClient, settings, httpContextAccessorMock.Object, serviceProviderMock.Object, new Mock<Microsoft.Extensions.Logging.ILogger<NotificationClient>>().Object);

        var services = new ServiceCollection();
        services.AddMemoryCache();
        var cacheInvalidator = new CacheInvalidator(new Microsoft.Extensions.Caching.Distributed.MemoryDistributedCache(Microsoft.Extensions.Options.Options.Create(new Microsoft.Extensions.Caching.Memory.MemoryDistributedCacheOptions())));

        _depositService = new DepositService(
            _uowMock.Object,
            accountClient,
            notificationClient,
            settings,
            new Mock<ILogger<DepositService>>().Object,
            cacheInvalidator
        );
    }

    private void SetupHttpResponse(string urlContains, HttpStatusCode statusCode, object responseBody)
    {
        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri != null && req.RequestUri.ToString().Contains(urlContains)),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(JsonSerializer.Serialize(responseBody))
            });
    }

    [TestMethod]
    public async Task ProcessDepositAsync_WithValidAmount_SuccessfullyDeposits()
    {
        // Arrange
        var accountNumber = 1001;
        var amount = 500m;
        var newBalance = 1500m;

        // Mock ValidateAccountAsync
        SetupHttpResponse($"/accounts/{accountNumber}", HttpStatusCode.OK, new { is_active = true });
        
        // Mock CreditAccountAsync
        SetupHttpResponse($"/accounts/{accountNumber}/credit", HttpStatusCode.OK, new { new_balance = newBalance });
        
        // Mock NotificationClient
        SetupHttpResponse($"/notifications", HttpStatusCode.OK, new { });

        var log = new DepositLogEntity { Id = 123, AccountId = accountNumber, TransactionType = TransactionType.DEPOSIT, Amount = amount, BalanceAfter = newBalance };
        _transactionLogMock.Setup(r => r.LogTransactionAsync(accountNumber, TransactionType.DEPOSIT, amount, newBalance, null, "Test deposit", It.IsAny<CancellationToken>(), It.IsAny<int?>()))
            .ReturnsAsync(log);

        // Act
        var result = await _depositService.ProcessDepositAsync(accountNumber, amount, "Test deposit", null);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual("SUCCESS", result.Status);
        Assert.AreEqual(amount, result.Amount);
        Assert.AreEqual(newBalance, result.NewBalance);
        _transactionLogMock.Verify(r => r.LogTransactionAsync(accountNumber, TransactionType.DEPOSIT, amount, newBalance, null, "Test deposit", It.IsAny<CancellationToken>(), It.IsAny<int?>()), Times.Once);
    }

    [TestMethod]
    public async Task ProcessDepositAsync_WithIdempotencyKey_ReturnsCachedResponse()
    {
        // Arrange
        var idempotencyKey = "idemp-key-123";
        var expectedResponse = new TransactionsService.DTOs.TransactionResultResponse
        {
            Status = "SUCCESS",
            TransactionId = 123,
            AccountNumber = 1001,
            Amount = 500,
            NewBalance = 1500
        };

        var keyRecord = new IdempotencyKeyEntity
        {
            Key = idempotencyKey,
            StatusCode = 201,
            ResponseBody = JsonSerializer.Serialize(expectedResponse)
        };

        _idempotencyMock.Setup(r => r.GetKeyAsync(idempotencyKey, It.IsAny<CancellationToken>())).ReturnsAsync(keyRecord);

        // Act
        var result = await _depositService.ProcessDepositAsync(1001, 500, "Test deposit", idempotencyKey);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(expectedResponse.TransactionId, result.TransactionId);
        _transactionLogMock.Verify(r => r.LogTransactionAsync(It.IsAny<int>(), It.IsAny<TransactionType>(), It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<int?>()), Times.Never);
    }
}
