// CONCEPT: ADO.NET (SqlConnection/SqlCommand/SqlParameter/SqlDataReader)
// Hand-written SQL data-access path for ITransferLimitRepository that MIRRORS the EF Core
// TransferLimitRepository. Selected when Settings.DataAccess == "AdoNet" (SQL Server only).
// GetDailyUsedAmountAsync is served by a STORED PROCEDURE (usp_TransferDailyStats); the rest use raw SQL.
using System.Data;
using Microsoft.Data.SqlClient;
using TransactionsService.Config;
using TransactionsService.Infrastructure.Data;

namespace TransactionsService.Infrastructure.Repositories;

public class AdoNetTransferLimitRepository : ITransferLimitRepository
{
    private readonly string _connectionString;

    public AdoNetTransferLimitRepository(Settings settings)
    {
        _connectionString = AdoNetSupport.BuildConnectionString(settings);
    }

    public async Task<TransferLimitEntity?> GetLimitByPrivilegeAsync(string privilege, CancellationToken ct = default)
    {
        // CONCEPT: raw SQL — single-row lookup by privilege (the primary key).
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(
            "SELECT privilege, daily_limit, per_transaction_limit FROM transfer_limits WHERE privilege = @privilege", connection);
        command.Parameters.Add(new SqlParameter("@privilege", SqlDbType.NVarChar, 50) { Value = privilege });

        TransferLimitEntity? limit;
        await using (var reader = await command.ExecuteReaderAsync(ct))
            limit = await reader.ReadAsync(ct) ? MapLimit(reader) : null;
        if (limit != null)
            limit.AllowedModes = await LoadModesAsync(connection, limit.Privilege, ct);
        return limit;
    }

