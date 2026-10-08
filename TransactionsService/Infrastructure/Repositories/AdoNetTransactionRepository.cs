// CONCEPT: ADO.NET (SqlConnection/SqlCommand/SqlParameter/SqlDataReader)
// Hand-written SQL data-access path for ITransactionRepository that MIRRORS the behaviour of the
// EF Core TransactionRepository. Selected when Settings.DataAccess == "AdoNet" (SQL Server only).
// Domain objects are rebuilt with the SAME factory/mapper the EF path uses (FundTransfer.Create /
// TransactionMapper), so transitions run through the domain aggregate exactly as before.
using System.Data;
using Microsoft.Data.SqlClient;
using TransactionsService.Config;
using TransactionsService.Domain.Models;
using TransactionsService.Infrastructure.Data;
using TransactionsService.Mapping;

namespace TransactionsService.Infrastructure.Repositories;

public class AdoNetTransactionRepository : ITransactionRepository
{
    private readonly string _connectionString;

    public AdoNetTransactionRepository(Settings settings)
    {
        _connectionString = AdoNetSupport.BuildConnectionString(settings);
    }

    public async Task<FundTransferEntity> CreateTransferRecordAsync(int sourceAccountId, int destAccountId, decimal amount, TransferMode mode, CancellationToken ct = default)
    {
        // Build the domain aggregate exactly like the EF repo, then persist via mapped columns.
        var domain = FundTransfer.Create(new AccountId(sourceAccountId), new AccountId(destAccountId), new Money(amount), mode);
        var entity = TransactionMapper.ToEntity(domain);

        // CONCEPT: raw SQL — parameterised INSERT with OUTPUT INSERTED.id to capture the identity.
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(
            @"INSERT INTO fund_transfers
                (source_account_id, destination_account_id, amount, transfer_mode, status, failure_reason, created_at)
              OUTPUT INSERTED.id
              VALUES
                (@source_account_id, @destination_account_id, @amount, @transfer_mode, @status, @failure_reason, @created_at)",
            connection);
        command.Parameters.Add(new SqlParameter("@source_account_id", SqlDbType.Int) { Value = entity.SourceAccountId });
        command.Parameters.Add(new SqlParameter("@destination_account_id", SqlDbType.Int) { Value = entity.DestinationAccountId });
        command.Parameters.Add(new SqlParameter("@amount", SqlDbType.Decimal) { Precision = 18, Scale = 2, Value = entity.Amount });
        command.Parameters.Add(new SqlParameter("@transfer_mode", SqlDbType.NVarChar, 10) { Value = entity.TransferMode.ToString() });
        command.Parameters.Add(new SqlParameter("@status", SqlDbType.NVarChar, 20) { Value = entity.Status });
        command.Parameters.Add(new SqlParameter("@failure_reason", SqlDbType.NVarChar, -1) { Value = (object?)entity.FailureReason ?? DBNull.Value });
        command.Parameters.Add(new SqlParameter("@created_at", SqlDbType.DateTime2) { Value = entity.CreatedAt });

        var newId = Convert.ToInt32(await command.ExecuteScalarAsync(ct));
        entity.Id = newId;
        return entity;
    }

