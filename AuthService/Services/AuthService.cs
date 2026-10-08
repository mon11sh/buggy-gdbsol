using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using AuthService.Security;
using AuthService.Domain.Ports;
using AuthService.Infrastructure.Repositories;
using AuthService.Domain.Exceptions;
using AuthService.Domain.Models;

namespace AuthService.Services;

/// <summary>
/// Manages the user login lifecycle, token issuance, refresh (rotation) and revocation.
/// </summary>
public class AuthenticationService
{
    private readonly IUnitOfWork _uow;
    private readonly IUserServicePort _users;
    private readonly JwtUtil _jwtUtil;
    private readonly ILogger<AuthenticationService> _logger;

    public AuthenticationService(IUnitOfWork uow, IUserServicePort users, JwtUtil jwtUtil, ILogger<AuthenticationService> logger)
    {
        _uow = uow;
        _users = users;
        _jwtUtil = jwtUtil;
        _logger = logger;
    }

    private async Task LogFailureAsync(string loginId, UserId? userId, string reason, string? ipAddress, string? userAgent, CancellationToken ct = default)
    {
        try
        {
            var auditLog = AuthAuditLog.CreateFailure(loginId, userId, reason, ipAddress, userAgent);
            await _uow.Audit.LogAuditAsync(auditLog, ct);
            await _uow.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log login failure for {LoginId}", loginId);
        }
    }

    public async Task<Dictionary<string, object>> LoginAsync(string loginId, string password, string? ipAddress, string? userAgent, CancellationToken ct = default)
    {
        Dictionary<string, object>? userData;
        try
        {
            userData = await _users.VerifyUserCredentialsAsync(loginId, password, ct);
        }
        catch (ServiceUnavailableException)
        {
            await LogFailureAsync(loginId, null, "User service unavailable", ipAddress, userAgent, ct);
            throw;
        }

        bool isValid = userData != null && userData.ContainsKey("is_valid") && bool.TryParse(userData["is_valid"]?.ToString(), out var parsed) && parsed;

        if (userData == null || !isValid)
        {
            _logger.LogWarning("Invalid credentials or user not found: {LoginId}", loginId);
            await LogFailureAsync(loginId, null, "Invalid credentials or user not found", ipAddress, userAgent, ct);
            throw new InvalidCredentialsException();
        }

        int rawUserId = int.Parse(userData["user_id"]?.ToString() ?? "0");
        var userId = new UserId(rawUserId);

        bool isActive = userData.ContainsKey("is_active") && bool.TryParse(userData["is_active"]?.ToString(), out var active) && active;
        string role = userData["role"]?.ToString() ?? string.Empty;

        if (!isActive)
        {
            _logger.LogWarning("Login attempt by inactive user: {LoginId}", loginId);
            await LogFailureAsync(loginId, userId, "User inactive", ipAddress, userAgent, ct);
            throw new UserInactiveException();
        }

        var session = await IssueSessionAsync(rawUserId, loginId, role, ipAddress, userAgent, ct);
        _logger.LogInformation("Successful login for user: {LoginId}", loginId);
        return session;
    }

    /// <summary>
    /// Exchanges a valid, unrevoked refresh token for a new access + refresh pair. The presented
    /// refresh token is revoked first (rotation), so a stolen token that is replayed after the
    /// legitimate client refreshed is refused.
    /// </summary>
    public async Task<Dictionary<string, object>> RefreshAsync(string refreshToken, string? ipAddress, string? userAgent, CancellationToken ct = default)
    {
        Dictionary<string, object> claims;
        try
        {
            claims = _jwtUtil.VerifyToken(refreshToken, JwtUtil.RefreshUse);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Refresh rejected - invalid refresh token");
            throw new InvalidCredentialsException("Invalid refresh token");
        }

        var jti = new TokenJti(claims["jti"].ToString()!);
        var loginId = claims.TryGetValue("login_id", out var l) ? l.ToString() ?? "" : "";
        var role = claims.TryGetValue("role", out var r) ? r.ToString() ?? "" : "";
        var rawUserId = claims.TryGetValue("sub", out var s) && int.TryParse(s.ToString(), out var id) ? id : 0;

        var stored = await _uow.Tokens.GetTokenAsync(jti, ct);
        if (stored == null || !stored.IsActive(DateTime.UtcNow))
        {
            _logger.LogWarning("Refresh rejected for {LoginId}: token revoked, rotated or expired", loginId);
            await LogFailureAsync(loginId, new UserId(rawUserId), "Refresh token revoked, rotated or expired", ipAddress, userAgent, ct);
            throw new InvalidCredentialsException("Refresh token has been revoked or expired");
        }

        await _uow.Tokens.RevokeTokenAsync(jti, ct); // rotation: one use only
        await _uow.CommitAsync(ct);

        var session = await IssueSessionAsync(rawUserId, loginId, role, ipAddress, userAgent, ct);
        _logger.LogInformation("Session refreshed for user: {LoginId}", loginId);
        return session;
    }

