using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using UsersService.Domain.Exceptions;
using UsersService.Domain.Models;
using UsersService.DTOs;
using UsersService.Infrastructure.Repositories;
using UsersService.Services;
using UsersService.Utils;

namespace UsersService.Tests.Services;

[TestClass]
public class UserServiceTests
{
    private Mock<IUnitOfWork> _uowMock = null!;
    private Mock<IUserRepository> _userRepoMock = null!;
    private Mock<IAuditRepository> _auditRepoMock = null!;

    private UserService _userService = null!;

    [TestInitialize]
    public void Setup()
    {
        _uowMock = new Mock<IUnitOfWork>();
        _userRepoMock = new Mock<IUserRepository>();
        _auditRepoMock = new Mock<IAuditRepository>();
        
        _uowMock.Setup(u => u.Users).Returns(_userRepoMock.Object);
        _uowMock.Setup(u => u.Audit).Returns(_auditRepoMock.Object);

        _userService = new UserService(
            _uowMock.Object,
            new Mock<ILogger<UserService>>().Object,
            UsersService.Tests.Utils.UserMappingProfileTests.NewMapper()
        );
    }

    private User CreateTestUser(int id, string loginId, string role, bool isActive)
    {
        return User.Load(
            new UserId(id),
            new LoginId(loginId),
            "Test User",
            PasswordHash.FromExistingHash("hash"),
            Enum.Parse<UserRole>(role, true),
            isActive ? UserStatus.ACTIVE : UserStatus.INACTIVE,
            DateTime.UtcNow
        );
    }

    [TestMethod]
    public async Task AddUserAsync_WithNewUser_SuccessfullyCreatesUser()
    {
        // Arrange
        var request = new AddUserRequest
        {
            Username = "Test User",
            LoginId = "testuser",
            Password = "Password123!",
            Role = "TELLER"
        };
        
        _userRepoMock.Setup(r => r.GetUserByLoginIdAsync(request.LoginId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var createdDomainUser = CreateTestUser(1, request.LoginId, request.Role, true);
        
        _userRepoMock.Setup(r => r.CreateUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdDomainUser);

        // Act
        var result = await _userService.AddUserAsync(request, "admin");

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(request.Username, result.Username);
        Assert.AreEqual(request.LoginId, result.LoginId);
        Assert.AreEqual(request.Role, result.Role);
        _uowMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [TestMethod]
    public async Task AddUserAsync_WithExistingUser_ThrowsUserAlreadyExistsException()
    {
        // Arrange
        var request = new AddUserRequest
        {
            Username = "Test User",
            LoginId = "testuser",
            Password = "Password123!",
            Role = "TELLER"
        };
        
        _userRepoMock.Setup(r => r.GetUserByLoginIdAsync(request.LoginId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTestUser(1, request.LoginId, "MANAGER", true));

        // Act & Assert
        try
        {
            await _userService.AddUserAsync(request, "admin");
            Assert.Fail("Expected UserAlreadyExistsException");
        }
        catch (UserAlreadyExistsException)
        {
            // Passed
        }
        
        _userRepoMock.Verify(r => r.CreateUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        _uowMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task InactivateUserAsync_WhenLastAdmin_ThrowsLastAdminException()
    {
        // Arrange
        var loginId = "admin";
        var user = CreateTestUser(1, loginId, "ADMIN", true);
        
        _userRepoMock.Setup(r => r.GetUserByLoginIdAsync(loginId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _userRepoMock.Setup(r => r.CountActiveAdminsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1); // Last admin

        // Act & Assert
        try
        {
            await _userService.InactivateUserAsync(loginId, "admin");
            Assert.Fail("Expected LastAdminException");
        }
        catch (LastAdminException)
        {
            // Passed
        }
        
        _userRepoMock.Verify(r => r.UpdateUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        _uowMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
