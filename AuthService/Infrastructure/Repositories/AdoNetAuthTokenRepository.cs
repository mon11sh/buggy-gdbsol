// CONCEPT: ADO.NET (SqlConnection/SqlCommand/SqlDataReader)
// A runtime-toggleable, hand-written SQL data-access path for AuthService that MIRRORS the
// behaviour of the EF Core AuthTokenRepository. Selected when Settings.DataAccess == "AdoNet"
// (SQL Server only). EF Core remains the default. Domain objects are rebuilt via the SAME
// AuthMapper.ToDomain used by the EF path, from an AuthTokenEntity hydrated out of the reader.
//
// NOTE on the Unit-of-Work boundary: the EF repository stages changes on the DbContext and
// only persists them when UnitOfWork.CommitAsync(ct) calls SaveChangesAsync(ct). ADO.NET has no
// change tracker, so each write here executes its SQL immediately; the paired AdoNetUnitOfWork
// therefore treats CommitAsync(ct) as a no-op.
using System.Data;
using AuthService.Config;
using AuthService.Domain.Models;
using AuthService.Infrastructure.Data;
using AuthService.Mapping;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace AuthService.Infrastructure.Repositories;

public class AdoNetAuthTokenRepository : IAuthTokenRepository
{
    private readonly string _connectionString;
    private readonly ILogger<AdoNetAuthTokenRepository> _logger;

    public AdoNetAuthTokenRepository(Settings settings, ILogger<AdoNetAuthTokenRepository> logger)
    {
        _connectionString = BuildConnectionString(settings);
        _logger = logger;
    }

    /// <summary>
    /// Resolves the SQL Server connection string. Uses Settings.DatabaseUrl when provided
    /// (the same string EF Core uses), otherwise builds it from the discrete Database* fields
    /// exactly like Program.cs's AddDbContext fallback for the sqlserver provider.
    /// </summary>
    public static string BuildConnectionString(Settings settings)
    {
        var provider = settings.DatabaseProvider.ToLowerInvariant();
        if (provider != "sqlserver" && provider != "mssql")
            throw new InvalidOperationException(
                $"AdoNet data access requires the 'sqlserver' provider; current provider is '{settings.DatabaseProvider}'.");

        if (!string.IsNullOrEmpty(settings.DatabaseUrl))
            return settings.DatabaseUrl;

        return $"Server={settings.DatabaseHost},{settings.DatabasePort};Database={settings.DatabaseName};" +
               $"User Id={settings.DatabaseUser};Password={settings.DatabasePassword};TrustServerCertificate=True;{Gdb.Common.Data.ConnectionPooling.SqlServer}";
    }

    // ---------------------------------------------------------------------------------------
    // Startup helper: create/refresh the usp_GetAuthTokenByJti stored procedure (idempotent).
    // ---------------------------------------------------------------------------------------
    public static async Task EnsureStoredProceduresAsync(Settings settings, ILogger logger, CancellationToken ct = default)
    {
        var connectionString = BuildConnectionString(settings);
        var sqlPath = Path.Combine(AppContext.BaseDirectory,
            "Infrastructure", "Data", "StoredProcedures", "usp_GetAuthTokenByJti.sql");

        if (!File.Exists(sqlPath))
        {
            logger.LogWarning("Stored procedure script not found at {Path}; skipping creation.", sqlPath);
            return;
        }

        var script = await File.ReadAllTextAsync(sqlPath, ct);

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(script, connection) { CommandType = CommandType.Text };
        await command.ExecuteNonQueryAsync(ct);
        logger.LogInformation("Ensured stored procedure usp_GetAuthTokenByJti (CREATE OR ALTER).");
    }

