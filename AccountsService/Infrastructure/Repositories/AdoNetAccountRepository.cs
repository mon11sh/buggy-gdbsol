// CONCEPT: ADO.NET (SqlConnection/SqlCommand/SqlDataReader)
// A runtime-toggleable, hand-written SQL data-access path for AccountsService that MIRRORS the
// behaviour of the EF Core AccountRepository. Selected when Settings.DataAccess == "AdoNet"
// (SQL Server only). EF Core remains the default. Domain objects are rebuilt with the SAME
// factory/restore methods the EF mapper uses (SavingsAccount.RestoreSavings / CurrentAccount.RestoreCurrent).
using System.Data;
using AccountsService.Config;
using AccountsService.Domain.Enums;
using AccountsService.Domain.Exceptions;
using AccountsService.Domain.Models;
using AccountsService.DTOs;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace AccountsService.Infrastructure.Repositories;

public class AdoNetAccountRepository : IAccountRepository
{
    private readonly string _connectionString;
    private readonly ILogger<AdoNetAccountRepository> _logger;

    // Base projection used by every read. Details columns are LEFT-joined and aliased so the
    // ambiguous account_number/created_at/updated_at columns never collide across the 3 tables.
    private const string SelectColumns = @"
        a.account_number, a.account_type, a.name, a.privilege, a.pin_hash, a.balance,
        a.bank_name, a.bank_branch, a.ifsc_code, a.is_active, a.activated_date, a.closed_date,
        s.date_of_birth   AS s_date_of_birth,
        s.gender          AS s_gender,
        s.phone_no        AS s_phone_no,
        s.aadhar_number   AS s_aadhar_number,
        s.aadhar_hash     AS s_aadhar_hash,
        c.company_name    AS c_company_name,
        c.website         AS c_website,
        c.registration_no AS c_registration_no";

    private const string FromJoins = @"
        FROM accounts a
        LEFT JOIN savings_account_details s ON s.account_number = a.account_number
        LEFT JOIN current_account_details c ON c.account_number = a.account_number";

    public AdoNetAccountRepository(Settings settings, ILogger<AdoNetAccountRepository> logger)
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
    // Startup helper: create/refresh the usp_AccountSummary stored procedure (idempotent).
    // ---------------------------------------------------------------------------------------
    public static async Task EnsureStoredProceduresAsync(Settings settings, ILogger logger, CancellationToken ct = default)
    {
        var connectionString = BuildConnectionString(settings);
        var sqlPath = Path.Combine(AppContext.BaseDirectory,
            "Infrastructure", "Data", "StoredProcedures", "usp_AccountSummary.sql");

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
        logger.LogInformation("Ensured stored procedure usp_AccountSummary (CREATE OR ALTER).");
    }

    // ---------------------------------------------------------------------------------------
    // Reads
    // ---------------------------------------------------------------------------------------
    public async Task<int> GetNextAccountNumberAsync(CancellationToken ct = default)
    {
        // Raw SQL: MAX(account_number). Mirror the EF repo's assignment: return MAX+1 only when
        // an account >= 1000 already exists, otherwise start the sequence at 1000.
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(
            "SELECT MAX(account_number) FROM accounts", connection);

        var result = await command.ExecuteScalarAsync(ct);
        if (result == null || result == DBNull.Value)
            return 1000;

        var max = Convert.ToInt32(result);
        return max >= 1000 ? max + 1 : 1000;
    }

    public async Task<Account?> GetByAccountNumberAsync(int accountNumber, CancellationToken ct = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(
            $"SELECT {SelectColumns} {FromJoins} WHERE a.account_number = @accountNumber", connection);
        command.Parameters.Add(new SqlParameter("@accountNumber", SqlDbType.Int) { Value = accountNumber });

        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return MapAccount(reader, includeDetails: true);
    }

    public async Task<Account?> GetByAadharHashAsync(string aadharHash, CancellationToken ct = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(
            $"SELECT {SelectColumns} {FromJoins} WHERE s.aadhar_hash = @aadharHash", connection);
        command.Parameters.Add(new SqlParameter("@aadharHash", SqlDbType.NVarChar, 64) { Value = aadharHash });

        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return MapAccount(reader, includeDetails: true);
    }

    public async Task<Account?> GetByRegistrationNoAsync(string registrationNo, CancellationToken ct = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(
            $"SELECT {SelectColumns} {FromJoins} WHERE c.registration_no = @registrationNo", connection);
        command.Parameters.Add(new SqlParameter("@registrationNo", SqlDbType.NVarChar, 50) { Value = registrationNo });

        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return MapAccount(reader, includeDetails: true);
    }

