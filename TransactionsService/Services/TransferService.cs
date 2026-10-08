using System.Text.Json;
using TransactionsService.Config;
using TransactionsService.Domain.Exceptions;
using TransactionsService.Domain.Models;
using TransactionsService.Infrastructure.Data;
using TransactionsService.Integration;
using TransactionsService.Utils;
using TransactionsService.DTOs;

namespace TransactionsService.Services;

/// <summary>
/// Manages account-to-account money transfers, employing a Saga pattern for distributed transactions.
/// </summary>
/// <remarks>
/// Architectural Intent: Because AccountsService handles individual balances, a transfer between two
/// accounts requires two distinct HTTP calls (Debit Source, Credit Destination). If the credit fails,
/// this service runs a compensating transaction (Refund Source).
///
/// Money-safety rules enforced here (audit Finding #3):
///  1. The PENDING record is committed BEFORE the first side effect, so a crash always leaves a row
///     the reconciliation worker can find.
///  2. The compensating refund is guarded and retried; if it still fails the transfer is marked
///     COMPENSATION_FAILED (money is in limbo) and surfaced loudly — never swallowed.
///  3. The idempotency key is RELEASED only when the failure happened before any side effect. Once
///     money may have moved, the key is COMPLETED with a failure record so a client retry replays the
///     failure instead of debiting the source a second time.
///  4. The post-credit local writes (COMPLETED status, both ledger legs, key completion) commit as one
///     unit of work, so a partially-recorded successful transfer cannot exist.
/// </remarks>
public class TransferService : ITransferService
{
    private const int RefundAttempts = 3;

    private readonly IUnitOfWork _uow;
    private readonly AccountServiceClient _accountClient;
    private readonly PaymentGatewayClient _paymentGatewayClient;
    private readonly NotificationClient _notificationClient;
    private readonly TransferLimitService _transferLimitService;
    private readonly Settings _settings;
    private readonly ILogger<TransferService> _logger;
    private readonly CacheInvalidator _invalidator;

    public TransferService(
        IUnitOfWork uow,
        AccountServiceClient accountClient,
        PaymentGatewayClient paymentGatewayClient,
        NotificationClient notificationClient,
        TransferLimitService transferLimitService,
        Settings settings,
        ILogger<TransferService> logger,
        CacheInvalidator invalidator)
    {
        _uow = uow;
        _accountClient = accountClient;
        _paymentGatewayClient = paymentGatewayClient;
        _notificationClient = notificationClient;
        _transferLimitService = transferLimitService;
        _settings = settings;
        _logger = logger;
        _invalidator = invalidator;
    }

