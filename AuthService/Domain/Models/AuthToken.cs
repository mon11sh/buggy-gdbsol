using System;

namespace AuthService.Domain.Models;

public class AuthToken
{
    public Guid Id { get; private set; }
    public TokenJti Jti { get; private set; }
    public UserId UserId { get; private set; }
    public string LoginId { get; private set; }
    public DateTime IssuedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public TokenStatus Status { get; private set; }

#pragma warning disable CS8618
    private AuthToken() { }
#pragma warning restore CS8618

    private AuthToken(Guid id, TokenJti jti, UserId userId, string loginId, DateTime issuedAt, DateTime expiresAt, TokenStatus status)
    {
        Id = id;
        Jti = jti;
        UserId = userId;
        LoginId = loginId;
        IssuedAt = issuedAt;
        ExpiresAt = expiresAt;
        Status = status;
    }

    public static AuthToken Create(TokenJti jti, UserId userId, string loginId, DateTime issuedAt, DateTime expiresAt)
    {
        return new AuthToken(
            Guid.NewGuid(),
            jti,
            userId,
            loginId,
            issuedAt,
            expiresAt,
            TokenStatus.ACTIVE
        );
    }

    public static AuthToken Load(Guid id, TokenJti jti, UserId userId, string loginId, DateTime issuedAt, DateTime expiresAt, TokenStatus status)
    {
        return new AuthToken(id, jti, userId, loginId, issuedAt, expiresAt, status);
    }

    public void Revoke()
    {
        Status = TokenStatus.REVOKED;
    }

    public bool IsActive(DateTime now)
    {
        return Status == TokenStatus.ACTIVE && ExpiresAt > now;
    }
}
