using System.Text.Json;
using TransactionsService.Config;
using TransactionsService.Domain.Models;
using TransactionsService.Infrastructure.Data;
using TransactionsService.Integration;
using TransactionsService.Utils;
using TransactionsService.DTOs;
using TransactionsService.Domain.Exceptions;

namespace TransactionsService.Services;

/// <summary>
/// Orchestrates the deposit workflow, ensuring safe and idempotent monetary additions to an account.
/// </summary>
/// <remarks>
/// Architectural Intent: Encapsulates the entire deposit transaction boundary, spanning across 
/// idempotency checks, account validations via internal APIs, logging, and notifications.
/// </remarks>
public class DepositService : IDepositService
{
    private readonly IUnitOfWork _uow;
    private readonly AccountServiceClient _accountClient;
    private readonly NotificationClient _notificationClient;
    private readonly Settings _settings;
    private readonly ILogger<DepositService> _logger;
    private readonly CacheInvalidator _invalidator;

    public DepositService(
        IUnitOfWork uow, 
        AccountServiceClient accountClient, 
        NotificationClient notificationClient, 
        Settings settings, 
        ILogger<DepositService> logger,
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
    /// Processes a deposit operation safely.
    /// </summary>
    /// <remarks>
    /// Business Rule: Idempotency keys prevent double-crediting an account if a client retries 
    /// a failed/timed-out network request. If a matching key is found with status 202 (processing), 
    /// it throws to prevent race conditions. If completed, it returns the cached response.
    /// </remarks>
    public async Task<TransactionResultResponse> ProcessDepositAsync(int accountNumber, decimal amount, string description, string? idempotencyKey, CancellationToken ct = default)
    {
        // 1. Validate Amount
        Validators.ValidateAmount(amount, _settings.MinimumDepositAmount, _settings.MaximumTransactionAmount);

        // 2. Idempotency: replay a finished request, refuse an in-flight or failed one, or reserve the key.
        if (!string.IsNullOrEmpty(idempotencyKey))
        {
            var existingKey = await _uow.Idempotency.GetKeyAsync(idempotencyKey, ct);
            if (existingKey == null && !await _uow.Idempotency.TryReserveKeyAsync(idempotencyKey, ct))
                existingKey = await _uow.Idempotency.GetKeyAsync(idempotencyKey, ct); // lost the race — replay whatever won

            if (existingKey != null)
            {
                IdempotencySettlement.ThrowIfNotReplayable(existingKey, "deposit");
                _logger.LogInformation("Idempotency key hit: {Key}", idempotencyKey);
                return JsonSerializer.Deserialize<TransactionResultResponse>(existingKey.ResponseBody)!;
            }
        }

        // Becomes true the moment money may have moved. Governs how the idempotency key is settled on failure.
        var sideEffectsStarted = false;

        try
        {

        // 3. Verify Account
        var accountData = await _accountClient.ValidateAccountAsync(accountNumber, ct);
        
        // 4. Credit Account — from here money may have moved: the idempotency key must not be released on failure.
        sideEffectsStarted = true;
        // CancellationToken.None from here on: side effects already happened; finishing the bookkeeping must not be cancelled.
        var creditResult = await _accountClient.CreditAccountAsync(accountNumber, amount, description, CancellationToken.None);
        var newBalance = creditResult.NewBalance;
        
        // 5. Log Transaction
        var log = await _uow.TransactionLogs.LogTransactionAsync(accountNumber, TransactionType.DEPOSIT, amount, newBalance, null, description, CancellationToken.None);

        // 6. Notify
        await _notificationClient.SendNotificationAsync(accountNumber, "DEPOSIT", $"Deposit of {amount} successful. New balance: {newBalance}", CancellationToken.None);

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
            // Release only if nothing moved; otherwise record the failure so a retry cannot double-credit.
            await IdempotencySettlement.SettleOnFailureAsync(_uow.Idempotency, idempotencyKey, sideEffectsStarted, ex);
            throw;
        }
    }
}
