using AccountsService.Infrastructure.Data;
using AccountsService.Infrastructure.Data.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AccountsService.Tests.Infrastructure;

/// <summary>
/// Proves the P0 fix for the account-balance lost-update race: the [ConcurrencyCheck]
/// RowVersion token (re-stamped by AppDbContext.SaveChanges) makes a stale write fail with
/// DbUpdateConcurrencyException instead of silently clobbering another transaction's balance.
/// Uses a real relational provider (SQLite in-memory) because the InMemory provider does not
/// enforce concurrency tokens.
/// </summary>
[TestClass]
public class AccountConcurrencyTests
{
    private static AppDbContext NewContext(SqliteConnection conn) =>
        new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options);

    private static AccountEntity NewAccount(int number, decimal balance) => new()
    {
        AccountNumber = number,
        AccountType = "SAVINGS",
        Name = "Test Holder",
        PinHash = "hash",
        Balance = balance,
        Privilege = "GOLD",
    };

    [TestMethod]
    public async Task RowVersion_BlocksLostUpdate_OnConcurrentBalanceWrite()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        await conn.OpenAsync();

        using (var seed = NewContext(conn))
        {
            await seed.Database.EnsureCreatedAsync();
            seed.Accounts.Add(NewAccount(5000, 100m));
            await seed.SaveChangesAsync();
        }

        // Two independent transactions load the SAME row (identical RowVersion).
        using var ctxA = NewContext(conn);
        using var ctxB = NewContext(conn);
        var a = await ctxA.Accounts.FirstAsync(x => x.AccountNumber == 5000);
        var b = await ctxB.Accounts.FirstAsync(x => x.AccountNumber == 5000);

        // B commits first: 100 -> 60.
        b.Balance = 60m;
        await ctxB.SaveChangesAsync();

        // A attempts a STALE write (100 -> 70). Without the token this silently overwrites B's
        // change (money resurrected). With the token it must throw.
        a.Balance = 70m;
        await Assert.ThrowsExactlyAsync<DbUpdateConcurrencyException>(() => ctxA.SaveChangesAsync());

        using var verify = NewContext(conn);
        var final = await verify.Accounts.FirstAsync(x => x.AccountNumber == 5000);
        Assert.AreEqual(60m, final.Balance, "B's committed balance must survive; A's stale write is rejected.");
    }

    [TestMethod]
    public async Task RowVersion_IsRestamped_OnEveryUpdate()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        await conn.OpenAsync();

        using var ctx = NewContext(conn);
        await ctx.Database.EnsureCreatedAsync();
        var acct = NewAccount(5001, 100m);
        ctx.Accounts.Add(acct);
        await ctx.SaveChangesAsync();
        var v0 = acct.RowVersion;

        acct.Balance = 90m;
        await ctx.SaveChangesAsync();

        Assert.AreNotEqual(v0, acct.RowVersion, "RowVersion must change on each update so stale writes are detectable.");
    }
}