    /// <summary>
    /// Executes a funds transfer between two accounts.
    /// </summary>
    /// <remarks>
    /// Business Rule: Enforces daily and per-transaction limits based on the source account's privilege level.
    /// External Integration: Calls out to a simulated external Payment Gateway to validate anti-fraud rules before executing.
    /// </remarks>
    public async Task<TransferResultResponse> ProcessTransferAsync(int fromAccount, int toAccount, decimal amount, string pin, TransferMode transferMode, string description, string? idempotencyKey, CancellationToken ct = default)
    {
        if (fromAccount == toAccount)
            throw new InvalidAmountException("Source and destination accounts must be different");

        // 1. Validate Amount
        Validators.ValidateAmount(amount, _settings.MinimumTransferAmount, _settings.MaximumTransactionAmount);

        // 2. Idempotency: replay a finished request, or reserve the key for this one.
        var replayed = await TryReplayAsync(idempotencyKey, ct);
        if (replayed != null)
            return replayed;

        // Becomes true the moment money may have moved. Governs how the idempotency key is settled on failure.
        var sideEffectsStarted = false;

        try
        {
            // 3. Verify Accounts and PIN
            var sourceAccountData = await _accountClient.ValidateAccountAsync(fromAccount, ct);
            await _accountClient.ValidateAccountAsync(toAccount, ct);
            await _accountClient.VerifyPinAsync(fromAccount, pin, ct);

            Validators.ValidateBalance(sourceAccountData.Balance, amount);

            // 4. Verify Limits
            var privilege = await _accountClient.GetAccountPrivilegeAsync(fromAccount, ct);
            var limits = await _transferLimitService.GetLimitAsync(privilege, ct);
            if (limits != null)
            {
                // Many-to-many rule: the tier must be allowed to use this rail.
                if (!limits.Allows(transferMode))
                    throw new TransferModeNotAllowedException(privilege, transferMode.ToString());

                var dailyTotal = await _uow.Transactions.GetDailyTransferTotalAsync(fromAccount, ct);
                Validators.ValidateTransferLimit(dailyTotal, amount, limits.DailyLimit, limits.PerTransactionLimit);
            }

            // 5. Payment Gateway Validation
            var paymentValid = await _paymentGatewayClient.ValidatePaymentAsync(fromAccount, toAccount, amount, transferMode.ToString(), ct);
            if (!paymentValid)
                throw new ServiceUnavailableException("PaymentGateway", "Payment validation failed");

            // 6. Record the transfer as PENDING — committed on its own, BEFORE any money moves.
            var transferRecord = await _uow.Transactions.CreateTransferRecordAsync(fromAccount, toAccount, amount, transferMode, ct);

            // 7. Debit Source (saga start). From here on, a failure must never release the idempotency key.
            sideEffectsStarted = true;
            // CancellationToken.None from here on (debit, credit, compensation, ledger, key completion): side effects already happened; finishing the bookkeeping must not be cancelled.
            var debitResult = await _accountClient.DebitAccountAsync(fromAccount, amount, $"Transfer to {toAccount}", CancellationToken.None);
            var sourceNewBalance = debitResult.NewBalance;

            // 8. Credit Destination, compensating the source if it fails.
            decimal destNewBalance;
            try
            {
                var creditResult = await _accountClient.CreditAccountAsync(toAccount, amount, $"Transfer from {fromAccount}", CancellationToken.None);
                destNewBalance = creditResult.NewBalance;
            }
            catch (Exception creditEx)
            {
                _logger.LogError(creditEx,
                    "Transfer {TransferId}: credit to destination {DestAccount} failed after debiting {SourceAccount}. Starting compensation.",
                    transferRecord.Id, toAccount, fromAccount);

                var refunded = await TryRefundSourceAsync(transferRecord.Id, fromAccount, toAccount, amount);
                if (refunded)
                {
                    await _uow.Transactions.UpdateTransferStatusAsync(transferRecord.Id, TransferStatus.FAILED, creditEx.Message, CancellationToken.None);
                    throw new ServiceUnavailableException("TransferSaga", "Transfer failed during credit. Funds reversed.");
                }

                // Money has left the source and could not be returned. Mark it unmistakably and stop.
                await _uow.Transactions.UpdateTransferStatusAsync(transferRecord.Id, TransferStatus.COMPENSATION_FAILED,
                    $"Credit failed ({creditEx.Message}); automatic refund of {amount} to {fromAccount} failed after {RefundAttempts} attempts.", CancellationToken.None);
                _logger.LogCritical(
                    "Transfer {TransferId}: COMPENSATION FAILED — {Amount} debited from {SourceAccount} was NOT refunded. Requires reconciliation.",
                    transferRecord.Id, amount, fromAccount);
                throw new ServiceUnavailableException("TransferSaga",
                    "Transfer failed and the automatic reversal did not succeed. The transfer is flagged for reconciliation; do not retry with the same Idempotency-Key.");
            }

            // 9-10. Record the outcome atomically: COMPLETED status + both ledger legs + idempotency completion.
            var response = new TransferResultResponse
            {
                Status = "SUCCESS",
                TransactionId = transferRecord.Id,
                FromAccount = fromAccount,
                ToAccount = toAccount,
                Amount = amount
            };

            // Database-only writes: the unit of work may re-run this block after a transient failure.
            await _uow.ExecuteInTransactionAsync(async () =>
            {
                await _uow.Transactions.UpdateTransferStatusAsync(transferRecord.Id, TransferStatus.COMPLETED, ct: CancellationToken.None);
                await _uow.TransactionLogs.LogTransactionAsync(fromAccount, TransactionType.TRANSFER, amount, sourceNewBalance, transferRecord.Id.ToString(), $"Transfer to {toAccount}", CancellationToken.None, transferId: transferRecord.Id);
                await _uow.TransactionLogs.LogTransactionAsync(toAccount, TransactionType.TRANSFER, amount, destNewBalance, transferRecord.Id.ToString(), $"Transfer from {fromAccount}", CancellationToken.None, transferId: transferRecord.Id);
                if (!string.IsNullOrEmpty(idempotencyKey))
                    await _uow.Idempotency.CompleteKeyAsync(idempotencyKey, JsonSerializer.Serialize(response), 201, CancellationToken.None);
            }, CancellationToken.None);

            // 11. Notify (best-effort; the client never throws)
            await _notificationClient.SendNotificationAsync(fromAccount, "TRANSFER_SENT", $"Transfer of {amount} to {toAccount} successful. New balance: {sourceNewBalance}", CancellationToken.None);
            await _notificationClient.SendNotificationAsync(toAccount, "TRANSFER_RECEIVED", $"Transfer of {amount} received from {fromAccount}. New balance: {destNewBalance}", CancellationToken.None);

            // 12. Evict Transaction Logs Cache
            _invalidator.EvictAccount(fromAccount);
            _invalidator.EvictAccount(toAccount);

            return response;
        }
        catch (Exception ex) when (ex is not IdempotencyException)
        {
            await SettleKeyOnFailureAsync(idempotencyKey, sideEffectsStarted, ex);
            throw;
        }
    }

