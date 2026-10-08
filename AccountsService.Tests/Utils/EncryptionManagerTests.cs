using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using AccountsService.Config;
using AccountsService.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AccountsService.Tests.Utils;

/// <summary>
/// The Aadhaar protection contract: authenticated encryption that fails closed, key separation
/// between ciphertext and lookup hash, and read-compatibility with the pre-upgrade CBC format.
/// </summary>
[TestClass]
public class EncryptionManagerTests
{
    private const string Master = "unit-test-master-secret-with-plenty-of-entropy";
    private const string Aadhaar = "456789012345";

    private static EncryptionManager Manager(string master = Master) => new(new Settings { PinEncryptionKey = master });

    [TestMethod]
    public void EncryptData_ProducesVersionedFormat_AndRoundTrips()
    {
        var m = Manager();
        var stored = m.EncryptData(Aadhaar);

        Assert.IsTrue(EncryptionManager.IsCurrentFormat(stored));
        Assert.AreEqual(Aadhaar, m.DecryptData(stored));
    }

    [TestMethod]
    public void EncryptData_UsesFreshNonce_SoEqualPlaintextsDiffer()
    {
        var m = Manager();
        Assert.AreNotEqual(m.EncryptData(Aadhaar), m.EncryptData(Aadhaar));
    }

    [TestMethod]
    public void DecryptData_Tampered_ThrowsInsteadOfReturningGarbage()
    {
        var m = Manager();
        var stored = m.EncryptData(Aadhaar);
        var payload = Convert.FromBase64String(stored.Substring(EncryptionManager.CurrentFormatPrefix.Length));
        payload[^1] ^= 0x01; // flip one ciphertext bit
        var tampered = EncryptionManager.CurrentFormatPrefix + Convert.ToBase64String(payload);

        Assert.ThrowsExactly<AuthenticationTagMismatchException>(() => m.DecryptData(tampered));
        Assert.IsFalse(m.TryDecrypt(tampered, out var plain));
        Assert.AreEqual(string.Empty, plain);
    }

    [TestMethod]
    public void DecryptData_WrongKey_FailsClosed()
    {
        var stored = Manager().EncryptData(Aadhaar);
        var other = Manager("a-completely-different-master-secret-value");

        Assert.IsFalse(other.TryDecrypt(stored, out _));
    }

    [TestMethod]
    public void DecryptData_Garbage_ThrowsCryptographicException()
    {
        var m = Manager();
        Assert.ThrowsExactly<CryptographicException>(() => m.DecryptData("EDw=not-a-real-ciphertext"));
        Assert.ThrowsExactly<CryptographicException>(() => m.DecryptData(string.Empty));
    }

    [TestMethod]
    public void DecryptData_LegacyCbcCiphertext_IsStillReadable()
    {
        // Reproduce exactly what the pre-upgrade code wrote: AES-CBC, IV prefixed, key = SHA-256(master).
        var legacyKey = SHA256.HashData(Encoding.UTF8.GetBytes(Master));
        using var aes = Aes.Create();
        aes.Key = legacyKey;
        aes.GenerateIV();
        using var ms = new MemoryStream();
        ms.Write(aes.IV, 0, aes.IV.Length);
        using (var cs = new CryptoStream(ms, aes.CreateEncryptor(aes.Key, aes.IV), CryptoStreamMode.Write))
        using (var sw = new StreamWriter(cs))
            sw.Write(Aadhaar);
        var legacyStored = Convert.ToBase64String(ms.ToArray());

        Assert.IsFalse(EncryptionManager.IsCurrentFormat(legacyStored));
        Assert.AreEqual(Aadhaar, Manager().DecryptData(legacyStored));
    }

    [TestMethod]
    public void DecryptData_LegacyPlaintextTwelveDigits_IsAcceptedForUpgrade()
        => Assert.AreEqual(Aadhaar, Manager().DecryptData(Aadhaar));

    [TestMethod]
    public void BlindIndex_IsDeterministic_AndSeparateFromLegacyKey()
    {
        var m = Manager();
        var current = m.GenerateBlindIndex(Aadhaar);

        Assert.AreEqual(current, m.GenerateBlindIndex(Aadhaar));
        Assert.AreNotEqual(current, m.GenerateBlindIndex("456789012346"));
        Assert.AreNotEqual(current, m.GenerateLegacyBlindIndex(Aadhaar), "blind-index key must not equal the legacy shared key");
        Assert.AreEqual(64, current.Length); // HMAC-SHA256 hex
    }
}
