using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using UsersService.Domain.Models;
using UsersService.Infrastructure.Data;
using UsersService.Mapping;

namespace UsersService.Infrastructure.Repositories;

public interface IUserRepository
{
    Task<User> CreateUserAsync(User user, CancellationToken ct = default);
    Task<User?> GetUserByLoginIdAsync(string loginId, CancellationToken ct = default);
    Task<User?> GetUserByIdAsync(int userId, CancellationToken ct = default);
    Task<User> UpdateUserAsync(User user, CancellationToken ct = default);
    Task<int> CountActiveAdminsAsync(CancellationToken ct = default);
    Task<List<User>> GetAllUsersAsync(CancellationToken ct = default);
}

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _context;

    public UserRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<User> CreateUserAsync(User user, CancellationToken ct = default)
    {
        var entity = UserMapper.ToEntity(user);
        _context.Users.Add(entity);
        await _context.SaveChangesAsync(ct);
        
        // Return mapped version to capture DB generated ID
        return UserMapper.ToDomain(entity);
    }

    public async Task<User?> GetUserByLoginIdAsync(string loginId, CancellationToken ct = default)
    {
        var entity = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.LoginId == loginId, ct);
        return entity == null ? null : UserMapper.ToDomain(entity);
    }

    public async Task<User?> GetUserByIdAsync(int userId, CancellationToken ct = default)
    {
        var entity = await _context.Users.FindAsync(new object[] { userId }, ct);
        return entity == null ? null : UserMapper.ToDomain(entity);
    }

    public async Task<User> UpdateUserAsync(User user, CancellationToken ct = default)
    {
        var entity = await _context.Users.FindAsync(new object[] { user.Id.Value }, ct);
        if (entity != null)
        {
            var updatedEntity = UserMapper.ToEntity(user);
            entity.Username = updatedEntity.Username;
            entity.Password = updatedEntity.Password;
            entity.Role = updatedEntity.Role;
            entity.IsActive = updatedEntity.IsActive;
            entity.UpdatedAt = updatedEntity.UpdatedAt;
            
            await _context.SaveChangesAsync(ct);
        }
        return user;
    }

    public async Task<int> CountActiveAdminsAsync(CancellationToken ct = default)
    {
        return await _context.Users.CountAsync(u => u.Role == "ADMIN" && u.IsActive, ct);
    }

    public async Task<List<User>> GetAllUsersAsync(CancellationToken ct = default)
    {
        var entities = await _context.Users.AsNoTracking().OrderByDescending(u => u.CreatedAt).ToListAsync(ct);
        return entities.Select(UserMapper.ToDomain).ToList();
    }
}
