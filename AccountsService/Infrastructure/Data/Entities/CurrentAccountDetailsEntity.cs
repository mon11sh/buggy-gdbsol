using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AccountsService.Infrastructure.Data.Entities;

[Table("current_account_details")]
public class CurrentAccountDetailsEntity
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
    [MaxLength(255)]
    [Column("company_name")]
    public string CompanyName { get; set; } = string.Empty;

    [MaxLength(255)]
    [Column("website")]
    public string? Website { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("registration_no")]
    public string RegistrationNo { get; set; } = string.Empty;

    [Required]
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Required]
    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    [ForeignKey("AccountNumber")]
    public AccountEntity Account { get; set; } = null!;
}
