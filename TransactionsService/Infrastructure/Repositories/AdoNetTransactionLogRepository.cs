// CONCEPT: ADO.NET (SqlConnection/SqlCommand/SqlParameter/SqlDataReader)
// Hand-written SQL data-access path for ITransactionLogRepository that MIRRORS the EF Core
// TransactionLogRepository (transaction_logging + fund_transfers reads, plus file logging).
// Selected when Settings.DataAccess == "AdoNet" (SQL Server only). EF Core remains the default.
using System.Data;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using TransactionsService.Config;
using TransactionsService.Domain.Models;
using TransactionsService.Infrastructure.Data;
using TransactionsService.Infrastructure.Audit;

namespace TransactionsService.Infrastructure.Repositories;

public class AdoNetTransactionLogRepository : ITransactionLogRepository
{
    private readonly string _connectionString;
    private readonly TransactionAuditFileWriter _audit;

    public AdoNetTransactionLogRepository(Settings settings, TransactionAuditFileWriter audit)
    {
        _connectionString = AdoNetSupport.BuildConnectionString(settings);
        _audit = audit;
    }

    public async Task<TransactionLogEntity> LogTransactionAsync(int accountId, TransactionType type, decimal amount, decimal balanceAfter, string? referenceId, string? description, CancellationToken ct = default, int? transferId = null)
    {
        var log = TransactionLogEntity.Create(type);
        log.AccountId = accountId;
        log.Amount = amount;
        log.BalanceAfter = balanceAfter;
        log.ReferenceId = referenceId;
        log.Description = description;
        log.CreatedAt = DateTime.UtcNow;
        if (log is TransferLegEntity leg) leg.TransferId = transferId;

        // CONCEPT: raw SQL — parameterised INSERT with OUTPUT INSERTED.id (transaction_type stored as
        // its string name, matching EF's HasConversion<string>()).
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(
            @"INSERT INTO transaction_logging
                (account_id, transaction_type, amount, balance_after, reference_id, description, created_at, transfer_id)
              OUTPUT INSERTED.id
              VALUES
                (@account_id, @transaction_type, @amount, @balance_after, @reference_id, @description, @created_at, @transfer_id)",
            connection);
        command.Parameters.Add(new SqlParameter("@account_id", SqlDbType.Int) { Value = log.AccountId });
        command.Parameters.Add(new SqlParameter("@transaction_type", SqlDbType.NVarChar, 20) { Value = log.TransactionType.ToString() });
        command.Parameters.Add(new SqlParameter("@amount", SqlDbType.Decimal) { Precision = 18, Scale = 2, Value = log.Amount });
        command.Parameters.Add(new SqlParameter("@balance_after", SqlDbType.Decimal) { Precision = 18, Scale = 2, Value = log.BalanceAfter });
        command.Parameters.Add(new SqlParameter("@reference_id", SqlDbType.NVarChar, -1) { Value = (object?)log.ReferenceId ?? DBNull.Value });
        command.Parameters.Add(new SqlParameter("@description", SqlDbType.NVarChar, -1) { Value = (object?)log.Description ?? DBNull.Value });
        command.Parameters.Add(new SqlParameter("@created_at", SqlDbType.DateTime2) { Value = log.CreatedAt });
        command.Parameters.Add(new SqlParameter("@transfer_id", SqlDbType.Int) { Value = (object?)(log as TransferLegEntity)?.TransferId ?? DBNull.Value });

        log.Id = Convert.ToInt32(await command.ExecuteScalarAsync(ct));
        _audit.Enqueue(log);
        return log;
    }

