using Microsoft.EntityFrameworkCore;

namespace AuthService.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<AuthTokenEntity> AuthTokens { get; set; }
    public DbSet<AuthAuditLogEntity> AuthAuditLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuthTokenEntity>(entity =>
        {
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.TokenJti).IsUnique();
            entity.HasIndex(e => e.ExpiresAt);
            entity.HasIndex(e => e.IsRevoked);
        });

        modelBuilder.Entity<AuthAuditLogEntity>(entity =>
        {
            entity.HasIndex(e => e.LoginId);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.Action);
            entity.HasIndex(e => e.CreatedAt);
        });

        base.OnModelCreating(modelBuilder);
    }
}
