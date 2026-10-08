using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Security.Cryptography;

namespace Gdb.Common.Security;

public static class JwtValidationExtensions
{
    /// <summary>Default issuer/audience shared by every GDB service when none is configured.</summary>
    public const string DefaultIssuer = "gdb-auth";
    public const string DefaultAudience = "gdb-services";

    /// <summary>
    /// Registers JWT bearer authentication with strict validation: signature, lifetime,
    /// issuer, audience, and a pinned signing algorithm. Issuer/audience validation is only
    /// relaxed when the caller passes an empty value explicitly.
    /// </summary>
    /// <remarks>
    /// Also installs a deny-by-default <see cref="AuthorizationOptions.FallbackPolicy"/>: any endpoint
    /// that carries no authorization metadata requires an authenticated user. Endpoints that are
    /// legitimately public (health probes) or guarded by another mechanism (<c>[InternalApi]</c> key)
    /// must opt out explicitly with <c>[AllowAnonymous]</c> — a forgotten attribute now fails closed
    /// instead of silently exposing a route (audit Finding #5).
    /// </remarks>
    public static IServiceCollection AddGdbJwtAuthentication(
        this IServiceCollection services,
        string secretKey,
        string algorithm = "HS256",
        string publicKey = "",
        string issuer = DefaultIssuer,
        string audience = DefaultAudience)
    {
        var useRsa = string.Equals(algorithm, "RS256", StringComparison.OrdinalIgnoreCase)
                     && !string.IsNullOrWhiteSpace(publicKey);

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    RequireSignedTokens = true,
                    ValidateIssuer = !string.IsNullOrWhiteSpace(issuer),
                    ValidIssuer = issuer,
                    ValidateAudience = !string.IsNullOrWhiteSpace(audience),
                    ValidAudience = audience,
                    // Pin the algorithm so a token signed with an unexpected scheme is rejected
                    // even if a matching key type happens to be registered.
                    ValidAlgorithms = new[] { useRsa ? SecurityAlgorithms.RsaSha256 : SecurityAlgorithms.HmacSha256 },
                    ClockSkew = TimeSpan.Zero,
                    RoleClaimType = "role",
                    NameClaimType = "login_id"
                };

                var keys = new List<SecurityKey>();

                if (useRsa)
                {
                    var rsa = RSA.Create();
                    rsa.ImportFromPem(publicKey);
                    keys.Add(new RsaSecurityKey(rsa));
                }
                else if (!string.IsNullOrWhiteSpace(secretKey))
                {
                    keys.Add(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)));
                }

                options.TokenValidationParameters.IssuerSigningKeys = keys;

                // Refresh tokens share the signing key but carry token_use=refresh: they are only ever
                // valid at the auth service's refresh endpoint, never as a Bearer token here.
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        if (context.Principal?.FindFirst("token_use")?.Value == "refresh")
                            context.Fail("A refresh token cannot be used as an access token.");
                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });

        return services;
    }
}
