using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using AuthService.Config;
using AuthService.DTOs;
using AuthService.Services;
using AuthService.Security;
using AuthService.Domain.Exceptions;

namespace AuthService.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthenticationService _authService;
    private readonly LoginThrottle _loginThrottle;
    private readonly Settings _settings;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<AuthController> _logger;

    public AuthController(AuthenticationService authService, LoginThrottle loginThrottle, Settings settings, IWebHostEnvironment env, ILogger<AuthController> logger)
    {
        _authService = authService;
        _loginThrottle = loginThrottle;
        _settings = settings;
        _env = env;
        _logger = logger;
    }

    private string? GetClientIp() => HttpContext.Connection.RemoteIpAddress?.ToString();
    private string? GetUserAgent() => Request.Headers["User-Agent"];

    [HttpPost("login")]
    public async Task<ActionResult<TokenResponse>> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var ipAddress = GetClientIp();
        var userAgent = GetUserAgent();

        _logger.LogInformation("Login attempt for {LoginId} from {IpAddress}", request.LoginId, ipAddress);

        if (_loginThrottle.IsLocked(request.LoginId))
        {
            _logger.LogWarning("Login blocked - too many attempts: {LoginId}", request.LoginId);
            return StatusCode(429, new { error_code = "TOO_MANY_ATTEMPTS", message = "Too many failed login attempts. Try again later." });
        }

        try
        {
            var tokenData = await _authService.LoginAsync(request.LoginId, request.Password, ipAddress, userAgent, ct);
            _loginThrottle.RecordSuccess(request.LoginId);
            return Ok(ToResponse(tokenData));
        }
        catch (UserNotFoundException)
        {
            _loginThrottle.RecordFailure(request.LoginId);
            throw;
        }
        catch (InvalidCredentialsException)
        {
            _loginThrottle.RecordFailure(request.LoginId);
            throw;
        }
    }

    /// <summary>
    /// Exchanges the refresh token (httpOnly cookie set at login, or <c>refresh_token</c> in the
    /// body for non-browser clients) for a new access token. The refresh token is rotated: the one
    /// presented is revoked and a new one is set.
    /// </summary>
    [HttpPost("refresh")]
    public async Task<ActionResult<TokenResponse>> Refresh([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] RefreshRequest? request, CancellationToken ct)
    {
        var refreshToken = request?.RefreshToken;
        if (string.IsNullOrEmpty(refreshToken))
            Request.Cookies.TryGetValue(_settings.RefreshCookieName, out refreshToken);

        if (string.IsNullOrEmpty(refreshToken))
            return Unauthorized(new { error_code = "INVALID_TOKEN", message = "Missing refresh token" });

        try
        {
            var tokenData = await _authService.RefreshAsync(refreshToken, GetClientIp(), GetUserAgent(), ct);
            return Ok(ToResponse(tokenData));
        }
        catch (InvalidCredentialsException)
        {
            ClearRefreshCookie(); // a dead refresh token is not worth keeping in the browser
            throw;
        }
    }

    [HttpGet("verify")]
    public async Task<ActionResult<TokenVerifyResponse>> VerifyToken(CancellationToken ct)
    {
        var token = BearerToken();
        if (token is null)
        {
            return Unauthorized(new { error_code = "INVALID_TOKEN", message = "Missing or invalid token" });
        }

        try
        {
            var claims = await _authService.VerifyTokenAsync(token, ct);
            return Ok(new TokenVerifyResponse
            {
                Valid = true,
                UserId = claims.ContainsKey("user_id") ? claims["user_id"] : (claims.ContainsKey("sub") ? claims["sub"] : null),
                LoginId = claims.ContainsKey("login_id") ? claims["login_id"] : null,
                Role = claims.ContainsKey("role") ? claims["role"] : null
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token verification failed");
            return Unauthorized(new { error_code = "INVALID_TOKEN", message = "Token verification failed" });
        }
    }

    [HttpPost("logout")]
    public async Task<ActionResult<LogoutResponse>> Logout(CancellationToken ct)
    {
        Request.Cookies.TryGetValue(_settings.RefreshCookieName, out var refreshToken);
        await _authService.LogoutAsync(BearerToken(), refreshToken, ct);
        ClearRefreshCookie();

        _logger.LogInformation("User logged out successfully");
        return Ok(new LogoutResponse { Status = "success", Message = "Logged out successfully" });
    }

    [HttpPost("register")]
    public ActionResult Register()
    {
        return StatusCode(403, new
        {
            error_code = "REGISTRATION_NOT_ALLOWED",
            message = "User registration is managed by administrators. Please contact your admin to create an account."
        });
    }

    private string? BearerToken()
    {
        var authHeader = Request.Headers["Authorization"].ToString();
        return authHeader.StartsWith("Bearer ", StringComparison.Ordinal) ? authHeader["Bearer ".Length..].Trim() : null;
    }

    /// <summary>The access token goes in the body (Bearer clients); the refresh token goes in an httpOnly cookie.</summary>
    private TokenResponse ToResponse(Dictionary<string, object> tokenData)
    {
        var refreshSeconds = int.Parse(tokenData["refresh_expires_in"].ToString()!);
        Response.Cookies.Append(_settings.RefreshCookieName, tokenData["refresh_token"].ToString()!, RefreshCookieOptions(DateTimeOffset.UtcNow.AddSeconds(refreshSeconds)));

        return new TokenResponse
        {
            AccessToken = tokenData["access_token"].ToString()!,
            TokenType = tokenData["token_type"].ToString()!,
            ExpiresIn = int.Parse(tokenData["expires_in"].ToString()!),
            UserId = int.Parse(tokenData["user_id"].ToString()!),
            LoginId = tokenData["login_id"].ToString()!,
            Role = tokenData["role"].ToString()!
        };
    }

    private void ClearRefreshCookie() => Response.Cookies.Delete(_settings.RefreshCookieName, RefreshCookieOptions(null));

    private CookieOptions RefreshCookieOptions(DateTimeOffset? expires) => new()
    {
        HttpOnly = true,                                  // never readable by page scripts
        Secure = Request.IsHttps || !_env.IsDevelopment(), // plain http only on a developer machine
        SameSite = SameSiteMode.Strict,
        Path = _settings.RefreshCookiePath,
        IsEssential = true,
        Expires = expires,
    };
}
