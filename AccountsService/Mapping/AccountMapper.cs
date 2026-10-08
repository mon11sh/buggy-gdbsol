using System;
using AccountsService.Domain.Enums;
using AccountsService.Domain.Models;
using AccountsService.Infrastructure.Data.Entities;

namespace AccountsService.Mapping;

public static class AccountMapper
{
    public static AccountEntity ToEntity(Account domain)
    {
        var entity = new AccountEntity
        {
            AccountType = domain.AccountType,
            Name = domain.Name,
            Privilege = domain.Privilege,
            PinHash = domain.PinHash,
            Balance = domain.Balance.Amount,
            BankName = domain.BankDetails.Name,
            BankBranch = domain.BankDetails.Branch,
            IfscCode = domain.BankDetails.IfscCode,
            ActivatedDate = domain.ActivatedDate
        };

        if (domain.AccountNumber != null)
        {
            entity.AccountNumber = domain.AccountNumber.Value;
        }

        switch (domain.Status)
        {
            case AccountStatus.ACTIVE:
                entity.IsActive = true;
                entity.ClosedDate = null;
                break;
            case AccountStatus.SUSPENDED:
                entity.IsActive = false;
                entity.ClosedDate = null;
                break;
            case AccountStatus.CLOSED:
                entity.IsActive = false;
                entity.ClosedDate = domain.StatusUpdatedDate;
                break;
            case AccountStatus.PROPOSED:
                entity.IsActive = false;
                entity.ClosedDate = null;
                break;
        }

        if (domain is SavingsAccount savingsAccount && savingsAccount.Details != null)
        {
            entity.SavingsDetails = new SavingsAccountDetailsEntity
            {
                DateOfBirth = DateTime.Parse(savingsAccount.Details.DateOfBirth),
                Gender = savingsAccount.Details.Gender,
                PhoneNo = savingsAccount.Details.PhoneNumber,
                AadharNumber = savingsAccount.Details.Aadhaar,
                AadharHash = savingsAccount.Details.AadhaarHash
            };
            
            if (domain.AccountNumber != null)
            {
                entity.SavingsDetails.AccountNumber = domain.AccountNumber.Value;
            }
        }
        else if (domain is CurrentAccount currentAccount && currentAccount.Details != null)
        {
            entity.CurrentDetails = new CurrentAccountDetailsEntity
            {
                CompanyName = currentAccount.Details.AccountHolderName,
                RegistrationNo = currentAccount.Details.RegistrationNumber,
                Website = currentAccount.Details.Website
            };

            if (domain.AccountNumber != null)
            {
                entity.CurrentDetails.AccountNumber = domain.AccountNumber.Value;
            }
        }

        return entity;
    }

    public static Account ToDomain(AccountEntity entity, SavingsAccountDetailsEntity? s = null, CurrentAccountDetailsEntity? c = null)
    {
        var savingsEntity = s ?? entity.SavingsDetails;
        var currentEntity = c ?? entity.CurrentDetails;

        AccountStatus status;
        if (entity.IsActive)
        {
            status = AccountStatus.ACTIVE;
        }
        else if (entity.ClosedDate != null)
        {
            status = AccountStatus.CLOSED;
        }
        else
        {
            status = AccountStatus.SUSPENDED; // Can also map to proposed, but suspended makes more sense for existing data
        }

        if (entity.AccountType == "SAVINGS" && savingsEntity != null)
        {
            var savingsDetails = new SavingsDetails(
                savingsEntity.DateOfBirth.ToString("yyyy-MM-dd"),
                savingsEntity.Gender,
                savingsEntity.PhoneNo,
                savingsEntity.AadharNumber,
                savingsEntity.AadharHash
            );

            return SavingsAccount.RestoreSavings(
                new AccountId(entity.AccountNumber),
                entity.AccountType,
                entity.Name,
                entity.Privilege,
                entity.PinHash,
                new Money(entity.Balance),
                new Bank(entity.BankName, entity.BankBranch, entity.IfscCode),
                status,
                entity.ActivatedDate,
                entity.ClosedDate,
                savingsDetails
            );
        }
        else if (entity.AccountType == "CURRENT" && currentEntity != null)
        {
            var currentDetails = new CurrentDetails(
                currentEntity.CompanyName,
                currentEntity.RegistrationNo,
                currentEntity.Website
            );

            return CurrentAccount.RestoreCurrent(
                new AccountId(entity.AccountNumber),
                entity.AccountType,
                entity.Name,
                entity.Privilege,
                entity.PinHash,
                new Money(entity.Balance),
                new Bank(entity.BankName, entity.BankBranch, entity.IfscCode),
                status,
                entity.ActivatedDate,
                entity.ClosedDate,
                currentDetails
            );
        }

        throw new InvalidOperationException($"Invalid AccountType: {entity.AccountType} or missing details.");
    }
}
