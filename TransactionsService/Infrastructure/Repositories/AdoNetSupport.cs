// CONCEPT: ADO.NET (shared connection helper + stored-procedure bootstrap)
// Support code shared by every AdoNet*Repository in TransactionsService. Selected at runtime when
// Settings.DataAccess == "AdoNet" (SQL Server only). EF Core remains the default. The connection
// string is resolved from Settings.DatabaseUrl (the same string EF Core uses) or built from the
// discrete Database* fields exactly like Program.cs's AddDbContext fallback for the sqlserver provider.
using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using TransactionsService.Config;

namespace TransactionsService.Infrastructure.Repositories;

internal static class AdoNetSupport
{
    /// <summary>
    /// Resolves the SQL Server connection string. Uses Settings.DatabaseUrl when provided (the same
    /// string EF Core uses), otherwise builds it from the discrete Database* fields. Throws when the
    /// configured provider is not SQL Server, since the hand-written SQL below is T-SQL specific.
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
    // Startup helper: create/refresh the usp_TransferDailyStats stored procedure (idempotent).
    // Called once at startup from Program.cs when DataAccess=AdoNet on SQL Server.
    // ---------------------------------------------------------------------------------------
    public static async Task EnsureStoredProceduresAsync(Settings settings, ILogger logger, CancellationToken ct = default)
    {
        var connectionString = BuildConnectionString(settings);
        var scriptDir = Path.Combine(AppContext.BaseDirectory, "Infrastructure", "Data", "StoredProcedures");
        if (!Directory.Exists(scriptDir))
        {
            logger.LogWarning("SQL script folder not found at {Path}; skipping database objects.", scriptDir);
            return;
        }

        // Schema patches first (columns the objects depend on), then functions, then procedures.
        var scripts = Directory.GetFiles(scriptDir, "*.sql")
            .OrderBy(p => Path.GetFileName(p).StartsWith("schema_", StringComparison.OrdinalIgnoreCase) ? 0
                        : Path.GetFileName(p).StartsWith("ufn_", StringComparison.OrdinalIgnoreCase) ? 1 : 2)
            .ThenBy(p => p, StringComparer.OrdinalIgnoreCase);

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);
        foreach (var path in scripts)
        {
            var script = await File.ReadAllTextAsync(path, ct);
            await using var command = new SqlCommand(script, connection) { CommandType = CommandType.Text };
            await command.ExecuteNonQueryAsync(ct);
            logger.LogInformation("Ensured database object from {Script}.", Path.GetFileName(path));
        }
    }
}
