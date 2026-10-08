using UsersService.Domain.Exceptions;
using UsersService.Domain.Models;
using UsersService.DTOs;
using UsersService.Utils;
using AutoMapper;

namespace UsersService.Services;

/// <summary>
/// Domain service responsible for the creation, modification, and lifecycle of Users.
/// </summary>
public class UserService
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<UserService> _logger;
    private readonly IMapper _mapper;

    public UserService(IUnitOfWork uow, ILogger<UserService> logger, IMapper mapper)
    {
        _uow = uow;
        _logger = logger;
        _mapper = mapper;
    }

    public async Task<AddUserResponse> AddUserAsync(AddUserRequest request, string? performedBy = null, CancellationToken ct = default)
    {
        _logger.LogInformation("Starting add user for: {LoginId}", request.LoginId);

        Validators.ValidateAddUserInput(request.Username, request.LoginId, request.Password, request.Role);
        var roleString = Validators.ValidateRole(request.Role);
        var role = Enum.Parse<UserRole>(roleString, true);

        var existingUser = await _uow.Users.GetUserByLoginIdAsync(request.LoginId, ct);
        if (existingUser != null)
        {
            throw new UserAlreadyExistsException(request.LoginId);
        }

        var loginId = new LoginId(request.LoginId);
        var passwordHash = PasswordHash.CreateFromRaw(request.Password);
        
        var user = User.Create(loginId, passwordHash, role, request.Username);
        
        user = await _uow.Users.CreateUserAsync(user, ct);
        await _uow.CommitAsync(ct);

        await AuditService.LogActionAsync(_uow.Audit, user.Id.Value, "CREATE", null, 
            new { username = user.Username, login_id = user.LoginId.Value, role = user.Role.ToString(), is_active = user.Status == UserStatus.ACTIVE }, 
            performedBy, ct);
        await _uow.CommitAsync(ct);

        _logger.LogInformation("User created successfully: {LoginId} with role: {Role}", request.LoginId, role);
        
        return _mapper.Map<AddUserResponse>(user);
    }

    public async Task<EditUserResponse> EditUserAsync(string loginId, EditUserRequest request, string? performedBy = null, CancellationToken ct = default)
    {
        _logger.LogInformation("Starting edit user for: {LoginId}", loginId);

        var user = await _uow.Users.GetUserByLoginIdAsync(loginId, ct);
        if (user == null)
            throw new UserNotFoundException(loginId);
        
        var oldData = new { username = user.Username, role = user.Role.ToString() };
        
        Validators.ValidateEditUserInput(request.Username, request.Password, request.Role);

        if (!string.IsNullOrEmpty(request.Username)) user.UpdateProfile(request.Username);
        if (!string.IsNullOrEmpty(request.Password)) user.ChangePassword(request.Password);
        // Role is deliberately NOT updated here to prevent privilege escalation.
        
        var updatedUser = await _uow.Users.UpdateUserAsync(user, ct);
        await _uow.CommitAsync(ct);

        await AuditService.LogActionAsync(_uow.Audit, user.Id.Value, "UPDATE", oldData, 
            new { username = updatedUser.Username, role = updatedUser.Role.ToString() }, 
            performedBy, ct);
        await _uow.CommitAsync(ct);

        _logger.LogInformation("User updated successfully: {LoginId}", loginId);

        return _mapper.Map<EditUserResponse>(updatedUser);
    }

    public async Task<ViewUserResponse> GetUserAsync(string loginId, CancellationToken ct = default)
    {
        var user = await _uow.Users.GetUserByLoginIdAsync(loginId, ct);
        if (user == null)
            throw new UserNotFoundException(loginId);

        return _mapper.Map<ViewUserResponse>(user);
    }

    public async Task<object> VerifyUserCredentialsAsync(string loginId, string password, CancellationToken ct = default)
    {
        var user = await _uow.Users.GetUserByLoginIdAsync(loginId, ct);
        if (user == null)
        {
            return new 
            {
                is_valid = false,
                user_id = (int?)null,
                role = (string?)null,
                is_active = false
            };
        }
        
        bool isValid = user.VerifyPassword(password);
        
        return new 
        {
            is_valid = isValid,
            user_id = isValid ? (int?)user.Id.Value : null,
            role = isValid ? user.Role.ToString() : null,
            is_active = user.Status == UserStatus.ACTIVE
        };
    }

    public async Task<object?> GetUserStatusAsync(string loginId, CancellationToken ct = default)
    {
        var user = await _uow.Users.GetUserByLoginIdAsync(loginId, ct);
        if (user == null) return null;
            
        return new
        {
            user_id = user.Id.Value,
            login_id = user.LoginId.Value,
            is_active = user.Status == UserStatus.ACTIVE,
            role = user.Role.ToString()
        };
    }

    public async Task<object?> GetUserRoleAsync(string loginId, CancellationToken ct = default)
    {
        var user = await _uow.Users.GetUserByLoginIdAsync(loginId, ct);
        if (user == null) return null;
            
        return new
        {
            user_id = user.Id.Value,
            login_id = user.LoginId.Value,
            role = user.Role.ToString()
        };
    }

    public async Task<object?> ValidateUserRoleAsync(string loginId, string requiredRole, CancellationToken ct = default)
    {
        var user = await _uow.Users.GetUserByLoginIdAsync(loginId, ct);
        if (user == null) return null;
            
        return new
        {
            has_role = user.Role.ToString() == requiredRole,
            user_role = user.Role.ToString(),
            is_active = user.Status == UserStatus.ACTIVE
        };
    }

    public async Task<object> BulkValidateUsersAsync(List<string> loginIds, CancellationToken ct = default)
    {
        var validUsers = new List<object>();
        var invalidUsers = new List<string>();
        
        foreach (var loginId in loginIds)
        {
            var user = await _uow.Users.GetUserByLoginIdAsync(loginId, ct);
            if (user != null)
            {
                validUsers.Add(new
                {
                    user_id = user.Id.Value,
                    login_id = user.LoginId.Value,
                    role = user.Role.ToString(),
                    is_active = user.Status == UserStatus.ACTIVE
                });
            }
            else
            {
                invalidUsers.Add(loginId);
            }
        }
        
        return new
        {
            valid_users = validUsers,
            invalid_users = invalidUsers,
            total_valid = validUsers.Count,
            total_invalid = invalidUsers.Count
        };
    }

    public async Task<ListUsersResponse> GetAllUsersAsync(int skip = 0, int limit = Gdb.Common.Http.Paging.MaxPageSize, CancellationToken ct = default)
    {
        var users = await _uow.Users.GetAllUsersAsync(ct);
        return new ListUsersResponse
        {
            Users = users.Skip(skip).Take(limit).Select(_mapper.Map<UserResponse>).ToList(),
            TotalCount = users.Count
        };
    }

    public async Task<InactivateUserResponse> InactivateUserAsync(string loginId, string? performedBy = null, CancellationToken ct = default)
    {
        var user = await _uow.Users.GetUserByLoginIdAsync(loginId, ct);
        if (user == null)
            throw new UserNotFoundException(loginId);

        var activeAdmins = await _uow.Users.CountActiveAdminsAsync(ct);
        user.Inactivate(activeAdmins);

        var updatedUser = await _uow.Users.UpdateUserAsync(user, ct);
        await _uow.CommitAsync(ct);

        await AuditService.LogActionAsync(_uow.Audit, user.Id.Value, "INACTIVATE", 
            new { is_active = true }, new { is_active = false }, performedBy, ct);
        await _uow.CommitAsync(ct);

        return _mapper.Map<InactivateUserResponse>(updatedUser);
    }

    public async Task<InactivateUserResponse> ActivateUserAsync(string loginId, string? performedBy = null, CancellationToken ct = default)
    {
        var user = await _uow.Users.GetUserByLoginIdAsync(loginId, ct);
        if (user == null)
            throw new UserNotFoundException(loginId);

        user.Activate();

        var updatedUser = await _uow.Users.UpdateUserAsync(user, ct);
        await _uow.CommitAsync(ct);

        await AuditService.LogActionAsync(_uow.Audit, user.Id.Value, "REACTIVATE", 
            new { is_active = false }, new { is_active = true }, performedBy, ct);
        await _uow.CommitAsync(ct);

        return _mapper.Map<InactivateUserResponse>(updatedUser) with { Message = "User activated successfully" };
    }
}
