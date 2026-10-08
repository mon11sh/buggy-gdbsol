using Gdb.Common.Exceptions;

namespace TransactionsService.Domain.Exceptions;

// Each exception declares the HTTP status it maps to; the shared Gdb.Common error boundary does the rest.
public abstract class TransactionException : GdbException
{
    protected TransactionException(string message, string errorCode, int statusCode = 400)
        : base(message, errorCode, statusCode) { }
}

public class InvalidAccountException : TransactionException
{
    public InvalidAccountException(int accountNumber, string message)
        : base(message, "INVALID_ACCOUNT", 404) { }
}

public class SourceAccountInactiveException : TransactionException
{
    public SourceAccountInactiveException(int accountNumber, string message)
        : base(message, "SOURCE_ACCOUNT_INACTIVE") { }
}

public class TargetAccountInactiveException : TransactionException
{
    public TargetAccountInactiveException(int accountNumber, string message)
        : base(message, "TARGET_ACCOUNT_INACTIVE") { }
}

public class InvalidPinException : TransactionException
{
    public InvalidPinException(string message)
        : base(message, "INVALID_PIN", 401) { }
}

public class InsufficientFundsException : TransactionException
{
    public InsufficientFundsException(int accountNumber, string message)
        : base(message, "INSUFFICIENT_FUNDS") { }
}

public class LimitExceededException : TransactionException
{
    public LimitExceededException(string message)
        : base(message, "LIMIT_EXCEEDED") { }
}

public class InvalidAmountException : TransactionException
{
    public InvalidAmountException(string message)
        : base(message, "INVALID_AMOUNT") { }
}

public class ServiceUnavailableException : TransactionException
{
    public ServiceUnavailableException(string serviceName, string message)
        : base(message, "SERVICE_UNAVAILABLE", 503) { }
}

public class IdempotencyException : TransactionException
{
    public IdempotencyException(string message)
        : base(message, "IDEMPOTENCY_CONFLICT") { }
}

public class TransferModeNotAllowedException : TransactionException
{
    public TransferModeNotAllowedException(string privilege, string mode)
        : base($"Transfer mode {mode} is not available to {privilege} accounts", "TRANSFER_MODE_NOT_ALLOWED") { }
}
