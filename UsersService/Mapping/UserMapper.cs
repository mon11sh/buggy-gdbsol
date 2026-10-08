using System;
using UsersService.Domain.Models;
using UsersService.Infrastructure.Data;

namespace UsersService.Mapping;

public static class UserMapper
{
    public static User ToDomain(UserEntity entity)
    {
        return User.Load(
            new UserId(entity.UserId),
            new LoginId(entity.LoginId),
            entity.Username,
            PasswordHash.FromExistingHash(entity.Password),
            Enum.TryParse<UserRole>(entity.Role, true, out var role) ? role : UserRole.MANAGER,
            entity.IsActive ? UserStatus.ACTIVE : UserStatus.INACTIVE,
            entity.CreatedAt
        );
    }

    public static UserEntity ToEntity(User domain)
    {
        return new UserEntity
        {
            UserId = domain.Id.Value,
            LoginId = domain.LoginId.Value,
            Username = domain.Username,
            Password = domain.Password.Hash,
            Role = domain.Role.ToString(),
            IsActive = domain.Status == UserStatus.ACTIVE,
            CreatedAt = domain.CreatedAt,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public static AuditLog ToDomain(AuditLogEntity entity)
    {
        return new AuditLog(
            entity.LogId,
            entity.UserId.HasValue ? new UserId(entity.UserId.Value) : null,
            Enum.TryParse<AuditAction>(entity.Action, true, out var action) ? action : AuditAction.UPDATE,
            entity.OldData,
            entity.NewData,
            entity.PerformedBy,
            entity.Timestamp
        );
    }

    public static AuditLogEntity ToEntity(AuditLog domain)
    {
        return new AuditLogEntity
        {
            LogId = domain.LogId,
            UserId = domain.UserId?.Value,
            Action = domain.Action.ToString(),
            OldData = domain.OldData,
            NewData = domain.NewData,
            PerformedBy = domain.PerformedBy,
            Timestamp = domain.Timestamp
        };
    }
}