    private async Task<Dictionary<string, object>> IssueSessionAsync(int rawUserId, string loginId, string role, string? ipAddress, string? userAgent, CancellationToken ct)
    {
        var userId = new UserId(rawUserId);
        string accessToken, accessJti, refreshToken, refreshJti;
        DateTime iat, exp, refreshIat, refreshExp;
        try
        {
            (accessToken, accessJti, iat, exp) = _jwtUtil.GenerateToken(rawUserId, loginId, role);
            (refreshToken, refreshJti, refreshIat, refreshExp) = _jwtUtil.GenerateRefreshToken(rawUserId, loginId, role);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate token for {LoginId}", loginId);
            await LogFailureAsync(loginId, userId, "Token generation failed", ipAddress, userAgent, ct);
            throw;
        }

        try
        {
            await _uow.Tokens.CreateTokenAsync(AuthToken.Create(new TokenJti(accessJti), userId, loginId, iat, exp), ct);
            await _uow.Tokens.CreateTokenAsync(AuthToken.Create(new TokenJti(refreshJti), userId, loginId, refreshIat, refreshExp), ct);
            await _uow.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store token for {LoginId}", loginId);
        }

        try
        {
            var auditLog = AuthAuditLog.CreateSuccess(loginId, userId, ipAddress, userAgent);
            await _uow.Audit.LogAuditAsync(auditLog, ct);
            await _uow.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log login success for {LoginId}", loginId);
        }

        return new Dictionary<string, object>
        {
            { "access_token", accessToken },
            { "token_type", "Bearer" },
            { "expires_in", (int)(exp - iat).TotalSeconds },
            { "refresh_token", refreshToken },
            { "refresh_expires_in", (int)(refreshExp - refreshIat).TotalSeconds },
            { "user_id", rawUserId },
            { "login_id", loginId },
            { "role", role }
        };
    }

    public async Task<Dictionary<string, object>> VerifyTokenAsync(string tokenString, CancellationToken ct = default)
    {
        Dictionary<string, object> claims;
        try
        {
            claims = _jwtUtil.VerifyToken(tokenString);
        }
        catch (Exception ex)
        {
            // Log the detail server-side; never leak the raw exception message to the client.
            _logger.LogWarning(ex, "Token verification failed");
            throw new InvalidCredentialsException("Invalid token");
        }

        var jti = new TokenJti(claims["jti"].ToString()!);
        try
        {
            var token = await _uow.Tokens.GetTokenAsync(jti, ct);
            if (token == null || !token.IsActive(DateTime.UtcNow))
            {
                throw new InvalidCredentialsException("Token has been revoked or expired");
            }
        }
        catch (InvalidCredentialsException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check token revocation");
            throw new InvalidCredentialsException("Unable to verify token revocation status");
        }

        return claims;
    }

    /// <summary>Revokes the access token and, when presented, the refresh token. Never throws.</summary>
    public async Task<bool> LogoutAsync(string? accessToken, string? refreshToken = null, CancellationToken ct = default)
    {
        await RevokeAsync(accessToken, JwtUtil.AccessUse, "User logged out", ct);
        await RevokeAsync(refreshToken, JwtUtil.RefreshUse, "User logged out (refresh token)", ct);
        return true;
    }

    private async Task RevokeAsync(string? tokenString, string tokenUse, string reason, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(tokenString))
            return;

        Dictionary<string, object> claims;
        try
        {
            claims = _jwtUtil.VerifyToken(tokenString, tokenUse);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Logout: ignoring invalid {TokenUse} token", tokenUse);
            return;
        }

        try
        {
            var jtiRaw = claims["jti"]?.ToString();
            var loginId = claims.ContainsKey("login_id") ? claims["login_id"].ToString() ?? "unknown" : "unknown";

            if (!string.IsNullOrEmpty(jtiRaw))
            {
                var jti = new TokenJti(jtiRaw);
                if (await _uow.Tokens.GetTokenAsync(jti, ct) != null)
                    await _uow.Tokens.RevokeTokenAsync(jti, ct);

                var auditLog = AuthAuditLog.CreateTokenRevoked(loginId, null, reason, null, null);
                await _uow.Audit.LogAuditAsync(auditLog, ct);
                await _uow.CommitAsync(ct);
                _logger.LogInformation("{TokenUse} token revoked for user: {LoginId}", tokenUse, loginId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Logout error while revoking a {TokenUse} token", tokenUse);
        }
    }
}
