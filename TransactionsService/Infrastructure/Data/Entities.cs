using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore.Infrastructure;
using TransactionsService.Domain.Models;

namespace TransactionsService.Infrastructure.Data;

/// <summary>
/// CONCEPT: inheritance mapping, table-per-hierarchy (TPH). One table (<c>transaction_logging</c>)
/// stores every ledger row; the existing <c>transaction_type</c> column is the discriminator that
/// tells EF which CLR subtype to materialise. Subtype-only columns (<c>transfer_id</c>) are simply
/// nullable in the shared table. <see cref="Create"/> is the factory the repositories use.
/// </summary>
[Table("transaction_logging")]
public abstract class TransactionLogEntity
{
    protected TransactionLogEntity(TransactionType transactionType) => TransactionType = transactionType;

    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("account_id")]
    public int AccountId { get; set; }

    /// <summary>The TPH discriminator (set by the subtype constructor; never change it on an existing row).</summary>
    [Column("transaction_type")]
    public TransactionType TransactionType { get; set; }

    [Column("amount", TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [Column("balance_after", TypeName = "decimal(18,2)")]
    public decimal BalanceAfter { get; set; }

    [Column("reference_id")]
    public string? ReferenceId { get; set; }

    [Column("description")]
    public string? Description { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public static TransactionLogEntity Create(TransactionType type) => type switch
    {
        TransactionType.DEPOSIT => new DepositLogEntity(),
        TransactionType.WITHDRAWAL => new WithdrawalLogEntity(),
        TransactionType.TRANSFER => new TransferLegEntity(),
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "No ledger row type for this transaction type."),
    };
}

public sealed class DepositLogEntity : TransactionLogEntity
{
    public DepositLogEntity() : base(TransactionType.DEPOSIT) { }
}

public sealed class WithdrawalLogEntity : TransactionLogEntity
{
    public WithdrawalLogEntity() : base(TransactionType.WITHDRAWAL) { }
}

/// <summary>One leg (source debit or destination credit) of a fund transfer.</summary>
public sealed class TransferLegEntity : TransactionLogEntity
{
    public TransferLegEntity() : base(TransactionType.TRANSFER) { }

    // CONCEPT: one-to-many (many side) - a leg belongs to exactly one fund transfer.
    [Column("transfer_id")]
    public int? TransferId { get; set; }
    public FundTransferEntity? Transfer { get; set; }
}

[Table("transfer_limits")]
public class TransferLimitEntity
{
    [Key]
    [Column("privilege")]
    public string Privilege { get; set; } = "";

    [Column("daily_limit", TypeName = "decimal(18,2)")]
    public decimal DailyLimit { get; set; }

    [Column("per_transaction_limit", TypeName = "decimal(18,2)")]
    public decimal PerTransactionLimit { get; set; }

    // CONCEPT: many-to-many - a privilege tier permits several transfer modes and a mode is
    // permitted to several tiers; EF manages the join table transfer_limit_modes.
    public ICollection<TransferModeEntity> AllowedModes { get; set; } = new List<TransferModeEntity>();

    public bool Allows(TransferMode mode) =>
        AllowedModes.Count == 0 || AllowedModes.Any(m => string.Equals(m.Mode, mode.ToString(), StringComparison.OrdinalIgnoreCase));
}

/// <summary>Reference row for a payment rail (NEFT/RTGS/IMPS/UPI).</summary>
[Table("transfer_modes")]
public class TransferModeEntity
{
    [Key]
    [Column("mode")]
    public string Mode { get; set; } = "";

    [Column("description")]
    public string? Description { get; set; }

    [JsonIgnore] // back-reference; never serialised (would recurse)
    public ICollection<TransferLimitEntity> Limits { get; set; } = new List<TransferLimitEntity>();
}

[Table("idempotency_keys")]
public class IdempotencyKeyEntity
{
    [Key]
    [Column("key")]
    public string Key { get; set; } = "";

    [Column("response_body")]
    public string ResponseBody { get; set; } = "";

    [Column("status_code")]
    public int StatusCode { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

[Table("fund_transfers")]
public class FundTransferEntity
{
    // CONCEPT: lazy loading WITHOUT proxies - EF injects ILazyLoader through this constructor when it
    // materialises the entity; the first read of Legs then loads them on demand. Code that creates
    // the entity itself (ADO.NET path, tests) uses the public constructor: no loader, plain collection.
    private readonly ILazyLoader? _lazyLoader;
    private ICollection<TransferLegEntity>? _legs;

    public FundTransferEntity() { }
    private FundTransferEntity(ILazyLoader lazyLoader) => _lazyLoader = lazyLoader;

    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("source_account_id")]
    public int SourceAccountId { get; set; }

    [Column("destination_account_id")]
    public int DestinationAccountId { get; set; }

    [Column("amount", TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [Column("transfer_mode")]
    public TransferMode TransferMode { get; set; }

    [Column("status")]
    public string Status { get; set; } = "PENDING";

    [Column("failure_reason")]
    public string? FailureReason { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // CONCEPT: one-to-many (one side) - the two ledger legs (debit + credit) written for this transfer.
    public ICollection<TransferLegEntity> Legs
    {
        get
        {
            _lazyLoader.Load(this, ref _legs); // no-op without a loader (or when already loaded)
            return _legs ??= new List<TransferLegEntity>();
        }
        set => _legs = value;
    }
}
