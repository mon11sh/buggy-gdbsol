using System.Text.RegularExpressions;
using UsersService.Domain.Exceptions;
using UsersService.Domain.Models;

namespace UsersService.Utils;

public static class Validators
{
    private static readonly Regex LoginIdRegex = new Regex(@"^[a-zA-Z0-9._-]+$", RegexOptions.Compiled);

    public static void ValidateAddUserInput(string username, string loginId, string password, string? role)
    {
        if (string.IsNullOrWhiteSpace(username) || username.Length < 1 || username.Length > 255)
            throw new InvalidUserInputException("username", "must be between 1 and 255 characters");

        if (string.IsNullOrWhiteSpace(loginId) || loginId.Length < 3 || loginId.Length > 50)
            throw new InvalidUserInputException("login_id", "must be between 3 and 50 characters");

        if (!LoginIdRegex.IsMatch(loginId))
            throw new InvalidUserInputException("login_id", "can only contain alphanumeric, dots, hyphens, underscores");

        ValidatePassword(password, loginId);
    }

    /// <summary>
    /// Password policy for staff accounts: 8-128 characters with upper, lower, digit and symbol, no
    /// whitespace, and not built from the login id. Applied on create and on change alike.
    /// </summary>
    public static void ValidatePassword(string? password, string? loginId)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
            throw new InvalidUserInputException("password", "must be at least 8 characters");
        if (password.Length > 128)
            throw new InvalidUserInputException("password", "must be at most 128 characters");
        if (password.Any(char.IsWhiteSpace))
            throw new InvalidUserInputException("password", "must not contain spaces");
        if (!password.Any(char.IsUpper))
            throw new InvalidUserInputException("password", "must contain at least one uppercase letter");
        if (!password.Any(char.IsLower))
            throw new InvalidUserInputException("password", "must contain at least one lowercase letter");
        if (!password.Any(char.IsDigit))
            throw new InvalidUserInputException("password", "must contain at least one digit");
        if (password.All(char.IsLetterOrDigit))
            throw new InvalidUserInputException("password", "must contain at least one symbol");
        if (!string.IsNullOrEmpty(loginId) && loginId.Length >= 3 && password.Contains(loginId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidUserInputException("password", "must not contain the login id");
    }

    public static void ValidateEditUserInput(string? username, string? password, string? role)
    {
        if (username != null && (username.Length < 1 || username.Length > 255))
            throw new InvalidUserInputException("username", "must be between 1 and 255 characters");

        if (password != null)
            ValidatePassword(password, loginId: null);
    }

    public static string ValidateRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
            return UserRole.MANAGER.ToString();

        if (Enum.TryParse<UserRole>(role, true, out var parsedRole))
        {
            return parsedRole.ToString();
        }
        
        throw new InvalidRoleException(role, new[] { "MANAGER", "TELLER", "ADMIN" });
    }
}
