using System.Text.Json;
using TransactionsService.Config;
using TransactionsService.Domain.Exceptions;
using TransactionsService.Domain.Models;
using TransactionsService.Infrastructure.Data;
using TransactionsService.Integration;
using TransactionsService.Utils;
using TransactionsService.DTOs;

namespace TransactionsService.Services;

/// <summary>
/// Orchestrates the withdrawal workflow, managing balance deductions safely.
/// </summary>
/// <remarks>
/// Architectural Intent: Validates account PIN and balance BEFORE attempting an idempotency-locked 
/// debit via the AccountsService internal API.
/// </remarks>
public class WithdrawService : IWithdrawService
{
    private readonly IUnitOfWork _uow;
    private readonly AccountServiceClient _accountClient;
    private readonly NotificationClient _notificationClient;
    private readonly Settings _settings;
    private readonly ILogger<WithdrawService> _logger;
    private readonly CacheInvalidator _invalidator;

    public WithdrawService(
        IUnitOfWork uow, 
        AccountServiceClient accountClient, 
        NotificationClient notificationClient, 
        Settings settings, 
        ILogger<WithdrawService> logger,
        CacheInvalidator invalidator)
    {
        _uow = uow;
        _accountClient = accountClient;
        _notificationClient = notificationClient;
        _settings = settings;
        _logger = logger;
        _invalidator = invalidator;
    }

    /// <summary>
    /// Processes a cash withdrawal.
    /// </summary>
    /// <remarks>
    /// Security Intent: Requires explicit PIN verification for withdrawals.
    /// Business Rule: Enforces minimum and maximum transaction limits defined in settings.
    /// </remarks>
    public async Task<TransactionResultResponse> ProcessWithdrawAsync(int accountNumber, decimal amount, string pin, string description, string? idempotencyKey, CancellationToken ct = default)
    {
        // 1. Validate Amount
        Validators.ValidateAmount(amount, _settings.MinimumWithdrawalAmount, _settings.MaximumTransactionAmount);

        // 2. Idempotency: replay a finished request, refuse an in-flight or failed one, or reserve the key.
        if (!string.IsNullOrEmpty(idempotencyKey))
        {
            var existingKey = await _uow.Idempotency.GetKeyAsync(idempotencyKey, ct);
            if (existingKey == null && !await _uow.Idempotency.TryReserveKeyAsync(idempotencyKey, ct))
                existingKey = await _uow.Idempotency.GetKeyAsync(idempotencyKey, ct); // lost the race — replay whatever won

            if (existingKey != null)
            {
                IdempotencySettlement.ThrowIfNotReplayable(existingKey, "withdrawal");
                _logger.LogInformation("Idempotency key hit: {Key}", idempotencyKey);
                return JsonSerializer.Deserialize<TransactionResultResponse>(existingKey.ResponseBody)!;
            }
        }

        // Becomes true the moment money may have moved. Governs how the idempotency key is settled on failure.
        var sideEffectsStarted = false;

        try
        {

        // 3. Verify Account and PIN
        var accountData = await _accountClient.ValidateAccountAsync(accountNumber, ct);
        await _accountClient.VerifyPinAsync(accountNumber, pin, ct);
        
        var currentBalance = accountData.Balance;
        Validators.ValidateBalance(currentBalance, amount);
        
        // 4. Debit Account — from here money may have moved: the idempotency key must not be released on failure.
        sideEffectsStarted = true;
        // CancellationToken.None from here on: side effects already happened; finishing the bookkeeping must not be cancelled.
        var debitResult = await _accountClient.DebitAccountAsync(accountNumber, amount, description, CancellationToken.None);
        var newBalance = debitResult.NewBalance;
        
        // 5. Log Transaction
        var log = await _uow.TransactionLogs.LogTransactionAsync(accountNumber, TransactionType.WITHDRAWAL, amount, newBalance, null, description, CancellationToken.None);

        // 6. Notify
        await _notificationClient.SendNotificationAsync(accountNumber, "WITHDRAWAL", $"Withdrawal of {amount} successful. New balance: {newBalance}", CancellationToken.None);

        var response = new TransactionResultResponse
        {
            Status = "SUCCESS",
            TransactionId = log.Id,
            AccountNumber = accountNumber,
            Amount = amount,
            NewBalance = newBalance
        };

        // 7. Save Idempotency
        if (!string.IsNullOrEmpty(idempotencyKey))
        {
            await _uow.Idempotency.CompleteKeyAsync(idempotencyKey, JsonSerializer.Serialize(response), 201, CancellationToken.None);
        }

        // 8. Evict Transaction Logs Cache
        _invalidator.EvictAccount(accountNumber);

        return response;
        }
        catch (Exception ex) when (ex is not IdempotencyException)
        {
            // Release only if nothing moved; otherwise record the failure so a retry cannot double-debit.
            await IdempotencySettlement.SettleOnFailureAsync(_uow.Idempotency, idempotencyKey, sideEffectsStarted, ex);
            throw;
        }
    }
}
