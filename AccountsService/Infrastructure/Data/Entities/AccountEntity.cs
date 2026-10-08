using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AccountsService.Infrastructure.Data.Entities;

[Table("accounts")]
public class AccountEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("account_number")]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int AccountNumber { get; set; }

    [Required]
    [MaxLength(10)]
    [Column("account_type")]
    public string AccountType { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    [Column("pin_hash")]
    public string PinHash { get; set; } = string.Empty;

    [Required]
    [Column("balance", TypeName = "decimal(15,2)")]
    public decimal Balance { get; set; } = 0.0m;

    [Required]
    [MaxLength(10)]
    [Column("privilege")]
    public string Privilege { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    [Column("bank_name")]
    public string BankName { get; set; } = "Global Digital Bank";

    [Required]
    [MaxLength(255)]
    [Column("bank_branch")]
    public string BankBranch { get; set; } = "Main Branch";

    [Required]
    [MaxLength(20)]
    [Column("ifsc_code")]
    public string IfscCode { get; set; } = "GDB0000001";

    [Required]
    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Required]
    [Column("activated_date")]
    public DateTime ActivatedDate { get; set; } = DateTime.UtcNow;

    [Column("closed_date")]
    public DateTime? ClosedDate { get; set; }

    [Required]
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Required]
    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Optimistic-concurrency token. Marked [ConcurrencyCheck] (portable across SQLite/MySQL/
    // Postgres/SQL Server, ignored by the InMemory provider) and re-stamped on every update by
    // AppDbContext.SaveChanges — so a stale write (e.g. a concurrent debit/credit) fails with
    // DbUpdateConcurrencyException instead of silently overwriting another transaction's balance.
    [ConcurrencyCheck]
    [Column("row_version")]
    public Guid RowVersion { get; set; } = Guid.NewGuid();

    // Navigation properties
    public SavingsAccountDetailsEntity? SavingsDetails { get; set; }
    public CurrentAccountDetailsEntity? CurrentDetails { get; set; }
}