    public async Task<List<TransactionLogEntity>> GetLogsAsync(int accountId, int skip = 0, int limit = 50, DateTime? startDate = null, DateTime? endDate = null, string? type = null, CancellationToken ct = default)
    {
        // CONCEPT: raw SQL — dynamic WHERE composed from optional filters, paged with OFFSET/FETCH.
        var sql = new StringBuilder(
            "SELECT id, account_id, transaction_type, amount, balance_after, reference_id, description, created_at, transfer_id " +
            "FROM transaction_logging WHERE account_id = @accountId");

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand { Connection = connection };
        command.Parameters.Add(new SqlParameter("@accountId", SqlDbType.Int) { Value = accountId });

        AppendDateFilters(sql, command, startDate, endDate);
        AppendTypeFilter(sql, command, type);

        sql.Append(" ORDER BY created_at DESC OFFSET @skip ROWS FETCH NEXT @limit ROWS ONLY");
        command.Parameters.Add(new SqlParameter("@skip", SqlDbType.Int) { Value = skip });
        command.Parameters.Add(new SqlParameter("@limit", SqlDbType.Int) { Value = limit });
        command.CommandText = sql.ToString();

        return await ReadLogsAsync(command, ct);
    }

    public async Task<List<TransactionLogEntity>> GetAllLogsAsync(int skip = 0, int limit = 50, string? type = null, DateTime? startDate = null, DateTime? endDate = null, CancellationToken ct = default)
    {
        // CONCEPT: raw SQL — same shape as GetLogsAsync without the account filter.
        var sql = new StringBuilder(
            "SELECT id, account_id, transaction_type, amount, balance_after, reference_id, description, created_at, transfer_id " +
            "FROM transaction_logging WHERE 1 = 1");

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand { Connection = connection };

        AppendDateFilters(sql, command, startDate, endDate);
        AppendTypeFilter(sql, command, type);

        sql.Append(" ORDER BY created_at DESC OFFSET @skip ROWS FETCH NEXT @limit ROWS ONLY");
        command.Parameters.Add(new SqlParameter("@skip", SqlDbType.Int) { Value = skip });
        command.Parameters.Add(new SqlParameter("@limit", SqlDbType.Int) { Value = limit });
        command.CommandText = sql.ToString();

        return await ReadLogsAsync(command, ct);
    }

