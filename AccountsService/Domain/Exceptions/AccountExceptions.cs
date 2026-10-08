using Gdb.Common.Exceptions;

namespace AccountsService.Domain.Exceptions;

// Each exception declares the HTTP status it maps to; the shared Gdb.Common error boundary does the rest.
public class AccountException : GdbException
{
    public AccountException(string message, string errorCode = "ACCOUNT_ERROR", int statusCode = 400)
        : base(message, errorCode, statusCode) { }
}

public class AccountNotFoundError : AccountException
{
    public AccountNotFoundError(int accountNumber)
        : base($"Account {accountNumber} not found", "ACCOUNT_NOT_FOUND", 404) { }
}

public class AccountAlreadyActiveError : AccountException
{
    public AccountAlreadyActiveError(int? accountNumber)
        : base($"Account {accountNumber} is already active", "ACCOUNT_ALREADY_ACTIVE", 409) { }
}

public class AccountAlreadyInactiveError : AccountException
{
    public AccountAlreadyInactiveError(int? accountNumber)
        : base($"Account {accountNumber} is already inactive", "ACCOUNT_ALREADY_INACTIVE", 409) { }
}

public class AccountClosedError : AccountException
{
    public AccountClosedError(int? accountNumber)
        : base($"Account {accountNumber} is closed and cannot be modified", "ACCOUNT_CLOSED", 409) { }
}

public class AccountInactiveError : AccountException
{
    public AccountInactiveError(int? accountNumber)
        : base($"Account {accountNumber} is inactive and cannot perform operations", "ACCOUNT_INACTIVE", 409) { }
}

public class AgeRestrictionError : AccountException
{
    public AgeRestrictionError(int minAge)
        : base($"Applicant must be at least {minAge} years old", "AGE_RESTRICTION") { }
}

public class InsufficientFundsError : AccountException
{
    public InsufficientFundsError(decimal balance, decimal amount)
        : base($"Insufficient funds: balance is {balance}, tried to debit {amount}", "INSUFFICIENT_FUNDS") { }
}

public class ValidationError : AccountException
{
    public string Field { get; }

    public ValidationError(string field, string message)
        : base(message, "VALIDATION_ERROR")
    {
        Field = field;
    }
}

public class InvalidPinError : AccountException
{
    public InvalidPinError(string message = "Invalid PIN format")
        : base(message, "INVALID_PIN", 401) { }
}

public class AccountLockedError : AccountException
{
    public AccountLockedError(string message, int retryAfterSeconds)
        : base(message, "ACCOUNT_LOCKED", 423)
    {
        RetryAfterSeconds = retryAfterSeconds;
    }
}
