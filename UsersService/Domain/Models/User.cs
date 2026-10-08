using System;
using UsersService.Domain.Exceptions;

namespace UsersService.Domain.Models;

public class User
{
    public UserId Id { get; private set; }
    public LoginId LoginId { get; private set; }
    public string Username { get; private set; }
    public PasswordHash Password { get; private set; }
    public UserRole Role { get; private set; }
    public UserStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }

#pragma warning disable CS8618
    private User() { }
#pragma warning restore CS8618

    private User(
        UserId id,
        LoginId loginId,
        string username,
        PasswordHash password,
        UserRole role,
        UserStatus status,
        DateTime createdAt)
    {
        Id = id;
        LoginId = loginId;
        Username = username;
        Password = password;
        Role = role;
        Status = status;
        CreatedAt = createdAt;
    }

    public static User Create(LoginId loginId, PasswordHash password, UserRole role, string username)
    {
        return new User(
            new UserId(0), // DB will generate ID
            loginId,
            username,
            password,
            role,
            UserStatus.ACTIVE,
            DateTime.UtcNow
        );
    }

    public static User Load(
        UserId id,
        LoginId loginId,
        string username,
        PasswordHash password,
        UserRole role,
        UserStatus status,
        DateTime createdAt)
    {
        return new User(id, loginId, username, password, role, status, createdAt);
    }

    public void UpdateProfile(string newUsername)
    {
        if (string.IsNullOrWhiteSpace(newUsername))
            throw new ArgumentException("Username cannot be empty");
            
        Username = newUsername;
    }

    public void ChangePassword(string rawNewPassword)
    {
        if (string.IsNullOrWhiteSpace(rawNewPassword))
            throw new ArgumentException("Password cannot be empty");
            
        Password = PasswordHash.CreateFromRaw(rawNewPassword);
    }

    public void ChangeRole(UserRole newRole)
    {
        Role = newRole;
    }

    public void Inactivate(int activeAdminCount)
    {
        if (Status == UserStatus.INACTIVE)
            throw new UserAlreadyInactiveException(LoginId.Value);

        if (Role == UserRole.ADMIN && activeAdminCount <= 1)
            throw new LastAdminException(LoginId.Value);

        Status = UserStatus.INACTIVE;
    }

    public void Activate()
    {
        if (Status == UserStatus.ACTIVE)
            throw new UserAlreadyActiveException(LoginId.Value);

        Status = UserStatus.ACTIVE;
    }

    public bool VerifyPassword(string rawPassword)
    {
        return Password.Verify(rawPassword);
    }
}
