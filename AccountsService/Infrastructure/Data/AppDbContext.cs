using AccountsService.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AccountsService.Infrastructure.Data;

/// <summary>
/// Entity Framework Core database context for the Accounts microservice.
/// </summary>
/// <remarks>
/// Architectural Intent: This context is strictly scoped to the Accounts domain. It is dynamically configured 
/// at startup to support PostgreSQL, MySQL, SQL Server, SQLite, or InMemory providers based on 
/// environment variables, enabling cross-platform deployment.
/// </remarks>
public class AppDbContext : DbContext
{
    public DbSet<AccountEntity> Accounts { get; set; }
    public DbSet<SavingsAccountDetailsEntity> SavingsAccountDetails { get; set; }
    public DbSet<CurrentAccountDetailsEntity> CurrentAccountDetails { get; set; }

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Business Rule: Account numbers are unique domain identifiers across the entire system.
        // The foreign key relationships enforce that an Account can only have ONE type of 
        // detailed record (Savings or Current) and cascading deletes prevent orphaned detail records.
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AccountEntity>()
            .HasIndex(a => a.AccountNumber)
            .IsUnique();

        modelBuilder.Entity<AccountEntity>()
            .HasOne(a => a.SavingsDetails)
            .WithOne(s => s.Account)
            .HasForeignKey<SavingsAccountDetailsEntity>(s => s.AccountNumber)
            .HasPrincipalKey<AccountEntity>(a => a.AccountNumber)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AccountEntity>()
            .HasOne(a => a.CurrentDetails)
            .WithOne(c => c.Account)
            .HasForeignKey<CurrentAccountDetailsEntity>(c => c.AccountNumber)
            .HasPrincipalKey<AccountEntity>(a => a.AccountNumber)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<SavingsAccountDetailsEntity>()
            .HasIndex(s => s.AccountNumber)
            .IsUnique();

        modelBuilder.Entity<SavingsAccountDetailsEntity>()
            .HasIndex(s => s.AadharHash)
            .IsUnique();

        modelBuilder.Entity<CurrentAccountDetailsEntity>()
            .HasIndex(c => c.AccountNumber)
            .IsUnique();

        modelBuilder.Entity<CurrentAccountDetailsEntity>()
            .HasIndex(c => c.RegistrationNo)
            .IsUnique();

        // Non-unique secondary indexes to speed up the common list/summary read paths:
        // filtering accounts by privilege tier, and the composite type + active-status filter
        // used by the account-summary aggregation.
        modelBuilder.Entity<AccountEntity>()
            .HasIndex(a => a.Privilege)
            .HasDatabaseName("ix_accounts_privilege");

        modelBuilder.Entity<AccountEntity>()
            .HasIndex(a => new { a.AccountType, a.IsActive })
            .HasDatabaseName("ix_accounts_type_active");
    }
    
    public override int SaveChanges()
    {
        UpdateTimestamps();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void UpdateTimestamps()
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.Entity is AccountEntity || e.Entity is SavingsAccountDetailsEntity || e.Entity is CurrentAccountDetailsEntity)
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

        foreach (var entry in entries)
        {
            var now = DateTime.UtcNow;
            
            if (entry.State == EntityState.Added)
            {
                var createdAtProperty = entry.Property("CreatedAt");
                if (createdAtProperty != null) createdAtProperty.CurrentValue = now;
            }
            
            var updatedAtProperty = entry.Property("UpdatedAt");
            if (updatedAtProperty != null) updatedAtProperty.CurrentValue = now;

            // Re-stamp the optimistic-concurrency token on every AccountEntity update so a
            // concurrent stale write (WHERE row_version = <original>) affects 0 rows and throws
            // DbUpdateConcurrencyException instead of clobbering another transaction's change.
            if (entry.State == EntityState.Modified && entry.Entity is AccountEntity)
            {
                entry.Property(nameof(AccountEntity.RowVersion)).CurrentValue = Guid.NewGuid();
            }
        }
    }
}
