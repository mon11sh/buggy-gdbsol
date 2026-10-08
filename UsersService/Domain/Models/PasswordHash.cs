namespace UsersService.Domain.Models;

public class PasswordHash
{
    public string Hash { get; }

    private PasswordHash(string hash)
    {
        Hash = hash;
    }

    public static PasswordHash CreateFromRaw(string rawPassword)
    {
        return new PasswordHash(BCrypt.Net.BCrypt.HashPassword(rawPassword));
    }

    public static PasswordHash FromExistingHash(string existingHash)
    {
        return new PasswordHash(existingHash);
    }

    public bool Verify(string rawPassword)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(rawPassword, Hash);
        }
        catch
        {
            return false;
        }
    }
}
