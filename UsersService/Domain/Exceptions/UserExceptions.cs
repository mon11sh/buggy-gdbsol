using Gdb.Common.Exceptions;

namespace UsersService.Domain.Exceptions;

// Each exception declares the HTTP status it maps to; the shared Gdb.Common error boundary does the rest.
public abstract class UserManagementException : GdbException
{
    protected UserManagementException(string message, string errorCode, int statusCode = 400)
        : base(message, errorCode, statusCode) { }
}

public class UserAlreadyExistsException : UserManagementException
{
    public UserAlreadyExistsException(string loginId)
        : base($"User with login_id '{loginId}' already exists", "USER_ALREADY_EXISTS", 409) { }
}

public class UserNotFoundException : UserManagementException
{
    public UserNotFoundException(string loginId)
        : base($"User with login_id '{loginId}' not found", "USER_NOT_FOUND", 404) { }
}

public class UserInactiveException : UserManagementException
{
    public UserInactiveException(string loginId)
        : base($"User '{loginId}' is inactive", "USER_INACTIVE", 403) { }
}

public class UserAlreadyActiveException : UserManagementException
{
    public UserAlreadyActiveException(string loginId)
        : base($"User '{loginId}' is already active", "USER_ALREADY_ACTIVE") { }
}

public class UserAlreadyInactiveException : UserManagementException
{
    public UserAlreadyInactiveException(string loginId)
        : base($"User with login_id '{loginId}' is already inactive", "USER_ALREADY_INACTIVE") { }
}

public class LastAdminException : UserManagementException
{
    public LastAdminException(string loginId)
        : base($"Cannot inactivate user '{loginId}': at least one active ADMIN must remain in the system", "LAST_ACTIVE_ADMIN") { }
}

public class InvalidUserInputException : UserManagementException
{
    public InvalidUserInputException(string field, string detail)
        : base($"Invalid {field}: {detail}", "INVALID_USER_INPUT") { }
}

public class InvalidRoleException : UserManagementException
{
    public InvalidRoleException(string role, string[] allowedRoles)
        : base($"Invalid role '{role}'. Valid roles are: {string.Join(", ", allowedRoles)}", "INVALID_ROLE") { }
}

public class DatabaseException : UserManagementException
{
    public DatabaseException(string message)
        : base(message, "DATABASE_ERROR", 500) { }
}
