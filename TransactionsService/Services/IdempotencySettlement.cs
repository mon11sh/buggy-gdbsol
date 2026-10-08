using System.Text.Json;
using TransactionsService.Domain.Exceptions;
using TransactionsService.Infrastructure.Data;
using TransactionsService.Infrastructure.Repositories;

namespace TransactionsService.Services;

/// <summary>
/// One idempotency failure policy shared by every money operation (deposit, withdraw, transfer).
/// </summary>
/// <remarks>
/// A key may be RELEASED (letting the client retry) only when the failure happened before any side
/// effect. Once money may have moved — including an ambiguous network failure on the money call — the
/// key is COMPLETED with a failure record so a retry with the same key replays the failure instead of
/// moving money a second time.
/// </remarks>
public static class IdempotencySettlement
{
    public const int ReplayedFailureStatusCode = 503;

    public static async Task SettleOnFailureAsync(IIdempotencyRepository idempotency, string? idempotencyKey, bool sideEffectsStarted, Exception failure)
    {
        // CancellationToken.None: side effects already happened; finishing the bookkeeping must not be cancelled (and the request token may itself be the reason we are here).
        if (string.IsNullOrEmpty(idempotencyKey))
            return;

        if (!sideEffectsStarted)
        {
            await idempotency.ReleaseKeyAsync(idempotencyKey, CancellationToken.None);
            return;
        }

        var failureRecord = JsonSerializer.Serialize(new { status = "FAILED", message = failure.Message });
        await idempotency.CompleteKeyAsync(idempotencyKey, failureRecord, ReplayedFailureStatusCode, CancellationToken.None);
    }

    /// <summary>
    /// Guards a stored key before it is replayed as a success: throws for in-flight (202) and for
    /// previously failed-after-side-effect (>= 400) records.
    /// </summary>
    public static void ThrowIfNotReplayable(IdempotencyKeyEntity existingKey, string operation)
    {
        if (existingKey.StatusCode == 202)
            throw new IdempotencyException($"A {operation} with this Idempotency-Key is already being processed");

        if (existingKey.StatusCode >= 400)
            throw new ServiceUnavailableException("Idempotency",
                $"A {operation} with this Idempotency-Key already failed after funds movement began and is flagged for reconciliation. Use a new key for a new {operation}.");
    }
}
