using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using AuthService.Config;

namespace AuthService.Security;

/// <summary>
/// Handles JWT token generation and validation for the application's authentication flow.
/// </summary>
/// <remarks>
/// Architectural Intent: Centralizes all cryptographic token operations. It dynamically supports
/// both RSA asymmetric keys (preferred for production) and symmetric HMAC keys based on configuration.
/// Two token kinds share one signing key and are told apart by the <c>token_use</c> claim: short-lived
/// <c>access</c> tokens (presented as Bearer to every service) and long-lived <c>refresh</c> tokens
/// (presented only to <c>/api/v1/auth/refresh</c>, normally from an httpOnly cookie). A refresh token
/// is never accepted where an access token is expected, and vice versa.
/// </remarks>
public class JwtUtil
{
    public const string TokenUseClaim = "token_use";
    public const string AccessUse = "access";
    public const string RefreshUse = "refresh";

    private readonly Settings _settings;

    public JwtUtil(Settings settings)
    {
        _settings = settings;
    }

    /// <summary>Short-lived access token carrying <c>login_id</c> and <c>role</c> so resource services need no lookup.</summary>
    public (string token, string jti, DateTime iat, DateTime exp) GenerateToken(int userId, string loginId, string role) =>
        Mint(userId, loginId, role, AccessUse, TimeSpan.FromMinutes(_settings.JwtExpirationMinutes));

    /// <summary>Long-lived refresh token; its jti is stored so it can be rotated and revoked.</summary>
    public (string token, string jti, DateTime iat, DateTime exp) GenerateRefreshToken(int userId, string loginId, string role) =>
        Mint(userId, loginId, role, RefreshUse, TimeSpan.FromDays(_settings.RefreshTokenDays));

    private (string token, string jti, DateTime iat, DateTime exp) Mint(int userId, string loginId, string role, string tokenUse, TimeSpan lifetime)
    {
        var jti = Guid.NewGuid().ToString();
        var iat = DateTime.UtcNow;
        var exp = iat + lifetime;

        SigningCredentials credentials;
        RSA? rsa = null;
        if (!string.IsNullOrWhiteSpace(_settings.JwtPrivateKey))
        {
            rsa = RSA.Create();
            rsa.ImportFromPem(_settings.JwtPrivateKey.ToCharArray());
            credentials = new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256);
        }
        else
        {
            credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.JwtSecretKey)), SecurityAlgorithms.HmacSha256);
        }

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, jti),
            new Claim("login_id", loginId),
            new Claim("role", role),
            new Claim(TokenUseClaim, tokenUse)
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            NotBefore = iat,
            Expires = exp,
            SigningCredentials = credentials,
            IssuedAt = iat,
            Issuer = _settings.JwtIssuer,
            Audience = _settings.JwtAudience
        };

        var handler = new JwtSecurityTokenHandler();
        var tokenString = handler.WriteToken(handler.CreateToken(tokenDescriptor));

        rsa?.Dispose();

        return (tokenString, jti, iat, exp);
    }

    /// <summary>
    /// Validates a token (signature, issuer, audience, lifetime, algorithm) and returns its claims.
    /// The token must be of the <paramref name="expectedUse"/> kind; a refresh token presented where an
    /// access token is expected (or the reverse) is rejected.
    /// </summary>
    public Dictionary<string, object> VerifyToken(string tokenString, string expectedUse = AccessUse)
    {
        var handler = new JwtSecurityTokenHandler();
        var issuerSigningKeys = new List<SecurityKey>();
        var rsaKeys = new List<RSA>();

        if (!string.IsNullOrWhiteSpace(_settings.JwtPublicKey))
        {
            var rsa = RSA.Create();
            rsa.ImportFromPem(_settings.JwtPublicKey.ToCharArray());
            rsaKeys.Add(rsa);
            issuerSigningKeys.Add(new RsaSecurityKey(rsa));
        }

        issuerSigningKeys.Add(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.JwtSecretKey)));

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = issuerSigningKeys,
            ValidateIssuer = !string.IsNullOrWhiteSpace(_settings.JwtIssuer),
            ValidIssuer = _settings.JwtIssuer,
            ValidateAudience = !string.IsNullOrWhiteSpace(_settings.JwtAudience),
            ValidAudience = _settings.JwtAudience,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256, SecurityAlgorithms.RsaSha256 },
            ClockSkew = TimeSpan.Zero
        };

        try
        {
            handler.ValidateToken(tokenString, validationParameters, out var validatedToken);
            var jwtToken = (JwtSecurityToken)validatedToken;

            var claimsDict = new Dictionary<string, object>();
            foreach (var claim in jwtToken.Claims)
            {
                if (int.TryParse(claim.Value, out int intVal) && claim.Type == JwtRegisteredClaimNames.Sub)
                    claimsDict[claim.Type] = intVal;
                else
                    claimsDict[claim.Type] = claim.Value;
            }

            if (!claimsDict.ContainsKey("jti") && claimsDict.ContainsKey(JwtRegisteredClaimNames.Jti))
                claimsDict["jti"] = claimsDict[JwtRegisteredClaimNames.Jti];

            var use = claimsDict.TryGetValue(TokenUseClaim, out var u) ? u.ToString() : AccessUse;
            if (!string.Equals(use, expectedUse, StringComparison.Ordinal))
                throw new SecurityTokenException($"A '{use}' token was presented where a '{expectedUse}' token is required.");

            return claimsDict;
        }
        finally
        {
            foreach (var rsa in rsaKeys)
                rsa.Dispose();
        }
    }
}
