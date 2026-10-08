using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TransactionsService.Domain.Models;
using TransactionsService.Infrastructure.Data;
using TransactionsService.Infrastructure.Repositories;

namespace TransactionsService.Tests.Infrastructure;

/// <summary>
/// Relational-only EF Core behaviour on a real provider (SQLite in-memory): TPH inheritance, the
/// one-to-many transfer→legs relationship (eager/split, lazy, explicit loading), the many-to-many
/// tier→modes relationship, Restrict delete semantics, model seeding (HasData) and bulk delete.
/// </summary>
[TestClass]
public class EfCoreRelationshipTests
{
    private static AppDbContext NewContext(SqliteConnection conn) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options);

    private static async Task<(SqliteConnection Conn, int TransferId)> SeedAsync()
    {
        var conn = new SqliteConnection("DataSource=:memory:");
        await conn.OpenAsync();
        using var db = NewContext(conn);
        await db.Database.EnsureCreatedAsync();

        var transfer = new FundTransferEntity { SourceAccountId = 1000, DestinationAccountId = 1001, Amount = 500m, TransferMode = TransferMode.NEFT, Status = "COMPLETED" };
        db.FundTransfers.Add(transfer);
        await db.SaveChangesAsync();

        db.TransactionLogs.AddRange(
            new TransferLegEntity { AccountId = 1000, Amount = 500m, BalanceAfter = 9500m, TransferId = transfer.Id },
            new TransferLegEntity { AccountId = 1001, Amount = 500m, BalanceAfter = 10500m, TransferId = transfer.Id },
            new DepositLogEntity { AccountId = 1000, Amount = 100m, BalanceAfter = 9600m });
        await db.SaveChangesAsync();
        return (conn, transfer.Id);
    }

    [TestMethod]
    public async Task HasData_seeds_limits_modes_and_the_join_rows()
    {
        var (conn, _) = await SeedAsync();
        using (conn)
        {
            using var db = NewContext(conn);
            var limits = await db.TransferLimits.Include(l => l.AllowedModes).AsNoTracking().OrderBy(l => l.Privilege).ToListAsync();
            CollectionAssert.AreEqual(new[] { "GOLD", "PREMIUM", "SILVER" }, limits.Select(l => l.Privilege).ToArray());

            // Many-to-many: SILVER may not use RTGS, PREMIUM may use every rail.
            var silver = limits.Single(l => l.Privilege == "SILVER");
            var premium = limits.Single(l => l.Privilege == "PREMIUM");
            Assert.IsFalse(silver.Allows(TransferMode.RTGS));
            Assert.IsTrue(silver.Allows(TransferMode.NEFT));
            Assert.IsTrue(premium.Allows(TransferMode.RTGS));
            Assert.HasCount(4, await db.TransferModes.ToListAsync());

            // ...and from the other side of the relationship.
            var rtgs = await db.TransferModes.Include(m => m.Limits).SingleAsync(m => m.Mode == "RTGS");
            CollectionAssert.AreEquivalent(new[] { "GOLD", "PREMIUM" }, rtgs.Limits.Select(l => l.Privilege).ToArray());
        }
    }

    [TestMethod]
    public async Task TPH_materialises_the_subtype_from_the_discriminator_column()
    {
        var (conn, _) = await SeedAsync();
        using (conn)
        {
            using var db = NewContext(conn);
            var rows = await db.TransactionLogs.AsNoTracking().OrderBy(l => l.Id).ToListAsync();

            Assert.HasCount(3, rows);
            Assert.HasCount(2, rows.OfType<TransferLegEntity>().ToList(), "materialised as the leg subtype from transaction_type = TRANSFER");
            Assert.HasCount(1, rows.OfType<DepositLogEntity>().ToList(), "materialised as the deposit subtype from transaction_type = DEPOSIT");
            Assert.IsTrue(rows.OfType<TransferLegEntity>().All(l => l.TransferId.HasValue), "subtype-only column is populated");
            Assert.AreEqual(1, await db.TransactionLogs.OfType<DepositLogEntity>().CountAsync(), "OfType filters on the discriminator");
            Assert.AreEqual(2, await db.Set<TransferLegEntity>().CountAsync());
        }
    }

    [TestMethod]
    public async Task Eager_lazy_and_explicit_loading_all_return_the_two_legs()
    {
        var (conn, transferId) = await SeedAsync();
        using (conn)
        {
            using var db = NewContext(conn);
            var repo = new TransactionRepository(db);

            var eager = await repo.GetTransferWithLegsAsync(transferId);
            Assert.HasCount(2, eager!.Legs, "Include + AsSplitQuery");
            Assert.IsTrue(eager.Legs.All(l => l.TransactionType == TransactionType.TRANSFER), "the deposit is not a leg");

            // Lazy: no Include, first access of Legs triggers the load through the injected ILazyLoader.
            var lazy = await db.FundTransfers.SingleAsync(t => t.Id == transferId);
            Assert.HasCount(2, lazy.Legs, "lazy-loaded on first access");

            // Explicit: an entity we built ourselves has no loader; LoadLegsAsync attaches it (Unchanged) and loads.
            // A fresh context: the lazy-loaded instance above is already tracked under the same key.
            using var db2 = NewContext(conn);
            var detached = new FundTransferEntity { Id = transferId };
            Assert.IsEmpty(detached.Legs);
            await new TransactionRepository(db2).LoadLegsAsync(detached);
            Assert.HasCount(2, detached.Legs, "explicit load fills the collection on demand");
            Assert.AreEqual(EntityState.Unchanged, db2.Entry(detached).State, "attached for loading, nothing to write");
        }
    }

    [TestMethod]
    public async Task Restrict_refuses_to_delete_a_transfer_that_still_has_legs()
    {
        var (conn, transferId) = await SeedAsync();
        using (conn)
        {
            using var db = NewContext(conn);
            var transfer = await db.FundTransfers.SingleAsync(t => t.Id == transferId);
            db.FundTransfers.Remove(transfer);
            Assert.AreEqual(EntityState.Deleted, db.Entry(transfer).State);

            await Assert.ThrowsExactlyAsync<DbUpdateException>(() => db.SaveChangesAsync(), "the ledger is append-only: FK RESTRICT blocks the delete");
        }
    }

    [TestMethod]
    public async Task ExecuteDelete_releases_only_stale_reservations()
    {
        var (conn, _) = await SeedAsync();
        using (conn)
        {
            using (var seed = NewContext(conn))
            {
                seed.IdempotencyKeys.AddRange(
                    new IdempotencyKeyEntity { Key = "old-reservation", StatusCode = 202, CreatedAt = DateTime.UtcNow.AddHours(-2) },
                    new IdempotencyKeyEntity { Key = "fresh-reservation", StatusCode = 202, CreatedAt = DateTime.UtcNow },
                    new IdempotencyKeyEntity { Key = "old-but-complete", StatusCode = 201, ResponseBody = "{}", CreatedAt = DateTime.UtcNow.AddHours(-2) });
                await seed.SaveChangesAsync();
            }

            using var db = NewContext(conn);
            var released = await new IdempotencyRepository(db).ReleaseStaleReservationsAsync(DateTime.UtcNow.AddMinutes(-15));

            Assert.AreEqual(1, released);
            var remaining = await db.IdempotencyKeys.AsNoTracking().Select(k => k.Key).OrderBy(k => k).ToListAsync();
            CollectionAssert.AreEqual(new[] { "fresh-reservation", "old-but-complete" }, remaining);
        }
    }
}
