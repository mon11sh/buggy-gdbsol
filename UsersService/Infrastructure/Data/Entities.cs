using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UsersService.Infrastructure.Data;

[Table("users")]
public class UserEntity
{
    [Key]
    [Column("user_id")]
    public int UserId { get; set; }

    [Column("username")]
    [MaxLength(255)]
    public string Username { get; set; } = null!;

    [Column("login_id")]
    [MaxLength(50)]
    public string LoginId { get; set; } = null!;

    [Column("password")]
    [MaxLength(255)]
    public string Password { get; set; } = null!;

    [Column("role")]
    [MaxLength(20)]
    public string Role { get; set; } = "MANAGER";

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

[Table("user_audit_logs")]
public class AuditLogEntity
{
    [Key]
    [Column("log_id")]
    public int LogId { get; set; }

    [Column("user_id")]
    public int? UserId { get; set; }

    [Column("action")]
    [MaxLength(30)]
    public string Action { get; set; } = null!;

    [Column("old_data")]
    public string? OldData { get; set; }

    [Column("new_data")]
    public string? NewData { get; set; }

    [Column("performed_by")]
    [MaxLength(50)]
    public string? PerformedBy { get; set; }

    [Column("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
