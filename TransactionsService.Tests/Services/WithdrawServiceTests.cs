using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Moq.Protected;
using TransactionsService.Config;
using TransactionsService.Domain.Exceptions;
using TransactionsService.Domain.Models;
using TransactionsService.Infrastructure.Data;
using TransactionsService.Infrastructure.Repositories;
using TransactionsService.Integration;
using TransactionsService.Services;
using TransactionsService.Utils;

namespace TransactionsService.Tests.Services;

/// <summary>
/// Covers the previously-untested withdrawal money path: PIN verification + balance debit
/// (happy path), and the security-critical wrong-PIN rejection (no debit occurs).
/// </summary>
[TestClass]
public class WithdrawServiceTests
{
    private Mock<IUnitOfWork> _uowMock = null!;
    private Mock<IIdempotencyRepository> _idempotencyMock = null!;
    private Mock<ITransactionLogRepository> _logMock = null!;
    private Mock<HttpMessageHandler> _handlerMock = null!;
    private WithdrawService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _uowMock = new Mock<IUnitOfWork>();
        _idempotencyMock = new Mock<IIdempotencyRepository>();
        _logMock = new Mock<ITransactionLogRepository>();
        _uowMock.Setup(u => u.Idempotency).Returns(_idempotencyMock.Object);
        _uowMock.Setup(u => u.TransactionLogs).Returns(_logMock.Object);

        _handlerMock = new Mock<HttpMessageHandler>();
        var httpClient = new HttpClient(_handlerMock.Object);

        var settings = new Settings
        {
            InternalApiKey = "key",
            MinimumWithdrawalAmount = 100,
            MaximumTransactionAmount = 100000,
        };

        var ctxAccessor = new Mock<IHttpContextAccessor>();
        var sp = new Mock<IServiceProvider>();
        var accountClient = new AccountServiceClient(httpClient, settings, ctxAccessor.Object, sp.Object);
        var notificationClient = new NotificationClient(httpClient, settings, ctxAccessor.Object, sp.Object, new Mock<Microsoft.Extensions.Logging.ILogger<NotificationClient>>().Object);

        _service = new WithdrawService(
            _uowMock.Object,
            accountClient,
            notificationClient,
            settings,
            new Mock<ILogger<WithdrawService>>().Object,
            new CacheInvalidator(new Microsoft.Extensions.Caching.Distributed.MemoryDistributedCache(Microsoft.Extensions.Options.Options.Create(new Microsoft.Extensions.Caching.Memory.MemoryDistributedCacheOptions()))));
    }

    // Register general matcher first, specific suffixes after — Moq's last matching setup wins.
    private void SetupHttp(string urlContains, HttpStatusCode status, object body) =>
        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri != null && r.RequestUri.ToString().Contains(urlContains)),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = status, Content = new StringContent(JsonSerializer.Serialize(body)) });

    [TestMethod]
    public async Task ProcessWithdrawAsync_ValidPinAndFunds_Debits()
    {
        const int acct = 5001;
        SetupHttp($"/internal/accounts/{acct}", HttpStatusCode.OK, new { is_active = true, balance = 1000m });
        SetupHttp("verify-pin", HttpStatusCode.OK, new { pin_valid = true });
        SetupHttp("/debit", HttpStatusCode.OK, new { new_balance = 600m });
        SetupHttp("/notifications", HttpStatusCode.OK, new { });

        _logMock.Setup(r => r.LogTransactionAsync(acct, TransactionType.WITHDRAWAL, 400m, 600m, null, "atm", It.IsAny<CancellationToken>(), It.IsAny<int?>()))
            .ReturnsAsync(new WithdrawalLogEntity { Id = 555, AccountId = acct, TransactionType = TransactionType.WITHDRAWAL, Amount = 400m, BalanceAfter = 600m });

        var result = await _service.ProcessWithdrawAsync(acct, 400m, "1234", "atm", null);

        Assert.AreEqual("SUCCESS", result.Status);
        Assert.AreEqual(600m, result.NewBalance);
        _logMock.Verify(r => r.LogTransactionAsync(acct, TransactionType.WITHDRAWAL, 400m, 600m, null, "atm", It.IsAny<CancellationToken>(), It.IsAny<int?>()), Times.Once);
    }

    [TestMethod]
    public async Task ProcessWithdrawAsync_WrongPin_ThrowsAndNeverDebits()
    {
        const int acct = 5002;
        SetupHttp($"/internal/accounts/{acct}", HttpStatusCode.OK, new { is_active = true, balance = 1000m });
        SetupHttp("verify-pin", HttpStatusCode.Unauthorized, new { });          // wrong PIN
        SetupHttp("/debit", HttpStatusCode.OK, new { new_balance = 600m });      // must NOT be reached

        await Assert.ThrowsExactlyAsync<InvalidPinException>(
            () => _service.ProcessWithdrawAsync(acct, 400m, "9999", "atm", null));

        // Debit endpoint must never have been called on a wrong PIN.
        _handlerMock.Protected().Verify("SendAsync", Times.Never(),
            ItExpr.Is<HttpRequestMessage>(r => r.RequestUri!.ToString().Contains("/debit")),
            ItExpr.IsAny<CancellationToken>());
    }
}
