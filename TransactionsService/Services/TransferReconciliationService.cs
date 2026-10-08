using TransactionsService.Config;
using TransactionsService.Domain.Models;
using TransactionsService.Integration;

namespace TransactionsService.Services;

/// <summary>
/// Background reconciliation for the transfer saga — the safety net the saga itself cannot be.
/// </summary>
/// <remarks>
/// Every <see cref="Settings.ReconciliationIntervalSeconds"/> it:
///  • re-attempts the refund for transfers marked COMPENSATION_FAILED and, on success, closes them as FAILED;
///  • reports transfers stuck in PENDING longer than <see cref="Settings.StaleTransferMinutes"/> (a crash
///    between debit and credit) so operations can reconcile them against the account ledger — it does
///    NOT guess whether the debit happened, because guessing wrong would move money;
///  • releases idempotency reservations (202) older than <see cref="Settings.IdempotencyReservationTtlMinutes"/>
///    so a client whose original request died is no longer blocked from retrying forever.
/// Failures inside a sweep are logged and the loop continues; shutdown cancels cleanly.
/// </remarks>
public sealed class TransferReconciliationService : BackgroundService
{
    private const int BatchSize = 100;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Settings _settings;
    private readonly ILogger<TransferReconciliationService> _logger;

    public TransferReconciliationService(IServiceScopeFactory scopeFactory, Settings settings, ILogger<TransferReconciliationService> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = settings;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(5, _settings.ReconciliationIntervalSeconds));
        _logger.LogInformation("Transfer reconciliation running every {Interval}s (stale after {Stale}m, reservation TTL {Ttl}m).",
            interval.TotalSeconds, _settings.StaleTransferMinutes, _settings.IdempotencyReservationTtlMinutes);

        using var timer = new PeriodicTimer(interval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await SweepAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Transfer reconciliation sweep failed; will retry on the next tick.");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
    }

    private async Task SweepAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var accounts = scope.ServiceProvider.GetRequiredService<AccountServiceClient>();
        var now = DateTime.UtcNow;

        // 1. Money in limbo: retry the refund.
        var compensationFailed = await uow.Transactions.GetStaleTransfersAsync(TransferStatus.COMPENSATION_FAILED, now, BatchSize, ct);
        foreach (var transfer in compensationFailed)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                // CancellationToken.None for the refund + FAILED mark: once the credit is sent, the status must land or the next sweep would refund again.
                await accounts.CreditAccountAsync(transfer.SourceAccountId, transfer.Amount, $"Reconciliation refund for failed transfer {transfer.Id}", CancellationToken.None);
                await uow.Transactions.UpdateTransferStatusAsync(transfer.Id, TransferStatus.FAILED,
                    $"{transfer.FailureReason} | Refunded by reconciliation at {now:O}.", CancellationToken.None);
                _logger.LogWarning("Reconciliation refunded transfer {TransferId}: {Amount} returned to {SourceAccount}.",
                    transfer.Id, transfer.Amount, transfer.SourceAccountId);
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex,
                    "Reconciliation could NOT refund transfer {TransferId} ({Amount} from {SourceAccount}); still COMPENSATION_FAILED. Manual action required.",
                    transfer.Id, transfer.Amount, transfer.SourceAccountId);
            }
        }

        // 2. Stuck PENDING: report, never guess.
        var staleCutoff = now.AddMinutes(-Math.Max(1, _settings.StaleTransferMinutes));
        var stuck = await uow.Transactions.GetStaleTransfersAsync(TransferStatus.PENDING, staleCutoff, BatchSize, ct);
        foreach (var transfer in stuck)
        {
            // Explicit load: how far did the bookkeeping get? 0 legs = died before/at the credit; 2 legs = died before COMPLETED landed.
            await uow.Transactions.LoadLegsAsync(transfer, ct);
            _logger.LogCritical(
                "Transfer {TransferId} has been PENDING since {CreatedAt:O} ({Amount} from {SourceAccount} to {DestAccount}); {Legs} ledger leg(s) written. " +
                "The process likely died between debit and credit - reconcile against the account ledger.",
                transfer.Id, transfer.CreatedAt, transfer.Amount, transfer.SourceAccountId, transfer.DestinationAccountId, transfer.Legs.Count);
        }

        // 3. Orphaned reservations.
        var reservationCutoff = now.AddMinutes(-Math.Max(1, _settings.IdempotencyReservationTtlMinutes));
        var released = await uow.Idempotency.ReleaseStaleReservationsAsync(reservationCutoff, ct);
        if (released > 0)
            _logger.LogWarning("Released {Count} orphaned idempotency reservation(s) older than {Cutoff:O}.", released, reservationCutoff);
    }
}