    public async Task<List<FundTransferEntity>> GetAllTransfersAsync(int skip = 0, int limit = 50, DateTime? startDate = null, DateTime? endDate = null, CancellationToken ct = default)
    {
        // CONCEPT: raw SQL — paged fund_transfers feed (matches the EF query).
        var sql = new StringBuilder(
            "SELECT id, source_account_id, destination_account_id, amount, transfer_mode, status, failure_reason, created_at " +
            "FROM fund_transfers WHERE 1 = 1");

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand { Connection = connection };

        AppendDateFilters(sql, command, startDate, endDate);

        sql.Append(" ORDER BY created_at DESC OFFSET @skip ROWS FETCH NEXT @limit ROWS ONLY");
        command.Parameters.Add(new SqlParameter("@skip", SqlDbType.Int) { Value = skip });
        command.Parameters.Add(new SqlParameter("@limit", SqlDbType.Int) { Value = limit });
        command.CommandText = sql.ToString();

        var results = new List<FundTransferEntity>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(new FundTransferEntity
            {
                Id = reader.GetInt32(reader.GetOrdinal("id")),
                SourceAccountId = reader.GetInt32(reader.GetOrdinal("source_account_id")),
                DestinationAccountId = reader.GetInt32(reader.GetOrdinal("destination_account_id")),
                Amount = reader.GetDecimal(reader.GetOrdinal("amount")),
                TransferMode = Enum.Parse<TransferMode>(reader.GetString(reader.GetOrdinal("transfer_mode")), true),
                Status = reader.GetString(reader.GetOrdinal("status")),
                FailureReason = reader.IsDBNull(reader.GetOrdinal("failure_reason")) ? null : reader.GetString(reader.GetOrdinal("failure_reason")),
                CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at"))
            });
        }
        return results;
    }

    public async Task<TransactionLogEntity?> GetLogByIdAsync(int transactionId, CancellationToken ct = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(
            "SELECT id, account_id, transaction_type, amount, balance_after, reference_id, description, created_at, transfer_id " +
            "FROM transaction_logging WHERE id = @id", connection);
        command.Parameters.Add(new SqlParameter("@id", SqlDbType.Int) { Value = transactionId });

        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? MapLog(reader) : null;
    }

    // ---------------------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------------------
    private static void AppendDateFilters(StringBuilder sql, SqlCommand command, DateTime? startDate, DateTime? endDate)
    {
        if (startDate.HasValue)
        {
            sql.Append(" AND created_at >= @startDate");
            command.Parameters.Add(new SqlParameter("@startDate", SqlDbType.DateTime2) { Value = startDate.Value });
        }
        if (endDate.HasValue)
        {
            sql.Append(" AND created_at <= @endDate");
            command.Parameters.Add(new SqlParameter("@endDate", SqlDbType.DateTime2) { Value = endDate.Value });
        }
    }

    private static void AppendTypeFilter(StringBuilder sql, SqlCommand command, string? type)
    {
        if (!string.IsNullOrEmpty(type) && Enum.TryParse<TransactionType>(type, true, out var t))
        {
            sql.Append(" AND transaction_type = @type");
            command.Parameters.Add(new SqlParameter("@type", SqlDbType.NVarChar, 20) { Value = t.ToString() });
        }
    }

    private static async Task<List<TransactionLogEntity>> ReadLogsAsync(SqlCommand command, CancellationToken ct = default)
    {
        var results = new List<TransactionLogEntity>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            results.Add(MapLog(reader));
        return results;
    }

    public async Task BulkInsertAsync(IReadOnlyList<TransactionLogEntity> logs, CancellationToken ct = default)
    {
        if (logs.Count == 0) return;

        // CONCEPT: SqlBulkCopy - streams rows straight into the table with the bulk-load protocol
        // (no per-row INSERT statements). Column mappings are explicit so a table change fails loudly.
        var table = new DataTable();
        table.Columns.Add("account_id", typeof(int));
        table.Columns.Add("transaction_type", typeof(string));
        table.Columns.Add("amount", typeof(decimal));
        table.Columns.Add("balance_after", typeof(decimal));
        table.Columns.Add("reference_id", typeof(string));
        table.Columns.Add("description", typeof(string));
        table.Columns.Add("created_at", typeof(DateTime));
        table.Columns.Add("transfer_id", typeof(int));
        foreach (var log in logs)
            table.Rows.Add(log.AccountId, log.TransactionType.ToString(), log.Amount, log.BalanceAfter,
                (object?)log.ReferenceId ?? DBNull.Value, (object?)log.Description ?? DBNull.Value, log.CreatedAt, (object?)(log as TransferLegEntity)?.TransferId ?? DBNull.Value);

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        using var bulk = new SqlBulkCopy(connection) { DestinationTableName = "transaction_logging", BatchSize = 1000 };
        foreach (DataColumn column in table.Columns)
            bulk.ColumnMappings.Add(column.ColumnName, column.ColumnName);
        await bulk.WriteToServerAsync(table, ct);
    }

    private static TransactionLogEntity MapLog(SqlDataReader reader)
    {
        // The discriminator column decides the CLR subtype - the hand-written equivalent of EF's TPH materialisation.
        var log = TransactionLogEntity.Create(Enum.Parse<TransactionType>(reader.GetString(reader.GetOrdinal("transaction_type")), true));
        log.Id = reader.GetInt32(reader.GetOrdinal("id"));
        log.AccountId = reader.GetInt32(reader.GetOrdinal("account_id"));
        log.Amount = reader.GetDecimal(reader.GetOrdinal("amount"));
        log.BalanceAfter = reader.GetDecimal(reader.GetOrdinal("balance_after"));
        log.ReferenceId = reader.IsDBNull(reader.GetOrdinal("reference_id")) ? null : reader.GetString(reader.GetOrdinal("reference_id"));
        log.Description = reader.IsDBNull(reader.GetOrdinal("description")) ? null : reader.GetString(reader.GetOrdinal("description"));
        log.CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at"));
        if (log is TransferLegEntity leg && !reader.IsDBNull(reader.GetOrdinal("transfer_id")))
            leg.TransferId = reader.GetInt32(reader.GetOrdinal("transfer_id"));
        return log;
    }
}
