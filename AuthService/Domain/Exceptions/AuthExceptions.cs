using Gdb.Common.Exceptions;

namespace AuthService.Domain.Exceptions;

// Each exception declares the HTTP status it maps to; the shared Gdb.Common error boundary does the rest.
public class AuthenticationException : GdbException
{
    public AuthenticationException(string message, string errorCode = "authentication_error", int statusCode = 401)
        : base(message, errorCode, statusCode) { }
}

public class InvalidCredentialsException : AuthenticationException
{
    public InvalidCredentialsException(string message = "Invalid credentials")
        : base(message, "INVALID_CREDENTIALS") { }
}

public class UserInactiveException : AuthenticationException
{
    public UserInactiveException(string message = "User is inactive")
        : base(message, "USER_INACTIVE") { }
}

public class UserNotFoundException : AuthenticationException
{
    public UserNotFoundException(string message = "User not found")
        : base(message, "USER_NOT_FOUND", 404) { }
}

public class ServiceUnavailableException : GdbException
{
    public ServiceUnavailableException(string message = "Service unavailable")
        : base(message, "SERVICE_UNAVAILABLE", 503) { }
}
