using System;
using AuthService.Domain.Models;
using AuthService.Infrastructure.Data;

namespace AuthService.Mapping;

public static class AuthMapper
{
    public static AuthTokenEntity ToEntity(AuthToken domain)
    {
        return new AuthTokenEntity
        {
            Id = domain.Id.ToString(),
            UserId = domain.UserId.Value,
            LoginId = domain.LoginId,
            TokenJti = domain.Jti.Value,
            IssuedAt = domain.IssuedAt,
            ExpiresAt = domain.ExpiresAt,
            IsRevoked = domain.Status == TokenStatus.REVOKED,
            CreatedAt = DateTime.UtcNow
        };
    }

    public static AuthToken ToDomain(AuthTokenEntity entity)
    {
        var status = entity.IsRevoked 
            ? TokenStatus.REVOKED 
            : (entity.ExpiresAt < DateTime.UtcNow ? TokenStatus.EXPIRED : TokenStatus.ACTIVE);

        return AuthToken.Load(
            Guid.Parse(entity.Id),
            new TokenJti(entity.TokenJti),
            new UserId(entity.UserId),
            entity.LoginId,
            entity.IssuedAt,
            entity.ExpiresAt,
            status
        );
    }

    public static AuthAuditLogEntity ToEntity(AuthAuditLog domain)
    {
        return new AuthAuditLogEntity
        {
            Id = domain.Id.ToString(),
            LoginId = domain.LoginId,
            UserId = domain.UserId?.Value,
            Action = domain.Action.ToString(),
            Reason = domain.Reason,
            IpAddress = domain.IpAddress,
            UserAgent = domain.UserAgent,
            CreatedAt = domain.CreatedAt
        };
    }
}
