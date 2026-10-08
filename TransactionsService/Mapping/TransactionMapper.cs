using System;
using TransactionsService.Domain.Models;
using TransactionsService.Infrastructure.Data;

namespace TransactionsService.Mapping;

public static class TransactionMapper
{
    public static FundTransfer ToDomain(FundTransferEntity entity)
    {
        return new FundTransfer(
            entity.Id,
            new AccountId(entity.SourceAccountId),
            new AccountId(entity.DestinationAccountId),
            new Money(entity.Amount),
            entity.TransferMode,
            Enum.TryParse<TransferStatus>(entity.Status, true, out var status) ? status : TransferStatus.PENDING,
            entity.FailureReason ?? string.Empty,
            $"Transfer to {entity.DestinationAccountId}",
            Guid.NewGuid(), // Not in DB
            entity.CreatedAt,
            entity.CreatedAt
        );
    }

    public static FundTransferEntity ToEntity(FundTransfer domain)
    {
        return new FundTransferEntity
        {
            Id = domain.TransactionId,
            SourceAccountId = domain.Account.Number,
            DestinationAccountId = domain.DestinationAccount.Number,
            Amount = domain.Amount.Amount,
            TransferMode = domain.Mode,
            Status = domain.TransferState.ToString(),
            FailureReason = string.IsNullOrEmpty(domain.FailureReason) ? null : domain.FailureReason,
            CreatedAt = domain.CreatedAt
        };
    }
}
