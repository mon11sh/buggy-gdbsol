using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using TransactionsService.Domain.Models;

namespace TransactionsService.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<TransactionLogEntity> TransactionLogs { get; set; } = null!;
    public DbSet<TransferLimitEntity> TransferLimits { get; set; } = null!;
    public DbSet<TransferModeEntity> TransferModes { get; set; } = null!;
    public DbSet<IdempotencyKeyEntity> IdempotencyKeys { get; set; } = null!;
    public DbSet<FundTransferEntity> FundTransfers { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TransactionLogEntity>(entity =>
        {
            entity.ToTable("transaction_logging");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TransactionType).HasConversion<string>();
            entity.HasIndex(e => e.AccountId);
            entity.HasIndex(e => e.CreatedAt);
            // Per-account history: filter on account_id, order/range on created_at — one composite seek, no sort.
            entity.HasIndex(e => new { e.AccountId, e.CreatedAt }).HasDatabaseName("ix_transaction_logging_account_created");

            // CONCEPT: TPH inheritance mapping - the transaction_type column doubles as the discriminator.
            entity.HasDiscriminator(e => e.TransactionType)
                .HasValue<DepositLogEntity>(TransactionType.DEPOSIT)
                .HasValue<WithdrawalLogEntity>(TransactionType.WITHDRAWAL)
                .HasValue<TransferLegEntity>(TransactionType.TRANSFER);
        });

        modelBuilder.Entity<TransferLegEntity>(entity =>
        {
            // CONCEPT: one-to-many with Fluent API + DeleteBehavior.Restrict — a fund transfer OWNS its two
            // ledger legs; the database refuses to delete a transfer that still has legs (the ledger is
            // append-only, so nothing should ever cascade a delete into it).
            entity.HasOne(e => e.Transfer)
                .WithMany(t => t.Legs)
                .HasForeignKey(e => e.TransferId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => e.TransferId).HasDatabaseName("ix_transaction_logging_transfer_id");
        });

        modelBuilder.Entity<TransferModeEntity>(entity =>
        {
            entity.ToTable("transfer_modes");
            entity.HasKey(e => e.Mode);
            entity.HasData(
                new TransferModeEntity { Mode = "NEFT", Description = "Batch settlement, any amount" },
                new TransferModeEntity { Mode = "IMPS", Description = "Instant, small value" },
                new TransferModeEntity { Mode = "UPI", Description = "Instant, small value" },
                new TransferModeEntity { Mode = "RTGS", Description = "Real-time gross settlement, high value" });
        });

        modelBuilder.Entity<TransferLimitEntity>(entity =>
        {
            entity.ToTable("transfer_limits");
            entity.HasKey(e => e.Privilege);

            // CONCEPT: model seeding with HasData — reference data becomes part of the model, so it is
            // created by EnsureCreated AND carried by migrations (with a diff when a value changes).
            entity.HasData(
                new TransferLimitEntity { Privilege = "PREMIUM", DailyLimit = 100000.00m, PerTransactionLimit = 50000.00m },
                new TransferLimitEntity { Privilege = "GOLD", DailyLimit = 50000.00m, PerTransactionLimit = 25000.00m },
                new TransferLimitEntity { Privilege = "SILVER", DailyLimit = 25000.00m, PerTransactionLimit = 12500.00m });

            // CONCEPT: many-to-many - EF owns the join table (privilege, mode); the join rows are seeded
            // too, so the "which rails may a tier use" rule ships with the schema.
            entity.HasMany(l => l.AllowedModes)
                .WithMany(m => m.Limits)
                .UsingEntity<Dictionary<string, object>>(
                    "transfer_limit_modes",
                    j => j.HasOne<TransferModeEntity>().WithMany().HasForeignKey("mode").OnDelete(DeleteBehavior.Cascade),
                    j => j.HasOne<TransferLimitEntity>().WithMany().HasForeignKey("privilege").OnDelete(DeleteBehavior.Cascade),
                    j =>
                    {
                        j.ToTable("transfer_limit_modes");
                        j.HasKey("privilege", "mode");
                        j.HasData(
                            new { privilege = "SILVER", mode = "NEFT" }, new { privilege = "SILVER", mode = "IMPS" }, new { privilege = "SILVER", mode = "UPI" },
                            new { privilege = "GOLD", mode = "NEFT" }, new { privilege = "GOLD", mode = "IMPS" }, new { privilege = "GOLD", mode = "UPI" }, new { privilege = "GOLD", mode = "RTGS" },
                            new { privilege = "PREMIUM", mode = "NEFT" }, new { privilege = "PREMIUM", mode = "IMPS" }, new { privilege = "PREMIUM", mode = "UPI" }, new { privilege = "PREMIUM", mode = "RTGS" });
                    });
        });

        modelBuilder.Entity<IdempotencyKeyEntity>(entity =>
        {
            entity.ToTable("idempotency_keys");
            entity.HasKey(e => e.Key);
            entity.HasIndex(e => e.CreatedAt);
        });

        modelBuilder.Entity<FundTransferEntity>(entity =>
        {
            entity.ToTable("fund_transfers");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TransferMode).HasConversion<string>();
            // The daily-limit check runs on EVERY transfer (source_account_id + created_at range + status):
            // without this the hottest query in the system is a full table scan (audit Finding #10).
            entity.HasIndex(e => new { e.SourceAccountId, e.CreatedAt, e.Status }).HasDatabaseName("ix_fund_transfers_source_created_status");
            // Global feed ordering and the reconciler's stale-row sweeps (status + created_at).
            entity.HasIndex(e => new { e.Status, e.CreatedAt }).HasDatabaseName("ix_fund_transfers_status_created");
        });
    }

    /// <summary>
    /// CONCEPT: change tracking — every SaveChanges reports what the tracker is about to flush, per
    /// EntityState (Added / Modified / Deleted; Unchanged entities are tracked but not written).
    /// Detached entities are, by definition, not in the tracker at all.
    /// </summary>
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var logger = this.GetService<ILoggerFactory>().CreateLogger<AppDbContext>();
        if (logger.IsEnabled(LogLevel.Debug))
        {
            int added = 0, modified = 0, deleted = 0, unchanged = 0;
            foreach (var entry in ChangeTracker.Entries())
            {
                switch (entry.State)
                {
                    case EntityState.Added: added++; break;
                    case EntityState.Modified: modified++; break;
                    case EntityState.Deleted: deleted++; break;
                    case EntityState.Unchanged: unchanged++; break;
                }
            }
            logger.LogDebug("SaveChanges: added={Added} modified={Modified} deleted={Deleted} unchanged={Unchanged}", added, modified, deleted, unchanged);
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
