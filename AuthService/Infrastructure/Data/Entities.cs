using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuthService.Infrastructure.Data;

[Table("auth_tokens")]
public class AuthTokenEntity
{
    [Key]
    [Column("id", TypeName = "varchar(36)")]
    public string Id { get; set; } = null!;

    [Column("user_id")]
    public int UserId { get; set; }

    [Column("login_id", TypeName = "varchar(255)")]
    public string LoginId { get; set; } = null!;

    [Column("token_jti", TypeName = "varchar(255)")]
    public string TokenJti { get; set; } = null!;

    [Column("issued_at")]
    public DateTime IssuedAt { get; set; }

    [Column("expires_at")]
    public DateTime ExpiresAt { get; set; }

    [Column("is_revoked")]
    public bool IsRevoked { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}

[Table("auth_audit_logs")]
public class AuthAuditLogEntity
{
    [Key]
    [Column("id", TypeName = "varchar(36)")]
    public string Id { get; set; } = null!;

    [Column("login_id", TypeName = "varchar(255)")]
    public string LoginId { get; set; } = null!;

    [Column("user_id")]
    public int? UserId { get; set; }

    [Column("action", TypeName = "varchar(30)")]
    public string Action { get; set; } = null!;

    [Column("reason", TypeName = "varchar(500)")]
    public string? Reason { get; set; }

    [Column("ip_address", TypeName = "varchar(45)")]
    public string? IpAddress { get; set; }

    [Column("user_agent", TypeName = "varchar(1000)")]
    public string? UserAgent { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}
