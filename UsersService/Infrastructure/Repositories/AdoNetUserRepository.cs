// CONCEPT: ADO.NET (SqlConnection/SqlCommand/SqlParameter/SqlDataReader)
// A runtime-toggleable, hand-written SQL data-access path for UsersService that MIRRORS the
// behaviour of the EF Core UserRepository. Selected when Settings.DataAccess == "AdoNet"
// (SQL Server only). EF Core remains the default. Domain objects are rebuilt with the SAME
// mapper (UserMapper.ToDomain/ToEntity) the EF repository uses, so mapping stays identical.
using System.Data;
using Microsoft.Data.SqlClient;
using UsersService.Config;
using UsersService.Domain.Models;
using UsersService.Infrastructure.Data;
using UsersService.Mapping;

namespace UsersService.Infrastructure.Repositories;

public class AdoNetUserRepository : IUserRepository
{
    private readonly string _connectionString;
    private readonly ILogger<AdoNetUserRepository> _logger;

    // Base projection used by every read. Column names match the EF entity mapping in Entities.cs.
    private const string SelectColumns =
        "user_id, username, login_id, password, role, is_active, created_at, updated_at";

    public AdoNetUserRepository(Settings settings, ILogger<AdoNetUserRepository> logger)
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
    // Startup helper: create/refresh the usp_UserList stored procedure (idempotent).
    // ---------------------------------------------------------------------------------------
    public static async Task EnsureStoredProceduresAsync(Settings settings, ILogger logger, CancellationToken ct = default)
    {
        var connectionString = BuildConnectionString(settings);
        var sqlPath = Path.Combine(AppContext.BaseDirectory,
            "Infrastructure", "Data", "StoredProcedures", "usp_UserList.sql");

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
        logger.LogInformation("Ensured stored procedure usp_UserList (CREATE OR ALTER).");
    }

    // ---------------------------------------------------------------------------------------
    // Reads
    // ---------------------------------------------------------------------------------------
    public async Task<User?> GetUserByLoginIdAsync(string loginId, CancellationToken ct = default)
    {
        // CONCEPT: raw SQL — parameterised SELECT (mirrors EF FirstOrDefault(u => u.LoginId == loginId)).
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(
            $"SELECT {SelectColumns} FROM users WHERE login_id = @loginId", connection);
        command.Parameters.Add(new SqlParameter("@loginId", SqlDbType.NVarChar, 50) { Value = loginId });

        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return UserMapper.ToDomain(MapEntity(reader));
    }

    public async Task<User?> GetUserByIdAsync(int userId, CancellationToken ct = default)
    {
        // CONCEPT: raw SQL — parameterised SELECT by primary key (mirrors EF FindAsync).
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(
            $"SELECT {SelectColumns} FROM users WHERE user_id = @userId", connection);
        command.Parameters.Add(new SqlParameter("@userId", SqlDbType.Int) { Value = userId });

        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return UserMapper.ToDomain(MapEntity(reader));
    }

    public async Task<int> CountActiveAdminsAsync(CancellationToken ct = default)
    {
        // CONCEPT: raw SQL — scalar COUNT (mirrors EF Count(u => u.Role == "ADMIN" && u.IsActive)).
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(
            "SELECT COUNT(*) FROM users WHERE role = @role AND is_active = 1", connection);
        command.Parameters.Add(new SqlParameter("@role", SqlDbType.NVarChar, 20) { Value = "ADMIN" });

        var result = await command.ExecuteScalarAsync(ct);
        return (result == null || result == DBNull.Value) ? 0 : Convert.ToInt32(result);
    }

