using TransactionsService.Infrastructure.Data;
using TransactionsService.Infrastructure.Repositories;

using TransactionsService.Integration;
using TransactionsService.Domain.Exceptions;
using TransactionsService.Domain.Models;

namespace TransactionsService.Services;

public class TransferLimitService
{
    private readonly ITransferLimitRepository _repository;
    private readonly AccountServiceClient _accountClient;
    private readonly ILogger<TransferLimitService> _logger;

    public TransferLimitService(ITransferLimitRepository repository, AccountServiceClient accountClient, ILogger<TransferLimitService> logger)
    {
        _repository = repository;
        _accountClient = accountClient;
        _logger = logger;
    }

    public async Task<TransferLimitEntity?> GetLimitAsync(string privilege, CancellationToken ct = default)
    {
        return await _repository.GetLimitByPrivilegeAsync(privilege.ToUpperInvariant(), ct);
    }

    public async Task<List<TransferLimitEntity>> GetAllLimitsAsync(CancellationToken ct = default)
    {
        return await _repository.GetAllLimitsAsync(ct);
    }

    public async Task<TransferLimitEntity> CreateLimitAsync(string privilege, decimal dailyLimit, decimal perTransactionLimit, CancellationToken ct = default)
    {
        return await _repository.CreateLimitAsync(privilege.ToUpperInvariant(), dailyLimit, perTransactionLimit, ct);
    }

    public async Task<TransferLimitEntity?> UpdateLimitAsync(string privilege, decimal dailyLimit, decimal perTransactionLimit, CancellationToken ct = default)
    {
        return await _repository.UpdateLimitAsync(privilege.ToUpperInvariant(), dailyLimit, perTransactionLimit, ct);
    }

    public async Task<object> GetTransferLimitAsync(int accountNumber, CancellationToken ct = default)
    {
        _logger.LogInformation($"Getting transfer limits for account {accountNumber}");
        var privilege = await _accountClient.GetAccountPrivilegeAsync(accountNumber, ct);
        
        var limitRule = await _repository.GetLimitByPrivilegeAsync(privilege, ct);
        if (limitRule == null)
            throw new Exception($"No transfer limit rule found for privilege {privilege}"); // Will be mapped to 500 or caught, actually Python raises TransferLimitNotFoundException

        var dailyUsed = await _repository.GetDailyUsedAmountAsync(accountNumber, ct: ct);
        var dailyRemaining = Math.Max(0, limitRule.DailyLimit - dailyUsed);

        var dailyCount = await _repository.GetDailyTransactionCountAsync(accountNumber, ct: ct);
        var transactionsRemaining = Math.Max(0, (int)limitRule.PerTransactionLimit - dailyCount);

        return new
        {
            account_number = accountNumber,
            privilege = privilege,
            daily_limit = limitRule.DailyLimit,
            daily_used = dailyUsed,
            daily_remaining = dailyRemaining,
            transaction_limit = limitRule.PerTransactionLimit,
            transactions_today = dailyCount,
            transactions_remaining = transactionsRemaining
        };
    }

    public async Task<object> GetRemainingLimitAsync(int accountNumber, CancellationToken ct = default)
    {
        _logger.LogInformation($"Quick check remaining limit for account {accountNumber}");
        var privilege = await _accountClient.GetAccountPrivilegeAsync(accountNumber, ct);
        
        var limitRule = await _repository.GetLimitByPrivilegeAsync(privilege, ct);
        if (limitRule == null)
            throw new Exception($"No transfer limit rule found for privilege {privilege}");

        var dailyUsed = await _repository.GetDailyUsedAmountAsync(accountNumber, ct: ct);
        var dailyRemaining = Math.Max(0, limitRule.DailyLimit - dailyUsed);

        var dailyCount = await _repository.GetDailyTransactionCountAsync(accountNumber, ct: ct);
        var transactionsRemaining = Math.Max(0, (int)limitRule.PerTransactionLimit - dailyCount);

        return new
        {
            account_number = accountNumber,
            daily_remaining = dailyRemaining,
            transactions_remaining = transactionsRemaining
        };
    }

    public async Task<object> CheckCanTransferAsync(int accountNumber, decimal proposedAmount, CancellationToken ct = default)
    {
        _logger.LogInformation($"Checking if account {accountNumber} can transfer {proposedAmount}");
        var privilege = await _accountClient.GetAccountPrivilegeAsync(accountNumber, ct);
        
        var limitEntity = await _repository.GetLimitByPrivilegeAsync(privilege, ct);
        if (limitEntity == null)
            throw new Exception($"No transfer limit rule found for privilege {privilege}");

        var dailyUsedAmt = await _repository.GetDailyUsedAmountAsync(accountNumber, ct: ct);
        var dailyCount = await _repository.GetDailyTransactionCountAsync(accountNumber, ct: ct);

        var limitRule = new TransferLimit(
            limitEntity.Privilege, 
            new Money(limitEntity.DailyLimit), 
            new Money(limitEntity.PerTransactionLimit)
        );

        try
        {
            limitRule.CheckLimits(new Money(proposedAmount), new Money(dailyUsedAmt), dailyCount);
        }
        catch (LimitExceededException ex)
        {
            return new
            {
                can_transfer = false,
                reason = ex.Message,
                daily_remaining = (float)(limitRule.DailyLimit.Amount - dailyUsedAmt),
                transactions_remaining = (int)limitRule.PerTransactionLimit.Amount - dailyCount
            };
        }

        return new
        {
            can_transfer = true,
            daily_remaining = (float)(limitRule.DailyLimit.Amount - dailyUsedAmt - proposedAmount),
            transactions_remaining = (int)limitRule.PerTransactionLimit.Amount - dailyCount - 1
        };
    }
}
