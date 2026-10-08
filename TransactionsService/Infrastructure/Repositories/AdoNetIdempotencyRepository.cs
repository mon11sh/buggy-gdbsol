// CONCEPT: ADO.NET (SqlConnection/SqlCommand/SqlParameter/SqlDataReader)
// Hand-written SQL data-access path for IIdempotencyRepository that MIRRORS the EF Core
// IdempotencyRepository. Selected when Settings.DataAccess == "AdoNet" (SQL Server only).
// TryReserveKeyAsync relies on the primary-key uniqueness of `key` to detect a concurrent insert.
using System.Data;
using Microsoft.Data.SqlClient;
using TransactionsService.Config;
using TransactionsService.Infrastructure.Data;

namespace TransactionsService.Infrastructure.Repositories;

public class AdoNetIdempotencyRepository : IIdempotencyRepository
{
    private readonly string _connectionString;

    public AdoNetIdempotencyRepository(Settings settings)
    {
        _connectionString = AdoNetSupport.BuildConnectionString(settings);
    }

    public async Task<IdempotencyKeyEntity?> GetKeyAsync(string key, CancellationToken ct = default)
    {
        // CONCEPT: raw SQL — single-row lookup by the primary key `key`.
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(
            "SELECT [key], response_body, status_code, created_at FROM idempotency_keys WHERE [key] = @key", connection);
        command.Parameters.Add(new SqlParameter("@key", SqlDbType.NVarChar, 255) { Value = key });

        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new IdempotencyKeyEntity
        {
            Key = reader.GetString(reader.GetOrdinal("key")),
            ResponseBody = reader.GetString(reader.GetOrdinal("response_body")),
            StatusCode = reader.GetInt32(reader.GetOrdinal("status_code")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at"))
        };
    }

    public async Task<bool> TryReserveKeyAsync(string key, CancellationToken ct = default)
    {
        // CONCEPT: raw SQL — optimistic reservation. A PK/unique violation (2627/2601) means another
        // request already reserved the key, so return false (mirrors the EF DbUpdateException catch).
        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(ct);
            await using var command = new SqlCommand(
                @"INSERT INTO idempotency_keys ([key], response_body, status_code, created_at)
                  VALUES (@key, @response_body, @status_code, @created_at)", connection);
            command.Parameters.Add(new SqlParameter("@key", SqlDbType.NVarChar, 255) { Value = key });
            command.Parameters.Add(new SqlParameter("@response_body", SqlDbType.NVarChar, -1) { Value = "PROCESSING" });
            command.Parameters.Add(new SqlParameter("@status_code", SqlDbType.Int) { Value = 202 });
            command.Parameters.Add(new SqlParameter("@created_at", SqlDbType.DateTime2) { Value = DateTime.UtcNow });
            await command.ExecuteNonQueryAsync(ct);
            return true;
        }
        catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
        {
            return false; // Unique constraint violation
        }
    }

    public async Task CompleteKeyAsync(string key, string responseBody, int statusCode, CancellationToken ct = default)
    {
        // CONCEPT: raw SQL — finalise a reserved key with the real response/status.
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(
            "UPDATE idempotency_keys SET response_body = @response_body, status_code = @status_code WHERE [key] = @key", connection);
        command.Parameters.Add(new SqlParameter("@response_body", SqlDbType.NVarChar, -1) { Value = responseBody });
        command.Parameters.Add(new SqlParameter("@status_code", SqlDbType.Int) { Value = statusCode });
        command.Parameters.Add(new SqlParameter("@key", SqlDbType.NVarChar, 255) { Value = key });
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task ReleaseKeyAsync(string key, CancellationToken ct = default)
    {
        // CONCEPT: raw SQL — remove a reservation so the request can be retried.
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(
            "DELETE FROM idempotency_keys WHERE [key] = @key", connection);
        command.Parameters.Add(new SqlParameter("@key", SqlDbType.NVarChar, 255) { Value = key });
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<int> ReleaseStaleReservationsAsync(DateTime olderThanUtc, CancellationToken ct = default)
    {
        // CONCEPT: raw SQL — sweep orphaned reservations (mirrors the EF ExecuteDelete).
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(
            "DELETE FROM idempotency_keys WHERE status_code = 202 AND created_at < @cutoff", connection);
        command.Parameters.Add(new SqlParameter("@cutoff", SqlDbType.DateTime2) { Value = olderThanUtc });
        return await command.ExecuteNonQueryAsync(ct);
    }
}