    public async Task<List<User>> GetAllUsersAsync(CancellationToken ct = default)
    {
        // CONCEPT: call a stored procedure via CommandType.StoredProcedure.
        // usp_UserList returns every user ordered by created_at DESC (mirrors the EF
        // OrderByDescending(u => u.CreatedAt) projection).
        var results = new List<User>();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand("usp_UserList", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            results.Add(UserMapper.ToDomain(MapEntity(reader)));

        return results;
    }

    // ---------------------------------------------------------------------------------------
    // Writes
    // ---------------------------------------------------------------------------------------
    public async Task<User> CreateUserAsync(User user, CancellationToken ct = default)
    {
        // CONCEPT: raw SQL — parameterised INSERT. user_id is an IDENTITY column, so it is
        // omitted from the column list and the DB-generated key is read back via SCOPE_IDENTITY().
        var entity = UserMapper.ToEntity(user);

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(
            @"INSERT INTO users (username, login_id, password, role, is_active, created_at, updated_at)
              VALUES (@username, @login_id, @password, @role, @is_active, @created_at, @updated_at);
              SELECT CAST(SCOPE_IDENTITY() AS INT);", connection);
        command.Parameters.Add(new SqlParameter("@username", SqlDbType.NVarChar, 255) { Value = entity.Username });
        command.Parameters.Add(new SqlParameter("@login_id", SqlDbType.NVarChar, 50) { Value = entity.LoginId });
        command.Parameters.Add(new SqlParameter("@password", SqlDbType.NVarChar, 255) { Value = entity.Password });
        command.Parameters.Add(new SqlParameter("@role", SqlDbType.NVarChar, 20) { Value = entity.Role });
        command.Parameters.Add(new SqlParameter("@is_active", SqlDbType.Bit) { Value = entity.IsActive });
        command.Parameters.Add(new SqlParameter("@created_at", SqlDbType.DateTime2) { Value = entity.CreatedAt });
        command.Parameters.Add(new SqlParameter("@updated_at", SqlDbType.DateTime2) { Value = entity.UpdatedAt });

        var newId = await command.ExecuteScalarAsync(ct);
        entity.UserId = (newId == null || newId == DBNull.Value) ? 0 : Convert.ToInt32(newId);

        // Return the mapped version to capture the DB-generated ID (mirrors the EF repo).
        return UserMapper.ToDomain(entity);
    }

    public async Task<User> UpdateUserAsync(User user, CancellationToken ct = default)
    {
        // CONCEPT: raw SQL — parameterised UPDATE. Mirrors the EF repo, which updates
        // Username / Password / Role / IsActive / UpdatedAt for the matched user_id.
        var entity = UserMapper.ToEntity(user);

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(
            @"UPDATE users
                 SET username = @username, password = @password, role = @role,
                     is_active = @is_active, updated_at = @updated_at
               WHERE user_id = @user_id", connection);
        command.Parameters.Add(new SqlParameter("@username", SqlDbType.NVarChar, 255) { Value = entity.Username });
        command.Parameters.Add(new SqlParameter("@password", SqlDbType.NVarChar, 255) { Value = entity.Password });
        command.Parameters.Add(new SqlParameter("@role", SqlDbType.NVarChar, 20) { Value = entity.Role });
        command.Parameters.Add(new SqlParameter("@is_active", SqlDbType.Bit) { Value = entity.IsActive });
        command.Parameters.Add(new SqlParameter("@updated_at", SqlDbType.DateTime2) { Value = DateTime.UtcNow });
        command.Parameters.Add(new SqlParameter("@user_id", SqlDbType.Int) { Value = user.Id.Value });

        await command.ExecuteNonQueryAsync(ct);
        return user;
    }

    // ---------------------------------------------------------------------------------------
    // Mapping helper — read a users row into the SAME UserEntity the EF mapper consumes.
    // ---------------------------------------------------------------------------------------
    private static UserEntity MapEntity(SqlDataReader reader) => new UserEntity
    {
        UserId = reader.GetInt32(reader.GetOrdinal("user_id")),
        Username = reader.GetString(reader.GetOrdinal("username")),
        LoginId = reader.GetString(reader.GetOrdinal("login_id")),
        Password = reader.GetString(reader.GetOrdinal("password")),
        Role = reader.GetString(reader.GetOrdinal("role")),
        IsActive = reader.GetBoolean(reader.GetOrdinal("is_active")),
        CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
        UpdatedAt = reader.GetDateTime(reader.GetOrdinal("updated_at"))
    };
}