    // ---------------------------------------------------------------------------------------
    // Writes
    // ---------------------------------------------------------------------------------------
    public async Task CreateTokenAsync(AuthToken token, CancellationToken ct = default)
    {
        // Reuse the EF mapper to project the domain object onto the same columns.
        var entity = AuthMapper.ToEntity(token);

        // CONCEPT: raw SQL — parameterised INSERT against auth_tokens.
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(
            @"INSERT INTO auth_tokens
                (id, user_id, login_id, token_jti, issued_at, expires_at, is_revoked, created_at)
              VALUES
                (@id, @user_id, @login_id, @token_jti, @issued_at, @expires_at, @is_revoked, @created_at)",
            connection);
        command.Parameters.Add(new SqlParameter("@id", SqlDbType.VarChar, 36) { Value = entity.Id });
        command.Parameters.Add(new SqlParameter("@user_id", SqlDbType.Int) { Value = entity.UserId });
        command.Parameters.Add(new SqlParameter("@login_id", SqlDbType.VarChar, 255) { Value = entity.LoginId });
        command.Parameters.Add(new SqlParameter("@token_jti", SqlDbType.VarChar, 255) { Value = entity.TokenJti });
        command.Parameters.Add(new SqlParameter("@issued_at", SqlDbType.DateTime2) { Value = entity.IssuedAt });
        command.Parameters.Add(new SqlParameter("@expires_at", SqlDbType.DateTime2) { Value = entity.ExpiresAt });
        command.Parameters.Add(new SqlParameter("@is_revoked", SqlDbType.Bit) { Value = entity.IsRevoked });
        command.Parameters.Add(new SqlParameter("@created_at", SqlDbType.DateTime2) { Value = entity.CreatedAt });

        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task RevokeTokenAsync(TokenJti tokenJti, CancellationToken ct = default)
    {
        // CONCEPT: raw SQL — parameterised UPDATE mirroring the EF path (set is_revoked = 1
        // for the matching jti; silently no-op when the token does not exist, exactly like the
        // FirstOrDefault + null-check in AuthTokenRepository).
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(
            "UPDATE auth_tokens SET is_revoked = 1 WHERE token_jti = @token_jti", connection);
        command.Parameters.Add(new SqlParameter("@token_jti", SqlDbType.VarChar, 255) { Value = tokenJti.Value });

        await command.ExecuteNonQueryAsync(ct);
    }

    // ---------------------------------------------------------------------------------------
    // Reads
    // ---------------------------------------------------------------------------------------
    public async Task<AuthToken?> GetTokenAsync(TokenJti tokenJti, CancellationToken ct = default)
    {
        // CONCEPT: call a stored procedure via CommandType.StoredProcedure.
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand("usp_GetAuthTokenByJti", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.Add(new SqlParameter("@token_jti", SqlDbType.VarChar, 255) { Value = tokenJti.Value });

        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;

        var entity = MapEntity(reader);
        // Reuse the EF mapper's status resolution so the ADO.NET and EF paths agree.
        return AuthMapper.ToDomain(entity);
    }

    // ---------------------------------------------------------------------------------------
    // Mapping helper — hydrate an AuthTokenEntity from the current reader row.
    // ---------------------------------------------------------------------------------------
    private static AuthTokenEntity MapEntity(SqlDataReader reader) => new AuthTokenEntity
    {
        Id = reader.GetString(reader.GetOrdinal("id")),
        UserId = reader.GetInt32(reader.GetOrdinal("user_id")),
        LoginId = reader.GetString(reader.GetOrdinal("login_id")),
        TokenJti = reader.GetString(reader.GetOrdinal("token_jti")),
        IssuedAt = reader.GetDateTime(reader.GetOrdinal("issued_at")),
        ExpiresAt = reader.GetDateTime(reader.GetOrdinal("expires_at")),
        IsRevoked = reader.GetBoolean(reader.GetOrdinal("is_revoked")),
        CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at"))
    };

    public async Task<int> PurgeExpiredAsync(DateTime expiredBeforeUtc, CancellationToken ct = default)
    {
        // CONCEPT: raw SQL DELETE - the affected-row count comes back from ExecuteNonQuery.
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand("DELETE FROM auth_tokens WHERE expires_at < @before", connection);
        command.Parameters.Add(new SqlParameter("@before", SqlDbType.DateTime2) { Value = expiredBeforeUtc });
        return await command.ExecuteNonQueryAsync(ct);
    }
}
