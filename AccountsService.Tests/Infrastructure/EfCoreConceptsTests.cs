using AccountsService.Infrastructure.Data;
using AccountsService.Infrastructure.Data.Entities;
using AccountsService.Infrastructure.Repositories;
using AccountsService.Utils;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AccountsService.Tests.Infrastructure;

/// <summary>
/// Runs the relational-only EF Core paths (raw SQL, compiled query, transactions + savepoints)
/// against a real provider (SQLite in-memory) - the InMemory provider cannot execute SQL or transactions.
/// </summary>
[TestClass]
public class EfCoreConceptsTests
{
    private static AppDbContext NewContext(SqliteConnection conn) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options);

    private static AccountEntity Account(int number, string name, string privilege) => new()
    {
        AccountNumber = number, AccountType = "SAVINGS", Name = name, PinHash = "hash", Balance = 1000m, Privilege = privilege,
    };

    private static readonly EncryptionManager Crypto = new(new AccountsService.Config.Settings { PinEncryptionKey = "12345678901234567890123456789012" });
    private static readonly string AlreadyCurrent = Crypto.EncryptData("999999999999");

    private static SavingsAccountDetailsEntity Details(int number, string aadhaar, string hash) => new()
    {
        AccountNumber = number, DateOfBirth = new DateTime(1990, 1, 1), Gender = "F", PhoneNo = $"98765432{number % 100:00}", AadharNumber = aadhaar, AadharHash = hash,
    };

    /// <summary>Three savings accounts; #1000 holds a legacy plaintext Aadhaar, #1001 an undecryptable value, #1002 the current format.</summary>
    private static async Task<SqliteConnection> SeedAsync()
    {
        var conn = new SqliteConnection("DataSource=:memory:");
        await conn.OpenAsync();
        using var db = NewContext(conn);
        await db.Database.EnsureCreatedAsync();
        db.Accounts.AddRange(Account(1000, "Jane Doe", "GOLD"), Account(1001, "John Smith", "SILVER"), Account(1002, "Alice Ray", "GOLD"));
        db.SavingsAccountDetails.AddRange(
            Details(1000, "123456789012", "legacy-1"),
            Details(1001, "not-a-number", "legacy-2"),
            Details(1002, AlreadyCurrent, Crypto.GenerateBlindIndex("999999999999")));
        await db.SaveChangesAsync();
        return conn;
    }

    [TestMethod]
    public async Task SearchAsync_uses_parameterised_raw_sql_on_a_relational_provider()
    {
        using var conn = await SeedAsync();
        using var db = NewContext(conn);
        var repo = new AccountRepository(db);

        var byName = await repo.SearchAsync("jan", null, 10);                  // FromSqlInterpolated
        var byPrivilege = await repo.SearchAsync(null, "gold", 10);            // FromSqlRaw
        var both = await repo.SearchAsync("a", "GOLD", 10);                    // raw SQL composed with LINQ
        var injection = await repo.SearchAsync("' OR 1=1 --", null, 10);      // a parameter, not SQL

        CollectionAssert.AreEqual(new[] { 1000 }, byName.Select(a => a.AccountNumber!.Value).ToArray());
        CollectionAssert.AreEqual(new[] { 1000, 1002 }, byPrivilege.Select(a => a.AccountNumber!.Value).ToArray());
        CollectionAssert.AreEqual(new[] { 1000, 1002 }, both.Select(a => a.AccountNumber!.Value).ToArray());
        Assert.IsEmpty(injection);
    }

    [TestMethod]
    public async Task Compiled_query_returns_the_account_or_null()
    {
        using var conn = await SeedAsync();
        using var db = NewContext(conn);
        var repo = new AccountRepository(db);

        var found = await repo.GetByAccountNumberAsync(1001);
        var missing = await repo.GetByAccountNumberAsync(4242);

        Assert.AreEqual("John Smith", found!.Name);
        Assert.IsNull(missing);
    }

    [TestMethod]
    public async Task Crypto_upgrade_commits_good_rows_and_skips_bad_rows_within_one_transaction()
    {
        using var conn = await SeedAsync();
        var crypto = Crypto;
        var alreadyCurrent = AlreadyCurrent;

        using var db = NewContext(conn);
        var (upgraded, skipped) = await AadhaarCryptoUpgrade.RunAsync(db, crypto, NullLogger.Instance);

        Assert.AreEqual(1, upgraded);
        Assert.AreEqual(1, skipped);

        using var verify = NewContext(conn);
        var rows = await verify.SavingsAccountDetails.AsNoTracking().OrderBy(d => d.AccountNumber).ToListAsync();
        Assert.StartsWith(EncryptionManager.CurrentFormatPrefix, rows[0].AadharNumber, "legacy plaintext re-encrypted");
        Assert.AreEqual(crypto.GenerateBlindIndex("123456789012"), rows[0].AadharHash, "blind index re-derived");
        Assert.AreEqual("not-a-number", rows[1].AadharNumber, "undecryptable row left untouched");
        Assert.AreEqual(alreadyCurrent, rows[2].AadharNumber, "current-format row not rewritten");
    }
}
