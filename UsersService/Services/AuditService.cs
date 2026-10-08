using System.Text.Json;
using UsersService.Domain.Models;
using UsersService.Infrastructure.Repositories;

namespace UsersService.Services;

public class AuditService
{
    public static async Task LogActionAsync(IAuditRepository repository, int? userId, string actionString, object? oldData = null, object? newData = null, string? performedBy = null, CancellationToken ct = default)
    {
        var action = Enum.Parse<AuditAction>(actionString, true);
        var oldDataJson = oldData != null ? JsonSerializer.Serialize(oldData) : null;
        var newDataJson = newData != null ? JsonSerializer.Serialize(newData) : null;

        var auditLog = new AuditLog(
            0, // DB generated ID
            userId.HasValue ? new UserId(userId.Value) : null,
            action,
            oldDataJson,
            newDataJson,
            performedBy,
            DateTime.UtcNow
        );

        await repository.LogActionAsync(auditLog, ct);
    }
}
