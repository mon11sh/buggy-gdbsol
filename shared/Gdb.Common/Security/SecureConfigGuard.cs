using System;
using System.Linq;

namespace Gdb.Common.Security;

/// <summary>
/// Fail-closed secret validation, run at service startup in EVERY environment (not just
/// production). If a required secret is empty or a well-known dev/default value, the service
/// REFUSES to start — unless the operator has explicitly opted into insecure defaults by
/// setting <c>AllowInsecureDefaults=true</c> (local development / teaching stack only), in
/// which case a loud warning is emitted and missing secrets are filled with shared dev
/// defaults so cross-service JWT/internal-key checks stay consistent.
///
/// PRODUCTION: leave AllowInsecureDefaults=false (the default) and inject real secrets via
/// environment variables / a secret store. A deployment that forgets a secret then fails to
/// boot instead of silently running with a forgeable, well-known key.
/// </summary>
public static class SecureConfigGuard
{
    // Shared dev-only fallbacks — identical to the historical baked-in defaults so an
    // opted-in dev/demo run stays cross-service consistent. Unreachable unless the operator
    // sets AllowInsecureDefaults=true. NEVER valid in production.
    public const string DevJwtSecretKey = "your-super-secret-jwt-key-change-in-production";
    public const string DevInternalApiKey = "dev-internal-api-key-change-in-prod";
    public const string DevPinEncryptionKey = "your-secret-encryption-key";

    private static readonly string[] InsecureMarkers =
    {
        "change-in-prod", "change-in-production", "do-not-use",
        "your-secret-encryption-key", "your-super-secret", "dev-internal-api-key",
    };

    /// <summary>True if the value is empty or contains a known dev/default marker.</summary>
    public static bool IsInsecure(string? value) =>
        string.IsNullOrWhiteSpace(value) ||
        InsecureMarkers.Any(m => value!.Contains(m, StringComparison.OrdinalIgnoreCase));

    /// <summary>Return the configured value, or the dev fallback when it is empty/insecure (dev opt-in only).</summary>
    public static string OrDevDefault(string? value, string devDefault) =>
        IsInsecure(value) ? devDefault : value!;

    /// <summary>
    /// Throws <see cref="InvalidOperationException"/> unless every secret is a real
    /// (non-empty, non-default) value — OR the caller explicitly opted into insecure defaults,
    /// in which case a warning is written to stderr and startup is allowed to continue.
    /// </summary>
    public static void Assert(bool allowInsecureDefaults, params (string Name, string? Value)[] secrets)
    {
        var offenders = secrets.Where(s => IsInsecure(s.Value)).Select(s => s.Name).ToArray();
        if (offenders.Length == 0) return;

        if (allowInsecureDefaults)
        {
            Console.Error.WriteLine(
                $"[SECURITY WARNING] Starting with empty/default secret(s) [{string.Join(", ", offenders)}] " +
                "because AllowInsecureDefaults=true. This is for LOCAL DEVELOPMENT / the teaching stack ONLY — " +
                "never enable it in a shared or production environment.");
            return;
        }

        throw new InvalidOperationException(
            $"Refusing to start: insecure/empty secret(s) [{string.Join(", ", offenders)}]. " +
            "Inject real values via environment variables / a secret store, or set " +
            "AllowInsecureDefaults=true for local development only.");
    }

    /// <summary>
    /// Hard rule: the insecure-defaults opt-in is never valid in Production. This closes the
    /// gap where a committed <c>AllowInsecureDefaults=true</c> would otherwise let a production
    /// host boot on the well-known dev keys with nothing more than a stderr warning.
    /// </summary>
    public static void AssertProductionSafe(bool isProduction, bool allowInsecureDefaults)
    {
        if (isProduction && allowInsecureDefaults)
            throw new InvalidOperationException(
                "Refusing to start: AllowInsecureDefaults=true is not permitted in Production. " +
                "Set it to false and inject real secrets via environment variables / a secret store.");
    }

    /// <summary>
    /// HMAC signing keys must carry at least <paramref name="minBytes"/> bytes of material
    /// (256 bits for HS256). A short key passes the marker check above yet is brute-forceable.
    /// </summary>
    public static void AssertMinimumKeyLength(string name, string? value, int minBytes = 32)
    {
        var bytes = string.IsNullOrEmpty(value) ? 0 : System.Text.Encoding.UTF8.GetByteCount(value);
        if (bytes < minBytes)
            throw new InvalidOperationException(
                $"Refusing to start: {name} must be at least {minBytes} bytes ({minBytes * 8} bits); got {bytes}.");
    }
}