    /// <summary>
    /// Returns the stored response when this Idempotency-Key already finished, throws when it is still
    /// in flight (202) or previously failed after side effects, and returns null after reserving a new key.
    /// </summary>
    private async Task<TransferResultResponse?> TryReplayAsync(string? idempotencyKey, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(idempotencyKey))
            return null;

        var existingKey = await _uow.Idempotency.GetKeyAsync(idempotencyKey, ct);
        if (existingKey == null && !await _uow.Idempotency.TryReserveKeyAsync(idempotencyKey, ct))
            existingKey = await _uow.Idempotency.GetKeyAsync(idempotencyKey, ct); // lost the race — replay whatever won

        if (existingKey == null)
            return null; // reserved for this request

        IdempotencySettlement.ThrowIfNotReplayable(existingKey, "transfer");

        _logger.LogInformation("Idempotency key hit: {Key}", idempotencyKey);
        return JsonSerializer.Deserialize<TransferResultResponse>(existingKey.ResponseBody)!;
    }

    /// <summary>Compensating transaction with bounded retry. Never throws; returns whether the refund landed.</summary>
    private async Task<bool> TryRefundSourceAsync(int transferId, int fromAccount, int toAccount, decimal amount)
    {
        // CancellationToken.None: side effects already happened; finishing the bookkeeping must not be cancelled.
        for (var attempt = 1; attempt <= RefundAttempts; attempt++)
        {
            try
            {
                await _accountClient.CreditAccountAsync(fromAccount, amount, $"Refund for failed transfer to {toAccount}", CancellationToken.None);
                _logger.LogInformation("Transfer {TransferId}: refund of {Amount} to {SourceAccount} succeeded on attempt {Attempt}.",
                    transferId, amount, fromAccount, attempt);
                return true;
            }
            catch (Exception refundEx)
            {
                _logger.LogError(refundEx, "Transfer {TransferId}: refund attempt {Attempt}/{Max} to {SourceAccount} failed.",
                    transferId, attempt, RefundAttempts, fromAccount);
                if (attempt < RefundAttempts)
                    await Task.Delay(TimeSpan.FromMilliseconds(250 * Math.Pow(2, attempt - 1)));
            }
        }
        return false;
    }

    /// <summary>
    /// Before any side effect the key is released so the client may retry; after a side effect it is
    /// completed with a failure record so a retry replays the failure instead of moving money again.
    /// </summary>
    private Task SettleKeyOnFailureAsync(string? idempotencyKey, bool sideEffectsStarted, Exception failure) =>
        IdempotencySettlement.SettleOnFailureAsync(_uow.Idempotency, idempotencyKey, sideEffectsStarted, failure);
}
