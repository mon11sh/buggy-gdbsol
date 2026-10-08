// CONCEPT: ADO.NET (SqlConnection/SqlCommand)
// A runtime-toggleable, hand-written SQL data-access path for AuthService that MIRRORS the
// behaviour of the EF Core AuthAuditRepository. Selected when Settings.DataAccess == "AdoNet"
// (SQL Server only). EF Core remains the default. The domain object is projected onto columns
// via the SAME AuthMapper.ToEntity used by the EF path.
//
// Like the token repo, the write executes its SQL immediately (ADO.NET has no change tracker),
// so the paired AdoNetUnitOfWork.CommitAsync(ct) is a no-op.
using System.Data;
using AuthService.Config;
using AuthService.Domain.Models;
using AuthService.Mapping;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace AuthService.Infrastructure.Repositories;

public class AdoNetAuthAuditRepository : IAuthAuditRepository
{
    private readonly string _connectionString;
    private readonly ILogger<AdoNetAuthAuditRepository> _logger;

    public AdoNetAuthAuditRepository(Settings settings, ILogger<AdoNetAuthAuditRepository> logger)
    {
        // Reuse the token repo's connection-string resolver (same sqlserver-only guard).
        _connectionString = AdoNetAuthTokenRepository.BuildConnectionString(settings);
        _logger = logger;
    }

    public async Task LogAuditAsync(AuthAuditLog auditLog, CancellationToken ct = default)
    {
        var entity = AuthMapper.ToEntity(auditLog);

        // CONCEPT: raw SQL — parameterised INSERT against auth_audit_logs. Nullable columns
        // (user_id, reason, ip_address, user_agent) coalesce to DBNull.
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(
            @"INSERT INTO auth_audit_logs
                (id, login_id, user_id, action, reason, ip_address, user_agent, created_at)
              VALUES
                (@id, @login_id, @user_id, @action, @reason, @ip_address, @user_agent, @created_at)",
            connection);
        command.Parameters.Add(new SqlParameter("@id", SqlDbType.VarChar, 36) { Value = entity.Id });
        command.Parameters.Add(new SqlParameter("@login_id", SqlDbType.VarChar, 255) { Value = entity.LoginId });
        command.Parameters.Add(new SqlParameter("@user_id", SqlDbType.Int) { Value = (object?)entity.UserId ?? DBNull.Value });
        command.Parameters.Add(new SqlParameter("@action", SqlDbType.VarChar, 30) { Value = entity.Action });
        command.Parameters.Add(new SqlParameter("@reason", SqlDbType.VarChar, 500) { Value = (object?)entity.Reason ?? DBNull.Value });
        command.Parameters.Add(new SqlParameter("@ip_address", SqlDbType.VarChar, 45) { Value = (object?)entity.IpAddress ?? DBNull.Value });
        command.Parameters.Add(new SqlParameter("@user_agent", SqlDbType.VarChar, 1000) { Value = (object?)entity.UserAgent ?? DBNull.Value });
        command.Parameters.Add(new SqlParameter("@created_at", SqlDbType.DateTime2) { Value = entity.CreatedAt });

        await command.ExecuteNonQueryAsync(ct);
    }
}
