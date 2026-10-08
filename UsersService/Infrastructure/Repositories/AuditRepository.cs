using System.Text.Json;
using UsersService.Domain.Models;
using UsersService.Infrastructure.Data;
using UsersService.Mapping;

namespace UsersService.Infrastructure.Repositories;

public interface IAuditRepository
{
    Task<bool> LogActionAsync(AuditLog auditLog, CancellationToken ct = default);
}

public class AuditRepository : IAuditRepository
{
    private readonly AppDbContext _context;
    private readonly ILogger<AuditRepository> _logger;

    public AuditRepository(AppDbContext context, ILogger<AuditRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<bool> LogActionAsync(AuditLog auditLog, CancellationToken ct = default)
    {
        try
        {
            var entity = UserMapper.ToEntity(auditLog);
            _context.AuditLogs.Add(entity);
            _logger.LogInformation("Audit logged: {Action} for user_id {UserId}", auditLog.Action, auditLog.UserId?.Value);
            return await Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging audit action");
            return false;
        }
    }
}
