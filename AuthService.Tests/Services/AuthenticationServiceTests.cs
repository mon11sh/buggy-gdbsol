using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Moq.Protected;
using AuthService.Config;
using AuthService.Domain.Exceptions;
using AuthService.Infrastructure.Repositories;
using AuthService.Integration;
using AuthService.Security;
using AuthService.Services;
using AuthService.Domain.Models;

namespace AuthService.Tests.Services;

[TestClass]
public class AuthenticationServiceTests
{
    private Mock<IUnitOfWork> _uowMock = null!;
    private Mock<IAuthAuditRepository> _auditMock = null!;
    private Mock<IAuthTokenRepository> _tokenMock = null!;
    private Mock<HttpMessageHandler> _httpMessageHandlerMock = null!;
    
    private AuthenticationService _authService = null!;
    private Settings _settings = null!;

    [TestInitialize]
    public void Setup()
    {
        _uowMock = new Mock<IUnitOfWork>();
        _auditMock = new Mock<IAuthAuditRepository>();
        _tokenMock = new Mock<IAuthTokenRepository>();
        _uowMock.Setup(u => u.Audit).Returns(_auditMock.Object);
        _uowMock.Setup(u => u.Tokens).Returns(_tokenMock.Object);

        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        var httpClient = new HttpClient(_httpMessageHandlerMock.Object);

        _settings = new Settings
        {
            InternalApiKey = "key",
            JwtSecretKey = "12345678901234567890123456789012",
            JwtExpirationMinutes = 30
        };

        var httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        var serviceProviderMock = new Mock<IServiceProvider>();

        var userClient = new UserServiceClient(httpClient, _settings, new Mock<ILogger<UserServiceClient>>().Object, httpContextAccessorMock.Object, serviceProviderMock.Object);
        var jwtUtil = new JwtUtil(_settings);

        _authService = new AuthenticationService(
            _uowMock.Object,
            userClient,
            jwtUtil,
            new Mock<ILogger<AuthenticationService>>().Object
        );
    }

    private void SetupHttpResponse(string urlContains, HttpStatusCode statusCode, object responseBody)
    {
        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri != null && req.RequestUri.ToString().Contains(urlContains)),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(JsonSerializer.Serialize(responseBody))
            });
    }

    [TestMethod]
    public async Task LoginAsync_WithValidCredentials_ReturnsToken()
    {
        // Arrange
        var loginId = "admin";
        var password = "password";
        
        SetupHttpResponse($"/verify", HttpStatusCode.OK, new 
        {
            is_valid = true,
            user_id = 1,
            is_active = true,
            role = "ADMIN"
        });

        // Act
        var result = await _authService.LoginAsync(loginId, password, "127.0.0.1", "TestAgent");

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.ContainsKey("access_token"));
        Assert.AreEqual(1, result["user_id"]);
        Assert.AreEqual(loginId, result["login_id"]);
        Assert.AreEqual("ADMIN", result["role"]);
        _auditMock.Verify(a => a.LogAuditAsync(It.Is<AuthAuditLog>(log => log.Action == AuditAction.LOGIN_SUCCESS && log.LoginId == loginId && log.UserId != null && log.UserId.Value == 1), It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [TestMethod]
    public async Task LoginAsync_WithInvalidCredentials_ThrowsInvalidCredentialsException()
    {
        // Arrange
        var loginId = "admin";
        var password = "wrongpassword";
        
        SetupHttpResponse($"/verify", HttpStatusCode.OK, new 
        {
            is_valid = false
        });

        // Act & Assert
        try
        {
            await _authService.LoginAsync(loginId, password, "127.0.0.1", "TestAgent");
            Assert.Fail("Expected InvalidCredentialsException");
        }
        catch (InvalidCredentialsException)
        {
            // Passed
        }
        
        _auditMock.Verify(a => a.LogAuditAsync(It.Is<AuthAuditLog>(log => log.Action == AuditAction.LOGIN_FAILURE && log.LoginId == loginId), It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [TestMethod]
    public async Task LoginAsync_WithInactiveUser_ThrowsUserInactiveException()
    {
        // Arrange
        var loginId = "admin";
        var password = "password";
        
        SetupHttpResponse($"/verify", HttpStatusCode.OK, new 
        {
            is_valid = true,
            user_id = 1,
            is_active = false,
            role = "ADMIN"
        });

        // Act & Assert
        try
        {
            await _authService.LoginAsync(loginId, password, "127.0.0.1", "TestAgent");
            Assert.Fail("Expected UserInactiveException");
        }
        catch (UserInactiveException)
        {
            // Passed
        }
        
        _auditMock.Verify(a => a.LogAuditAsync(It.Is<AuthAuditLog>(log => log.Action == AuditAction.LOGIN_FAILURE && log.LoginId == loginId && log.UserId != null && log.UserId.Value == 1), It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }
}
