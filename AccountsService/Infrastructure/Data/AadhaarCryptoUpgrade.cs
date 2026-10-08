using Microsoft.EntityFrameworkCore;
using AccountsService.Utils;

namespace AccountsService.Infrastructure.Data;

/// <summary>
/// One-time, idempotent startup pass that moves every stored Aadhaar from the pre-upgrade format
/// (AES-CBC or plaintext, blind index under the shared legacy key) to the current one (AES-GCM,
/// blind index under the dedicated HKDF-derived key). Rows already in the current format are skipped,
/// so re-running it costs one indexed query. A row that cannot be decrypted is logged and left
/// untouched — it is never overwritten with garbage.
/// </summary>
/// <remarks>
/// Runs through EF Core regardless of the DataAccess toggle (the DbContext is always configured),
/// so it upgrades the same database the ADO.NET path reads.
/// CONCEPT: transactions + savepoints — each batch is one transaction; each row gets its own
/// savepoint, so a row that fails mid-write is rolled back ALONE while the rest of the batch commits.
/// </remarks>
public static class AadhaarCryptoUpgrade
{
    private const int BatchSize = 200;

    public static async Task<(int Upgraded, int Skipped)> RunAsync(AppDbContext db, EncryptionManager crypto, ILogger logger, CancellationToken ct = default)
    {
        var prefix = EncryptionManager.CurrentFormatPrefix;
        int upgraded = 0, skipped = 0;
        var skippedIds = new HashSet<int>(); // rows left as-is must not be fetched (and counted) again

        // A retrying execution strategy (EnableRetryOnFailure) requires user transactions to run inside it.
        var strategy = db.Database.CreateExecutionStrategy();

        while (!ct.IsCancellationRequested)
        {
            // Legacy rows are exactly those without the version prefix (the seed and all new writes carry it).
            var batch = await db.SavingsAccountDetails
                .Where(d => !d.AadharNumber.StartsWith(prefix) && !skippedIds.Contains(d.Id))
                .OrderBy(d => d.Id)
                .Take(BatchSize)
                .ToListAsync(ct);

            if (batch.Count == 0)
                break;

            var progressed = false;
            await strategy.ExecuteAsync(async () =>
            {
                // The in-memory provider has no transactions; everything else gets one per batch.
                var relational = db.Database.IsRelational();
                await using var tx = relational ? await db.Database.BeginTransactionAsync(ct) : null;
                var savepoints = tx?.SupportsSavepoints == true;

                foreach (var row in batch)
                {
                    if (!crypto.TryDecrypt(row.AadharNumber, out var plaintext))
                    {
                        skipped++; skippedIds.Add(row.Id);
                        logger.LogError("Aadhaar upgrade: account {AccountNumber} holds an undecryptable value; left unchanged for manual review.", row.AccountNumber);
                        continue;
                    }

                    if (savepoints) await tx!.CreateSavepointAsync("row", ct);
                    try
                    {
                        row.AadharNumber = crypto.EncryptData(plaintext);
                        row.AadharHash = crypto.GenerateBlindIndex(plaintext);
                        row.UpdatedAt = DateTime.UtcNow;
                        await db.SaveChangesAsync(ct);
                        upgraded++;
                        progressed = true;
                    }
                    catch (DbUpdateException ex)
                    {
                        // e.g. a blind-index collision with an already-upgraded duplicate: undo THIS row only.
                        if (savepoints) await tx!.RollbackToSavepointAsync("row", ct);
                        db.Entry(row).State = EntityState.Detached;
                        skipped++; skippedIds.Add(row.Id);
                        logger.LogError(ex, "Aadhaar upgrade: account {AccountNumber} could not be written; rolled back to its savepoint.", row.AccountNumber);
                    }
                }

                if (tx is not null) await tx.CommitAsync(ct);
            });

            // Every remaining legacy row is undecryptable: stop rather than spin on the same batch forever.
            if (!progressed)
                break;
        }

        if (upgraded > 0 || skipped > 0)
            logger.LogWarning("Aadhaar crypto upgrade: {Upgraded} row(s) re-encrypted and re-indexed, {Skipped} skipped.", upgraded, skipped);
        else
            logger.LogInformation("Aadhaar crypto upgrade: nothing to do (all rows already in the current format).");

        return (upgraded, skipped);
    }
}