    /// <summary>Many-to-many by hand: the join table transfer_limit_modes joined to transfer_modes.</summary>
    private static async Task<List<TransferModeEntity>> LoadModesAsync(SqlConnection connection, string privilege, CancellationToken ct)
    {
        await using var command = new SqlCommand(
            @"SELECT m.mode, m.description FROM transfer_limit_modes lm
              JOIN transfer_modes m ON m.mode = lm.mode
              WHERE lm.privilege = @privilege ORDER BY m.mode", connection);
        command.Parameters.Add(new SqlParameter("@privilege", SqlDbType.NVarChar, 50) { Value = privilege });
        var modes = new List<TransferModeEntity>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            modes.Add(new TransferModeEntity
            {
                Mode = reader.GetString(reader.GetOrdinal("mode")),
                Description = reader.IsDBNull(reader.GetOrdinal("description")) ? null : reader.GetString(reader.GetOrdinal("description")),
            });
        return modes;
    }

    public async Task<List<TransferLimitEntity>> GetAllLimitsAsync(CancellationToken ct = default)
    {
        // CONCEPT: raw SQL — full table scan (small reference table).
        var results = new List<TransferLimitEntity>();
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(
            "SELECT privilege, daily_limit, per_transaction_limit FROM transfer_limits", connection);

        await using (var reader = await command.ExecuteReaderAsync(ct))
            while (await reader.ReadAsync(ct))
                results.Add(MapLimit(reader));
        foreach (var limit in results)
            limit.AllowedModes = await LoadModesAsync(connection, limit.Privilege, ct);
        return results;
    }

    public async Task<TransferLimitEntity> CreateLimitAsync(string privilege, decimal dailyLimit, decimal perTransactionLimit, CancellationToken ct = default)
    {
        // CONCEPT: raw SQL — INSERT of a new privilege limit row.
        var limit = new TransferLimitEntity
        {
            Privilege = privilege,
            DailyLimit = dailyLimit,
            PerTransactionLimit = perTransactionLimit
        };

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(
            @"INSERT INTO transfer_limits (privilege, daily_limit, per_transaction_limit)
              VALUES (@privilege, @daily_limit, @per_transaction_limit)", connection);
        command.Parameters.Add(new SqlParameter("@privilege", SqlDbType.NVarChar, 50) { Value = limit.Privilege });
        command.Parameters.Add(new SqlParameter("@daily_limit", SqlDbType.Decimal) { Precision = 18, Scale = 2, Value = limit.DailyLimit });
        command.Parameters.Add(new SqlParameter("@per_transaction_limit", SqlDbType.Decimal) { Precision = 18, Scale = 2, Value = limit.PerTransactionLimit });
        await command.ExecuteNonQueryAsync(ct);
        return limit;
    }

    public async Task<TransferLimitEntity?> UpdateLimitAsync(string privilege, decimal dailyLimit, decimal perTransactionLimit, CancellationToken ct = default)
    {
        // CONCEPT: raw SQL — UPDATE existing row; return null when the privilege does not exist
        // (mirrors the EF repo: it fetches first and only saves when found).
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(
            @"UPDATE transfer_limits SET daily_limit = @daily_limit, per_transaction_limit = @per_transaction_limit
              WHERE privilege = @privilege", connection);
        command.Parameters.Add(new SqlParameter("@daily_limit", SqlDbType.Decimal) { Precision = 18, Scale = 2, Value = dailyLimit });
        command.Parameters.Add(new SqlParameter("@per_transaction_limit", SqlDbType.Decimal) { Precision = 18, Scale = 2, Value = perTransactionLimit });
        command.Parameters.Add(new SqlParameter("@privilege", SqlDbType.NVarChar, 50) { Value = privilege });

        var rows = await command.ExecuteNonQueryAsync(ct);
        if (rows == 0) return null;

        return new TransferLimitEntity
        {
            Privilege = privilege,
            DailyLimit = dailyLimit,
            PerTransactionLimit = perTransactionLimit
        };
    }

    public async Task<decimal> GetDailyUsedAmountAsync(int accountNumber, DateTime? date = null, CancellationToken ct = default)
    {
        // CONCEPT: stored procedure — usp_TransferDailyStats aggregates the day's transfers for the
        // source account and returns (TotalAmount, TransferCount). Here we read TotalAmount.
        var targetDate = (date ?? DateTime.UtcNow).Date;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand("usp_TransferDailyStats", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.Add(new SqlParameter("@AccountNumber", SqlDbType.Int) { Value = accountNumber });
        command.Parameters.Add(new SqlParameter("@Date", SqlDbType.Date) { Value = targetDate });

        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return 0m;
        var ordinal = reader.GetOrdinal("TotalAmount");
        return reader.IsDBNull(ordinal) ? 0m : reader.GetDecimal(ordinal);
    }

    public async Task<int> GetDailyTransactionCountAsync(int accountNumber, DateTime? date = null, CancellationToken ct = default)
    {
        // CONCEPT: raw SQL — COUNT of the day's transfers for the source account (matches the EF query).
        var targetDate = (date ?? DateTime.UtcNow).Date;
        var start = targetDate;
        var end = start.AddDays(1);

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(
            @"SELECT COUNT(*) FROM fund_transfers
              WHERE source_account_id = @accountNumber AND created_at >= @start AND created_at < @end", connection);
        command.Parameters.Add(new SqlParameter("@accountNumber", SqlDbType.Int) { Value = accountNumber });
        command.Parameters.Add(new SqlParameter("@start", SqlDbType.DateTime2) { Value = start });
        command.Parameters.Add(new SqlParameter("@end", SqlDbType.DateTime2) { Value = end });

        var result = await command.ExecuteScalarAsync(ct);
        return result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
    }

    private static TransferLimitEntity MapLimit(SqlDataReader reader) => new()
    {
        Privilege = reader.GetString(reader.GetOrdinal("privilege")),
        DailyLimit = reader.GetDecimal(reader.GetOrdinal("daily_limit")),
        PerTransactionLimit = reader.GetDecimal(reader.GetOrdinal("per_transaction_limit"))
    };
}
