using System;

namespace UsersService.Domain.Models;

public class AuditLog
{
    public int LogId { get; private set; }
    public UserId? UserId { get; private set; }
    public AuditAction Action { get; private set; }
    public string? OldData { get; private set; }
    public string? NewData { get; private set; }
    public string? PerformedBy { get; private set; }
    public DateTime Timestamp { get; private set; }

#pragma warning disable CS8618
    private AuditLog() { }
#pragma warning restore CS8618

    public AuditLog(
        int logId,
        UserId? userId,
        AuditAction action,
        string? oldData,
        string? newData,
        string? performedBy,
        DateTime timestamp)
    {
        LogId = logId;
        UserId = userId;
        Action = action;
        OldData = oldData;
        NewData = newData;
        PerformedBy = performedBy;
        Timestamp = timestamp;
    }
}