    public async Task<List<Account>> GetAllAsync(string? accountType = null, CancellationToken ct = default)
    {
        // CONCEPT: 'in' parameter — pass the readonly filter struct by reference to build the WHERE clause.
        var query = new AccountQuery(accountType);
        var whereClause = BuildWhereClause(in query);

        var results = new List<Account>();
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(
            $"SELECT {SelectColumns} {FromJoins} {whereClause} ORDER BY a.account_number", connection);

        if (!string.IsNullOrEmpty(accountType))
            command.Parameters.Add(new SqlParameter("@accountType", SqlDbType.NVarChar, 10) { Value = accountType });

        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            // CONCEPT: method overloading — the single-argument overload is used here.
            results.Add(MapAccount(reader));
        }
        return results;
    }

    public async Task<List<Account>> SearchAsync(string? nameContains, string? privilege, int limit, CancellationToken ct = default)
    {
        // CONCEPT: raw SQL search - LIKE with a parameterised pattern (never string-concatenated) and TOP for the cap.
        var where = new List<string>();
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand { Connection = connection };
        if (!string.IsNullOrWhiteSpace(nameContains))
        {
            where.Add("UPPER(a.name) LIKE @pattern");
            command.Parameters.Add(new SqlParameter("@pattern", SqlDbType.NVarChar, 260) { Value = $"%{nameContains.Trim().ToUpperInvariant()}%" });
        }
        if (!string.IsNullOrWhiteSpace(privilege))
        {
            where.Add("a.privilege = @privilege");
            command.Parameters.Add(new SqlParameter("@privilege", SqlDbType.NVarChar, 20) { Value = privilege.ToUpperInvariant() });
        }
        command.Parameters.Add(new SqlParameter("@limit", SqlDbType.Int) { Value = limit });
        var whereSql = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "";
        command.CommandText = $"SELECT TOP (@limit) {SelectColumns} {FromJoins} {whereSql} ORDER BY a.account_number";

        var results = new List<Account>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            results.Add(MapAccount(reader, includeDetails: true));
        return results;
    }

    public async Task<AccountSummaryResponse> GetSummaryAsync(CancellationToken ct = default)
    {
        // CONCEPT: call a stored procedure via CommandType.StoredProcedure.
        var response = new AccountSummaryResponse();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand("usp_AccountSummary", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        await using var reader = await command.ExecuteReaderAsync(ct);

        // Result set 1: headline scalars
        if (await reader.ReadAsync(ct))
        {
            response.TotalAccounts = reader.GetInt32(reader.GetOrdinal("TotalAccounts"));
            response.TotalBalance = reader.GetDecimal(reader.GetOrdinal("TotalBalance"));
            response.ActiveAccounts = reader.GetInt32(reader.GetOrdinal("ActiveAccounts"));
        }

        // Result set 2: by type
        if (await reader.NextResultAsync(ct))
        {
            while (await reader.ReadAsync(ct))
                response.ByType[reader.GetString(reader.GetOrdinal("AccountType"))] =
                    reader.GetInt32(reader.GetOrdinal("Cnt"));
        }

        // Result set 3: by privilege
        if (await reader.NextResultAsync(ct))
        {
            while (await reader.ReadAsync(ct))
                response.ByPrivilege[reader.GetString(reader.GetOrdinal("Privilege"))] =
                    reader.GetInt32(reader.GetOrdinal("Cnt"));
        }

        // Result set 4: recent accounts
        if (await reader.NextResultAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                response.RecentAccounts.Add(new RecentAccountDto
                {
                    AccountNumber = reader.GetInt32(reader.GetOrdinal("account_number")),
                    Name = reader.GetString(reader.GetOrdinal("name")),
                    AccountType = reader.GetString(reader.GetOrdinal("account_type"))
                });
            }
        }

        return response;
    }

    // ---------------------------------------------------------------------------------------
    // Writes
    // ---------------------------------------------------------------------------------------
    public async Task<Account> SaveAsync(Account account, CancellationToken ct = default)
    {
        if (account.AccountNumber == null)
        {
            var nextNum = await GetNextAccountNumberAsync(ct);
            account.AssignNumber(new AccountId(nextNum));
            await InsertAsync(account, ct);
        }
        else
        {
            await UpdateAsync(account, ct);
        }
        return account;
    }

    private async Task InsertAsync(Account account, CancellationToken ct = default)
    {
        var (isActive, closedDate) = MapStatus(account);

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(ct);
        try
        {
            var now = DateTime.UtcNow;
            await using (var insertAccount = new SqlCommand(
                @"INSERT INTO accounts
                    (account_number, account_type, name, pin_hash, balance, privilege,
                     bank_name, bank_branch, ifsc_code, is_active, activated_date, closed_date,
                     created_at, updated_at, row_version)
                  VALUES
                    (@account_number, @account_type, @name, @pin_hash, @balance, @privilege,
                     @bank_name, @bank_branch, @ifsc_code, @is_active, @activated_date, @closed_date,
                     @created_at, @updated_at, @row_version)", connection, transaction))
            {
                insertAccount.Parameters.Add(new SqlParameter("@account_number", SqlDbType.Int) { Value = account.AccountNumber!.Value });
                insertAccount.Parameters.Add(new SqlParameter("@account_type", SqlDbType.NVarChar, 10) { Value = account.AccountType });
                insertAccount.Parameters.Add(new SqlParameter("@name", SqlDbType.NVarChar, 255) { Value = account.Name });
                insertAccount.Parameters.Add(new SqlParameter("@pin_hash", SqlDbType.NVarChar, 255) { Value = account.PinHash });
                insertAccount.Parameters.Add(new SqlParameter("@balance", SqlDbType.Decimal) { Precision = 15, Scale = 2, Value = account.Balance.Amount });
                insertAccount.Parameters.Add(new SqlParameter("@privilege", SqlDbType.NVarChar, 10) { Value = account.Privilege });
                insertAccount.Parameters.Add(new SqlParameter("@bank_name", SqlDbType.NVarChar, 255) { Value = account.BankDetails.Name });
                insertAccount.Parameters.Add(new SqlParameter("@bank_branch", SqlDbType.NVarChar, 255) { Value = account.BankDetails.Branch });
                insertAccount.Parameters.Add(new SqlParameter("@ifsc_code", SqlDbType.NVarChar, 20) { Value = account.BankDetails.IfscCode });
                insertAccount.Parameters.Add(new SqlParameter("@is_active", SqlDbType.Bit) { Value = isActive });
                insertAccount.Parameters.Add(new SqlParameter("@activated_date", SqlDbType.DateTime2) { Value = account.ActivatedDate });
                insertAccount.Parameters.Add(new SqlParameter("@closed_date", SqlDbType.DateTime2) { Value = (object?)closedDate ?? DBNull.Value });
                insertAccount.Parameters.Add(new SqlParameter("@created_at", SqlDbType.DateTime2) { Value = now });
                insertAccount.Parameters.Add(new SqlParameter("@updated_at", SqlDbType.DateTime2) { Value = now });
                insertAccount.Parameters.Add(new SqlParameter("@row_version", SqlDbType.UniqueIdentifier) { Value = Guid.NewGuid() });
                await insertAccount.ExecuteNonQueryAsync(ct);
            }

            if (account is SavingsAccount savings && savings.Details != null)
            {
                await using var insertSavings = new SqlCommand(
                    @"INSERT INTO savings_account_details
                        (account_number, date_of_birth, gender, phone_no, aadhar_number, aadhar_hash, created_at, updated_at)
                      VALUES
                        (@account_number, @date_of_birth, @gender, @phone_no, @aadhar_number, @aadhar_hash, @created_at, @updated_at)",
                    connection, transaction);
                insertSavings.Parameters.Add(new SqlParameter("@account_number", SqlDbType.Int) { Value = account.AccountNumber!.Value });
                insertSavings.Parameters.Add(new SqlParameter("@date_of_birth", SqlDbType.DateTime2) { Value = DateTime.Parse(savings.Details.DateOfBirth) });
                insertSavings.Parameters.Add(new SqlParameter("@gender", SqlDbType.NVarChar, 10) { Value = savings.Details.Gender });
                insertSavings.Parameters.Add(new SqlParameter("@phone_no", SqlDbType.NVarChar, 20) { Value = savings.Details.PhoneNumber });
                insertSavings.Parameters.Add(new SqlParameter("@aadhar_number", SqlDbType.NVarChar, 255) { Value = savings.Details.Aadhaar });
                insertSavings.Parameters.Add(new SqlParameter("@aadhar_hash", SqlDbType.NVarChar, 64) { Value = savings.Details.AadhaarHash });
                insertSavings.Parameters.Add(new SqlParameter("@created_at", SqlDbType.DateTime2) { Value = now });
                insertSavings.Parameters.Add(new SqlParameter("@updated_at", SqlDbType.DateTime2) { Value = now });
                await insertSavings.ExecuteNonQueryAsync(ct);
            }
            else if (account is CurrentAccount current && current.Details != null)
            {
                await using var insertCurrent = new SqlCommand(
                    @"INSERT INTO current_account_details
                        (account_number, company_name, website, registration_no, created_at, updated_at)
                      VALUES
                        (@account_number, @company_name, @website, @registration_no, @created_at, @updated_at)",
                    connection, transaction);
                insertCurrent.Parameters.Add(new SqlParameter("@account_number", SqlDbType.Int) { Value = account.AccountNumber!.Value });
                insertCurrent.Parameters.Add(new SqlParameter("@company_name", SqlDbType.NVarChar, 255) { Value = current.Details.AccountHolderName });
                insertCurrent.Parameters.Add(new SqlParameter("@website", SqlDbType.NVarChar, 255) { Value = (object?)current.Details.Website ?? DBNull.Value });
                insertCurrent.Parameters.Add(new SqlParameter("@registration_no", SqlDbType.NVarChar, 50) { Value = current.Details.RegistrationNumber });
                insertCurrent.Parameters.Add(new SqlParameter("@created_at", SqlDbType.DateTime2) { Value = now });
                insertCurrent.Parameters.Add(new SqlParameter("@updated_at", SqlDbType.DateTime2) { Value = now });
                await insertCurrent.ExecuteNonQueryAsync(ct);
            }

            await transaction.CommitAsync(ct);
        }
        catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
        {
            // 2627 = unique constraint / PK violation, 2601 = unique index violation.
            await transaction.RollbackAsync();
            throw new AccountException("Duplicate constraint violation", "DUPLICATE_ERROR");
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private async Task UpdateAsync(Account account, CancellationToken ct = default)
    {
        var (isActive, closedDate) = MapStatus(account);

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(ct);
        try
        {
            int rows;
            await using (var updateAccount = new SqlCommand(
                @"UPDATE accounts
                     SET name = @name, privilege = @privilege, balance = @balance,
                         is_active = @is_active, closed_date = @closed_date, pin_hash = @pin_hash,
                         updated_at = @updated_at, row_version = @row_version
                   WHERE account_number = @account_number", connection, transaction))
            {
                updateAccount.Parameters.Add(new SqlParameter("@name", SqlDbType.NVarChar, 255) { Value = account.Name });
                updateAccount.Parameters.Add(new SqlParameter("@privilege", SqlDbType.NVarChar, 10) { Value = account.Privilege });
                updateAccount.Parameters.Add(new SqlParameter("@balance", SqlDbType.Decimal) { Precision = 15, Scale = 2, Value = account.Balance.Amount });
                updateAccount.Parameters.Add(new SqlParameter("@is_active", SqlDbType.Bit) { Value = isActive });
                updateAccount.Parameters.Add(new SqlParameter("@closed_date", SqlDbType.DateTime2) { Value = (object?)closedDate ?? DBNull.Value });
                updateAccount.Parameters.Add(new SqlParameter("@pin_hash", SqlDbType.NVarChar, 255) { Value = account.PinHash });
                updateAccount.Parameters.Add(new SqlParameter("@updated_at", SqlDbType.DateTime2) { Value = DateTime.UtcNow });
                updateAccount.Parameters.Add(new SqlParameter("@row_version", SqlDbType.UniqueIdentifier) { Value = Guid.NewGuid() });
                updateAccount.Parameters.Add(new SqlParameter("@account_number", SqlDbType.Int) { Value = account.AccountNumber!.Value });
                rows = await updateAccount.ExecuteNonQueryAsync(ct);
            }

            if (rows == 0)
                throw new AccountNotFoundError(account.AccountNumber!.Value);

            if (account is SavingsAccount savings && savings.Details != null)
            {
                await using var updateSavings = new SqlCommand(
                    @"UPDATE savings_account_details
                         SET phone_no = @phone_no, updated_at = @updated_at
                       WHERE account_number = @account_number", connection, transaction);
                updateSavings.Parameters.Add(new SqlParameter("@phone_no", SqlDbType.NVarChar, 20) { Value = savings.Details.PhoneNumber });
                updateSavings.Parameters.Add(new SqlParameter("@updated_at", SqlDbType.DateTime2) { Value = DateTime.UtcNow });
                updateSavings.Parameters.Add(new SqlParameter("@account_number", SqlDbType.Int) { Value = account.AccountNumber!.Value });
                await updateSavings.ExecuteNonQueryAsync(ct);
            }
            else if (account is CurrentAccount current && current.Details != null)
            {
                await using var updateCurrent = new SqlCommand(
                    @"UPDATE current_account_details
                         SET company_name = @company_name, website = @website, updated_at = @updated_at
                       WHERE account_number = @account_number", connection, transaction);
                updateCurrent.Parameters.Add(new SqlParameter("@company_name", SqlDbType.NVarChar, 255) { Value = current.Details.AccountHolderName });
                updateCurrent.Parameters.Add(new SqlParameter("@website", SqlDbType.NVarChar, 255) { Value = (object?)current.Details.Website ?? DBNull.Value });
                updateCurrent.Parameters.Add(new SqlParameter("@updated_at", SqlDbType.DateTime2) { Value = DateTime.UtcNow });
                updateCurrent.Parameters.Add(new SqlParameter("@account_number", SqlDbType.Int) { Value = account.AccountNumber!.Value });
                await updateCurrent.ExecuteNonQueryAsync(ct);
            }

            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<Account> UpdateBalanceAtomicAsync(int accountNumber, Action<Account> applyDomainOperation, CancellationToken ct = default)
    {
        const int maxRetries = 3;
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(ct);

            // Load a FRESH row together with its optimistic-concurrency token.
            Guid rowVersion;
            Account domain;
            await using (var loadCommand = new SqlCommand(
                $"SELECT {SelectColumns}, a.row_version AS a_row_version {FromJoins} WHERE a.account_number = @accountNumber", connection))
            {
                loadCommand.Parameters.Add(new SqlParameter("@accountNumber", SqlDbType.Int) { Value = accountNumber });
                await using var reader = await loadCommand.ExecuteReaderAsync(ct);
                if (!await reader.ReadAsync(ct))
                    throw new AccountNotFoundError(accountNumber);

                rowVersion = reader.GetGuid(reader.GetOrdinal("a_row_version"));
                domain = MapAccount(reader, includeDetails: true);
            }

            // Apply the caller's domain operation OUTSIDE any concurrency catch so domain-invariant
            // exceptions (InsufficientFunds, AccountInactive, ...) propagate to the caller.
            applyDomainOperation(domain);

            var (isActive, closedDate) = MapStatus(domain);

            await using var updateCommand = new SqlCommand(
                @"UPDATE accounts
                     SET balance = @balance, is_active = @is_active, closed_date = @closed_date,
                         updated_at = @updated_at, row_version = @new_row_version
                   WHERE account_number = @account_number AND row_version = @old_row_version", connection);
            updateCommand.Parameters.Add(new SqlParameter("@balance", SqlDbType.Decimal) { Precision = 15, Scale = 2, Value = domain.Balance.Amount });
            updateCommand.Parameters.Add(new SqlParameter("@is_active", SqlDbType.Bit) { Value = isActive });
            updateCommand.Parameters.Add(new SqlParameter("@closed_date", SqlDbType.DateTime2) { Value = (object?)closedDate ?? DBNull.Value });
            updateCommand.Parameters.Add(new SqlParameter("@updated_at", SqlDbType.DateTime2) { Value = DateTime.UtcNow });
            updateCommand.Parameters.Add(new SqlParameter("@new_row_version", SqlDbType.UniqueIdentifier) { Value = Guid.NewGuid() });
            updateCommand.Parameters.Add(new SqlParameter("@account_number", SqlDbType.Int) { Value = accountNumber });
            updateCommand.Parameters.Add(new SqlParameter("@old_row_version", SqlDbType.UniqueIdentifier) { Value = rowVersion });

            var affected = await updateCommand.ExecuteNonQueryAsync(ct);
            if (affected > 0)
                return domain;

            // Optimistic-concurrency conflict: another writer changed row_version first. Retry.
            if (attempt == maxRetries)
                throw new AccountException("Account was modified concurrently; please retry.", "CONCURRENCY_CONFLICT");
        }

        throw new AccountException("Account was modified concurrently; please retry.", "CONCURRENCY_CONFLICT");
    }

    // ---------------------------------------------------------------------------------------
    // Mapping helpers
    // ---------------------------------------------------------------------------------------

    // CONCEPT: method overloading — two methods, same name, different signatures.
    // The single-argument overload defaults to including the joined detail rows.
    private Account MapAccount(SqlDataReader reader) => MapAccount(reader, includeDetails: true);

    // CONCEPT: method overloading — second overload with an extra parameter.
    private Account MapAccount(SqlDataReader reader, bool includeDetails)
    {
        var accountNumber = reader.GetInt32(reader.GetOrdinal("account_number"));
        var accountType = reader.GetString(reader.GetOrdinal("account_type"));
        var name = reader.GetString(reader.GetOrdinal("name"));
        var privilege = reader.GetString(reader.GetOrdinal("privilege"));
        var pinHash = reader.GetString(reader.GetOrdinal("pin_hash"));
        var balance = reader.GetDecimal(reader.GetOrdinal("balance"));
        var bankName = reader.GetString(reader.GetOrdinal("bank_name"));
        var bankBranch = reader.GetString(reader.GetOrdinal("bank_branch"));
        var ifscCode = reader.GetString(reader.GetOrdinal("ifsc_code"));
        var isActive = reader.GetBoolean(reader.GetOrdinal("is_active"));
        var activatedDate = reader.GetDateTime(reader.GetOrdinal("activated_date"));
        var closedOrdinal = reader.GetOrdinal("closed_date");
        DateTime? closedDate = reader.IsDBNull(closedOrdinal) ? null : reader.GetDateTime(closedOrdinal);

        // Mirror AccountMapper.ToDomain status resolution.
        AccountStatus status = isActive
            ? AccountStatus.ACTIVE
            : closedDate != null ? AccountStatus.CLOSED : AccountStatus.SUSPENDED;

        var bank = new Bank(bankName, bankBranch, ifscCode);
        var money = new Money(balance);
        var id = new AccountId(accountNumber);

        if (accountType == "SAVINGS")
        {
            var dobOrdinal = reader.GetOrdinal("s_date_of_birth");
            if (includeDetails && !reader.IsDBNull(dobOrdinal))
            {
                var details = new SavingsDetails(
                    reader.GetDateTime(dobOrdinal).ToString("yyyy-MM-dd"),
                    reader.GetString(reader.GetOrdinal("s_gender")),
                    reader.GetString(reader.GetOrdinal("s_phone_no")),
                    reader.GetString(reader.GetOrdinal("s_aadhar_number")),
                    reader.GetString(reader.GetOrdinal("s_aadhar_hash")));

                return SavingsAccount.RestoreSavings(
                    id, accountType, name, privilege, pinHash, money, bank, status, activatedDate, closedDate, details);
            }
        }
        else if (accountType == "CURRENT")
        {
            var companyOrdinal = reader.GetOrdinal("c_company_name");
            if (includeDetails && !reader.IsDBNull(companyOrdinal))
            {
                var websiteOrdinal = reader.GetOrdinal("c_website");
                var details = new CurrentDetails(
                    reader.GetString(companyOrdinal),
                    reader.GetString(reader.GetOrdinal("c_registration_no")),
                    reader.IsDBNull(websiteOrdinal) ? string.Empty : reader.GetString(websiteOrdinal));

                return CurrentAccount.RestoreCurrent(
                    id, accountType, name, privilege, pinHash, money, bank, status, activatedDate, closedDate, details);
            }
        }

        throw new InvalidOperationException($"Invalid AccountType: {accountType} or missing details.");
    }

    // Mirror AccountMapper.ToEntity's status -> (is_active, closed_date) projection.
    private static (bool IsActive, DateTime? ClosedDate) MapStatus(Account account) => account.Status switch
    {
        AccountStatus.ACTIVE => (true, (DateTime?)null),
        AccountStatus.CLOSED => (false, account.StatusUpdatedDate),
        _ => (false, (DateTime?)null) // SUSPENDED / PROPOSED
    };

    // CONCEPT: 'in' parameter modifier — a readonly struct passed by 'in' (read-only reference,
    // avoids a defensive copy) used to compose the GetAll filter clause.
    private readonly struct AccountQuery
    {
        public readonly string? AccountType;
        public AccountQuery(string? accountType) => AccountType = accountType;
    }

    // CONCEPT: 'in' parameter
    private static string BuildWhereClause(in AccountQuery query)
        => string.IsNullOrEmpty(query.AccountType) ? string.Empty : "WHERE a.account_type = @accountType";
}
