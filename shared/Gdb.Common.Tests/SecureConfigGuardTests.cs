using Gdb.Common.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Gdb.Common.Tests;

[TestClass]
public class SecureConfigGuardTests
{
    [TestMethod]
    public void Assert_RealSecrets_DoesNotThrow_RegardlessOfFlag()
    {
        // A strong, non-default secret is accepted even with the opt-in off.
        SecureConfigGuard.Assert(allowInsecureDefaults: false,
            ("JwtSecretKey", "9f8c2b1a-Real-Prod-Secret-7d6e5f4c3b2a1908"),
            ("InternalApiKey", "k-4d3c2b1a-9f8e7d6c-Real-Internal-Key"));
    }

    [TestMethod]
    public void Assert_EmptySecret_FailsClosed_WhenNotOptedIn()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            SecureConfigGuard.Assert(allowInsecureDefaults: false, ("JwtSecretKey", "")));
    }

    [TestMethod]
    public void Assert_KnownDefaultSecret_FailsClosed_WhenNotOptedIn()
    {
        // The historical committed default must be rejected in EVERY environment by default.
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            SecureConfigGuard.Assert(allowInsecureDefaults: false,
                ("JwtSecretKey", SecureConfigGuard.DevJwtSecretKey)));
    }

    [TestMethod]
    public void Assert_DefaultSecret_Allowed_WhenExplicitlyOptedIn()
    {
        // Dev/teaching opt-in: default/empty secrets are permitted (with a stderr warning).
        SecureConfigGuard.Assert(allowInsecureDefaults: true,
            ("JwtSecretKey", ""),
            ("InternalApiKey", SecureConfigGuard.DevInternalApiKey),
            ("PinEncryptionKey", SecureConfigGuard.DevPinEncryptionKey));
    }

    [TestMethod]
    public void IsInsecure_FlagsEmptyDefaultsAndMarkers_ButNotRealSecrets()
    {
        Assert.IsTrue(SecureConfigGuard.IsInsecure(""));
        Assert.IsTrue(SecureConfigGuard.IsInsecure("   "));
        Assert.IsTrue(SecureConfigGuard.IsInsecure(null));
        Assert.IsTrue(SecureConfigGuard.IsInsecure(SecureConfigGuard.DevJwtSecretKey));
        Assert.IsTrue(SecureConfigGuard.IsInsecure("something-change-in-prod"));
        Assert.IsFalse(SecureConfigGuard.IsInsecure("a-genuinely-strong-injected-secret-value-123"));
    }

    [TestMethod]
    public void OrDevDefault_ReturnsRealValue_WhenSecure_ElseFallback()
    {
        var real = "a-genuinely-strong-injected-secret-value-123";
        Assert.AreEqual(real, SecureConfigGuard.OrDevDefault(real, SecureConfigGuard.DevJwtSecretKey));
        Assert.AreEqual(SecureConfigGuard.DevJwtSecretKey, SecureConfigGuard.OrDevDefault("", SecureConfigGuard.DevJwtSecretKey));
    }
}
