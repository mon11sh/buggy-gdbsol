using System;

namespace AuthService.Domain.Models;

public class AuthAuditLog
{
    public Guid Id { get; private set; }
    public string LoginId { get; private set; }
    public AuditAction Action { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public UserId? UserId { get; private set; }
    public string? Reason { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }

#pragma warning disable CS8618
    private AuthAuditLog() { }
#pragma warning restore CS8618

    private AuthAuditLog(
        Guid id,
        string loginId,
        AuditAction action,
        DateTime createdAt,
        UserId? userId,
        string? reason,
        string? ipAddress,
        string? userAgent)
    {
        Id = id;
        LoginId = loginId;
        Action = action;
        CreatedAt = createdAt;
        UserId = userId;
        Reason = reason;
        IpAddress = ipAddress;
        UserAgent = userAgent;
    }

    public static AuthAuditLog CreateSuccess(string loginId, UserId? userId, string? ipAddress, string? userAgent)
    {
        return new AuthAuditLog(
            Guid.NewGuid(),
            loginId,
            AuditAction.LOGIN_SUCCESS,
            DateTime.UtcNow,
            userId,
            null,
            ipAddress,
            userAgent
        );
    }

    public static AuthAuditLog CreateFailure(string loginId, UserId? userId, string? reason, string? ipAddress, string? userAgent)
    {
        return new AuthAuditLog(
            Guid.NewGuid(),
            loginId,
            AuditAction.LOGIN_FAILURE,
            DateTime.UtcNow,
            userId,
            reason,
            ipAddress,
            userAgent
        );
    }

    public static AuthAuditLog CreateTokenRevoked(string loginId, UserId? userId, string? reason, string? ipAddress, string? userAgent)
    {
        return new AuthAuditLog(
            Guid.NewGuid(),
            loginId,
            AuditAction.TOKEN_REVOKED,
            DateTime.UtcNow,
            userId,
            reason,
            ipAddress,
            userAgent
        );
    }

    public static AuthAuditLog Load(
        Guid id,
        string loginId,
        AuditAction action,
        DateTime createdAt,
        UserId? userId,
        string? reason,
        string? ipAddress,
        string? userAgent)
    {
        return new AuthAuditLog(id, loginId, action, createdAt, userId, reason, ipAddress, userAgent);
    }
}