    public async Task UpdateTransferStatusAsync(int transferId, TransferStatus status, string? failureReason = null, CancellationToken ct = default)
    {
        // CONCEPT: raw SQL — load the row, drive the transition through the domain aggregate (invariants
        // + base status kept in sync), then persist the new status/failure reason. No-op when missing,
        // mirroring the EF repo's `if (transfer != null)` guard.
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        FundTransferEntity? existing;
        await using (var load = new SqlCommand(
            @"SELECT id, source_account_id, destination_account_id, amount, transfer_mode, status, failure_reason, created_at
              FROM fund_transfers WHERE id = @id", connection))
        {
            load.Parameters.Add(new SqlParameter("@id", SqlDbType.Int) { Value = transferId });
            await using var reader = await load.ExecuteReaderAsync(ct);
            existing = await reader.ReadAsync(ct) ? MapTransfer(reader) : null;
        }

        if (existing == null) return;

        var domain = TransactionMapper.ToDomain(existing);
        switch (status)
        {
            case TransferStatus.COMPLETED: domain.MarkCompleted(); break;
            case TransferStatus.FAILED: domain.MarkFailed(failureReason ?? "Transfer failed"); break;
            case TransferStatus.PROCESSING: domain.MarkProcessing(); break;
            case TransferStatus.COMPENSATION_FAILED: domain.MarkCompensationFailed(failureReason ?? "Compensation failed"); break;
        }

        var updated = TransactionMapper.ToEntity(domain);
        await using var update = new SqlCommand(
            @"UPDATE fund_transfers SET status = @status, failure_reason = @failure_reason WHERE id = @id", connection);
        update.Parameters.Add(new SqlParameter("@status", SqlDbType.NVarChar, 20) { Value = updated.Status });
        update.Parameters.Add(new SqlParameter("@failure_reason", SqlDbType.NVarChar, -1) { Value = (object?)updated.FailureReason ?? DBNull.Value });
        update.Parameters.Add(new SqlParameter("@id", SqlDbType.Int) { Value = transferId });
        await update.ExecuteNonQueryAsync(ct);
    }

    public async Task<decimal> GetDailyTransferTotalAsync(int accountId, CancellationToken ct = default)
    {
        // CONCEPT: raw SQL — SUM of today's COMPLETED transfers for the source account (matches the EF query).
        var today = DateTime.UtcNow.Date;
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        // CONCEPT: SQL scalar FUNCTION - ufn_DailyTransferTotal encapsulates the day-window + status rule and composes inside SELECT.
        await using var command = new SqlCommand("SELECT dbo.ufn_DailyTransferTotal(@accountId, @today)", connection);
        command.Parameters.Add(new SqlParameter("@accountId", SqlDbType.Int) { Value = accountId });
        command.Parameters.Add(new SqlParameter("@today", SqlDbType.Date) { Value = today });

        var result = await command.ExecuteScalarAsync(ct);
        return result == null || result == DBNull.Value ? 0m : Convert.ToDecimal(result);
    }

    public async Task<FundTransferEntity?> GetTransferWithLegsAsync(int transferId, CancellationToken ct = default)
    {
        // Two statements on one connection (the ADO.NET equivalent of a split query): no JOIN row duplication.
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        FundTransferEntity? transfer;
        await using (var head = new SqlCommand(
            @"SELECT id, source_account_id, destination_account_id, amount, transfer_mode, status, failure_reason, created_at
              FROM fund_transfers WHERE id = @id", connection))
        {
            head.Parameters.Add(new SqlParameter("@id", SqlDbType.Int) { Value = transferId });
            await using var reader = await head.ExecuteReaderAsync(ct);
            transfer = await reader.ReadAsync(ct) ? MapTransfer(reader) : null;
        }
        if (transfer == null) return null;

        await LoadLegsAsync(transfer, ct);
        return transfer;
    }

    public async Task LoadLegsAsync(FundTransferEntity transfer, CancellationToken ct = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(
            @"SELECT id, account_id, transaction_type, amount, balance_after, reference_id, description, created_at, transfer_id
              FROM transaction_logging WHERE transfer_id = @id ORDER BY id", connection);
        command.Parameters.Add(new SqlParameter("@id", SqlDbType.Int) { Value = transfer.Id });

        var legs = new List<TransferLegEntity>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            legs.Add(new TransferLegEntity
            {
                Id = reader.GetInt32(reader.GetOrdinal("id")),
                AccountId = reader.GetInt32(reader.GetOrdinal("account_id")),
                Amount = reader.GetDecimal(reader.GetOrdinal("amount")),
                BalanceAfter = reader.GetDecimal(reader.GetOrdinal("balance_after")),
                ReferenceId = reader.IsDBNull(reader.GetOrdinal("reference_id")) ? null : reader.GetString(reader.GetOrdinal("reference_id")),
                Description = reader.IsDBNull(reader.GetOrdinal("description")) ? null : reader.GetString(reader.GetOrdinal("description")),
                CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                TransferId = transfer.Id,
            });
        }
        transfer.Legs = legs;
    }

    public async Task<List<FundTransferEntity>> GetStaleTransfersAsync(TransferStatus status, DateTime olderThanUtc, int limit, CancellationToken ct = default)
    {
        // CONCEPT: raw SQL — reconciliation input: rows still in @status created before @cutoff (matches the EF query).
        var results = new List<FundTransferEntity>();
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(
            @"SELECT TOP (@limit) id, source_account_id, destination_account_id, amount, transfer_mode, status, failure_reason, created_at
              FROM fund_transfers
              WHERE status = @status AND created_at < @cutoff
              ORDER BY created_at", connection);
        command.Parameters.Add(new SqlParameter("@limit", SqlDbType.Int) { Value = limit });
        command.Parameters.Add(new SqlParameter("@status", SqlDbType.NVarChar, 20) { Value = status.ToString() });
        command.Parameters.Add(new SqlParameter("@cutoff", SqlDbType.DateTime2) { Value = olderThanUtc });

        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            results.Add(MapTransfer(reader));
        return results;
    }

    private static FundTransferEntity MapTransfer(SqlDataReader reader) => new()
    {
        Id = reader.GetInt32(reader.GetOrdinal("id")),
        SourceAccountId = reader.GetInt32(reader.GetOrdinal("source_account_id")),
        DestinationAccountId = reader.GetInt32(reader.GetOrdinal("destination_account_id")),
        Amount = reader.GetDecimal(reader.GetOrdinal("amount")),
        TransferMode = Enum.Parse<TransferMode>(reader.GetString(reader.GetOrdinal("transfer_mode")), true),
        Status = reader.GetString(reader.GetOrdinal("status")),
        FailureReason = reader.IsDBNull(reader.GetOrdinal("failure_reason")) ? null : reader.GetString(reader.GetOrdinal("failure_reason")),
        CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at"))
    };
}
