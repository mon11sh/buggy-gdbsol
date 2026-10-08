using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AccountsService.Infrastructure.Data.Entities;

[Table("savings_account_details")]
public class SavingsAccountDetailsEntity
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
    [Column("date_of_birth")]
    public DateTime DateOfBirth { get; set; }

    [Required]
    [MaxLength(10)]
    [Column("gender")]
    public string Gender { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    [Column("phone_no")]
    public string PhoneNo { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    [Column("aadhar_number")]
    public string AadharNumber { get; set; } = string.Empty;

    [Required]
    [MaxLength(64)]
    [Column("aadhar_hash")]
    public string AadharHash { get; set; } = string.Empty;

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
