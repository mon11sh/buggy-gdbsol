using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore.Storage;
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
/// The transfer saga's money-safety contract:
///  • happy path commits COMPLETED + both ledger legs + the idempotency key as one unit;
///  • credit failure refunds the source and marks FAILED;
///  • refund failure marks COMPENSATION_FAILED and KEEPS the idempotency key (retry must not re-debit);
///  • a failure before any side effect RELEASES the key (retry is safe).
/// </summary>
[TestClass]
public class TransferServiceTests
{
    private const int From = 5001;
    private const int To = 6001;

    private Mock<IUnitOfWork> _uowMock = null!;
    private Mock<ITransactionRepository> _txnRepoMock = null!;
    private Mock<IIdempotencyRepository> _idempotencyMock = null!;
    private Mock<ITransactionLogRepository> _logMock = null!;
    private Mock<ITransferLimitRepository> _limitRepoMock = null!;
    private Mock<HttpMessageHandler> _handlerMock = null!;
    private TransferService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _uowMock = new Mock<IUnitOfWork>();
        _txnRepoMock = new Mock<ITransactionRepository>();
        _idempotencyMock = new Mock<IIdempotencyRepository>();
        _logMock = new Mock<ITransactionLogRepository>();
        _limitRepoMock = new Mock<ITransferLimitRepository>();
        _uowMock.Setup(u => u.Transactions).Returns(_txnRepoMock.Object);
        _uowMock.Setup(u => u.Idempotency).Returns(_idempotencyMock.Object);
        _uowMock.Setup(u => u.TransactionLogs).Returns(_logMock.Object);
        _uowMock.Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>())).Returns<Func<Task>, CancellationToken>((work, _) => work());

        _handlerMock = new Mock<HttpMessageHandler>();
        var httpClient = new HttpClient(_handlerMock.Object);

        var settings = new Settings
        {
            InternalApiKey = "key",
            MinimumTransferAmount = 1,
            MaximumTransactionAmount = 1000000,
        };

        var ctxAccessor = new Mock<IHttpContextAccessor>();
        var sp = new Mock<IServiceProvider>();
        var accountClient = new AccountServiceClient(httpClient, settings, ctxAccessor.Object, sp.Object);
        var paymentClient = new PaymentGatewayClient(httpClient, settings, ctxAccessor.Object, sp.Object);
        var notificationClient = new NotificationClient(httpClient, settings, ctxAccessor.Object, sp.Object, new Mock<Microsoft.Extensions.Logging.ILogger<NotificationClient>>().Object);
        var limitService = new TransferLimitService(_limitRepoMock.Object, accountClient, new Mock<ILogger<TransferLimitService>>().Object);

        _service = new TransferService(
            _uowMock.Object, accountClient, paymentClient, notificationClient, limitService,
            settings, new Mock<ILogger<TransferService>>().Object, new CacheInvalidator(new Microsoft.Extensions.Caching.Distributed.MemoryDistributedCache(Microsoft.Extensions.Options.Options.Create(new Microsoft.Extensions.Caching.Memory.MemoryDistributedCacheOptions()))));

        _limitRepoMock.Setup(r => r.GetLimitByPrivilegeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((TransferLimitEntity?)null);
        _txnRepoMock.Setup(r => r.CreateTransferRecordAsync(From, To, 1000m, TransferMode.NEFT, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FundTransferEntity { Id = 999, SourceAccountId = From, DestinationAccountId = To });
    }

    // Register general matchers first, specific ones after (Moq's last matching setup wins).
    private void SetupHttp(string urlContains, HttpStatusCode status, object body) =>
        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri != null && r.RequestUri.ToString().Contains(urlContains)),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => new HttpResponseMessage { StatusCode = status, Content = new StringContent(JsonSerializer.Serialize(body)) });

    /// <summary>Everything up to (not including) the credit/refund legs succeeds.</summary>
    private void ArrangeHappyPreconditions(bool pinValid = true)
    {
        SetupHttp($"/accounts/{From}", HttpStatusCode.OK, new { is_active = true, balance = 5000m }); // validate source
        SetupHttp($"/accounts/{To}", HttpStatusCode.OK, new { is_active = true });                    // validate dest
        SetupHttp("verify-pin", HttpStatusCode.OK, new { pin_valid = pinValid });
        SetupHttp("/privilege", HttpStatusCode.OK, new { privilege = "GOLD" });
        SetupHttp("/payment/process", HttpStatusCode.OK, new { success = true });
        SetupHttp($"{From}/debit", HttpStatusCode.OK, new { new_balance = 4000m });                    // debit source OK
    }

    private void VerifySent(string urlContains, Times times) =>
        _handlerMock.Protected().Verify("SendAsync", times,
            ItExpr.Is<HttpRequestMessage>(r => r.RequestUri!.ToString().Contains(urlContains)),
            ItExpr.IsAny<CancellationToken>());

    [TestMethod]
    public async Task ProcessTransferAsync_HappyPath_CommitsStatusLegsAndKeyAtomically()
    {
        ArrangeHappyPreconditions();
        SetupHttp($"{To}/credit", HttpStatusCode.OK, new { new_balance = 3000m });
        SetupHttp("notify", HttpStatusCode.OK, new { });
        _idempotencyMock.Setup(i => i.GetKeyAsync("k-ok", It.IsAny<CancellationToken>())).ReturnsAsync((IdempotencyKeyEntity?)null);
        _idempotencyMock.Setup(i => i.TryReserveKeyAsync("k-ok", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await _service.ProcessTransferAsync(From, To, 1000m, "1234", TransferMode.NEFT, "rent", "k-ok");

        Assert.AreEqual("SUCCESS", result.Status);
        Assert.AreEqual(999, result.TransactionId);
        VerifySent($"{To}/credit", Times.Once());
        VerifySent($"{From}/credit", Times.Never()); // no refund on success
        _txnRepoMock.Verify(r => r.UpdateTransferStatusAsync(999, TransferStatus.COMPLETED, It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        _logMock.Verify(l => l.LogTransactionAsync(From, TransactionType.TRANSFER, 1000m, 4000m, "999", It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<int?>()), Times.Once);
        _logMock.Verify(l => l.LogTransactionAsync(To, TransactionType.TRANSFER, 1000m, 3000m, "999", It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<int?>()), Times.Once);
        _idempotencyMock.Verify(i => i.CompleteKeyAsync("k-ok", It.IsAny<string>(), 201, It.IsAny<CancellationToken>()), Times.Once);
        _idempotencyMock.Verify(i => i.ReleaseKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _uowMock.Verify(u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task ProcessTransferAsync_CreditToDestinationFails_RefundsSourceAndMarksFailed()
    {
        ArrangeHappyPreconditions();
        SetupHttp($"{To}/credit", HttpStatusCode.InternalServerError, new { });   // credit dest FAILS
        SetupHttp($"{From}/credit", HttpStatusCode.OK, new { new_balance = 5000m }); // refund source OK

        await Assert.ThrowsExactlyAsync<ServiceUnavailableException>(
            () => _service.ProcessTransferAsync(From, To, 1000m, "1234", TransferMode.NEFT, "rent", null));

        VerifySent($"{From}/credit", Times.Once());
        _txnRepoMock.Verify(r => r.UpdateTransferStatusAsync(999, TransferStatus.FAILED, It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        _txnRepoMock.Verify(r => r.UpdateTransferStatusAsync(It.IsAny<int>(), TransferStatus.COMPLETED, It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task ProcessTransferAsync_RefundAlsoFails_MarksCompensationFailed_AndKeepsIdempotencyKey()
    {
        ArrangeHappyPreconditions();
        SetupHttp($"{To}/credit", HttpStatusCode.InternalServerError, new { });   // credit dest FAILS
        SetupHttp($"{From}/credit", HttpStatusCode.InternalServerError, new { }); // refund FAILS too
        _idempotencyMock.Setup(i => i.GetKeyAsync("k-limbo", It.IsAny<CancellationToken>())).ReturnsAsync((IdempotencyKeyEntity?)null);
        _idempotencyMock.Setup(i => i.TryReserveKeyAsync("k-limbo", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await Assert.ThrowsExactlyAsync<ServiceUnavailableException>(
            () => _service.ProcessTransferAsync(From, To, 1000m, "1234", TransferMode.NEFT, "rent", "k-limbo"));

        // Refund was genuinely attempted (bounded retry), then the transfer was flagged, not silently dropped.
        VerifySent($"{From}/credit", Times.Exactly(3));
        _txnRepoMock.Verify(r => r.UpdateTransferStatusAsync(999, TransferStatus.COMPENSATION_FAILED, It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        _txnRepoMock.Verify(r => r.UpdateTransferStatusAsync(999, TransferStatus.FAILED, It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        // The key must survive as a failure record so a retry cannot debit the source again.
        _idempotencyMock.Verify(i => i.ReleaseKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _idempotencyMock.Verify(i => i.CompleteKeyAsync("k-limbo", It.IsAny<string>(), 503, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task ProcessTransferAsync_FailsBeforeAnySideEffect_ReleasesIdempotencyKey()
    {
        ArrangeHappyPreconditions(pinValid: false); // wrong PIN: rejected before the PENDING record / debit
        _idempotencyMock.Setup(i => i.GetKeyAsync("k-early", It.IsAny<CancellationToken>())).ReturnsAsync((IdempotencyKeyEntity?)null);
        _idempotencyMock.Setup(i => i.TryReserveKeyAsync("k-early", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await Assert.ThrowsExactlyAsync<InvalidPinException>(
            () => _service.ProcessTransferAsync(From, To, 1000m, "0000", TransferMode.NEFT, "rent", "k-early"));

        VerifySent($"{From}/debit", Times.Never());
        _txnRepoMock.Verify(r => r.CreateTransferRecordAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<TransferMode>(), It.IsAny<CancellationToken>()), Times.Never);
        _idempotencyMock.Verify(i => i.ReleaseKeyAsync("k-early", It.IsAny<CancellationToken>()), Times.Once);
        _idempotencyMock.Verify(i => i.CompleteKeyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task ProcessTransferAsync_ReplayOfFailedKey_RefusesToReExecute()
    {
        _idempotencyMock.Setup(i => i.GetKeyAsync("k-failed", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IdempotencyKeyEntity { Key = "k-failed", StatusCode = 503, ResponseBody = "{\"status\":\"FAILED\"}" });

        await Assert.ThrowsExactlyAsync<ServiceUnavailableException>(
            () => _service.ProcessTransferAsync(From, To, 1000m, "1234", TransferMode.NEFT, "rent", "k-failed"));

        VerifySent($"{From}/debit", Times.Never());
        _txnRepoMock.Verify(r => r.CreateTransferRecordAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<TransferMode>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
