using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AccountsService.Config;

namespace AccountsService.Utils;

/// <summary>
/// Field-level protection for the Aadhaar number (audit Finding #4).
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item><description><b>Authenticated encryption.</b> AES-256-GCM: any tampering or wrong key fails the tag check and
///   <see cref="DecryptData"/> throws — the ciphertext is never returned as if it were data.</description></item>
///   <item><description><b>Key separation.</b> The encryption key and the blind-index (HMAC) key are both derived from the
///   configured master secret with HKDF-SHA256 under different <c>info</c> labels, so the lookup hash and the
///   ciphertext no longer share key material.</description></item>
///   <item><description><b>Versioned format.</b> New ciphertext is <c>v1:</c> + base64(nonce ‖ tag ‖ ciphertext). Values without
///   the prefix are the pre-upgrade format (AES-CBC, IV-prefixed, key = SHA-256(master)) and are still readable so the
///   startup upgrade pass (<c>AadhaarCryptoUpgrade</c>) can migrate them; they are never written again.</description></item>
/// </list>
/// </remarks>
public class EncryptionManager
{
    public const string CurrentFormatPrefix = "v1:";

    private const int NonceBytes = 12;
    private const int TagBytes = 16;
    private const int KeyBytes = 32;
    private static readonly byte[] EncryptionInfo = Encoding.UTF8.GetBytes("gdb.aadhaar.encryption.v1");
    private static readonly byte[] BlindIndexInfo = Encoding.UTF8.GetBytes("gdb.aadhaar.blind-index.v1");
    private static readonly Regex TwelveDigits = new("^[0-9]{12}$", RegexOptions.Compiled);

    private readonly byte[] _encryptionKey;
    private readonly byte[] _blindIndexKey;
    private readonly byte[] _legacyKey; // SHA-256(master): the single key the pre-upgrade format used for both roles

    public EncryptionManager(Settings settings)
    {
        var master = Encoding.UTF8.GetBytes(settings.PinEncryptionKey ?? string.Empty);
        _encryptionKey = HKDF.DeriveKey(HashAlgorithmName.SHA256, master, KeyBytes, salt: null, info: EncryptionInfo);
        _blindIndexKey = HKDF.DeriveKey(HashAlgorithmName.SHA256, master, KeyBytes, salt: null, info: BlindIndexInfo);
        _legacyKey = SHA256.HashData(master);
    }

    /// <summary>True when the stored value is already in the current authenticated format.</summary>
    public static bool IsCurrentFormat(string? stored) =>
        !string.IsNullOrEmpty(stored) && stored.StartsWith(CurrentFormatPrefix, StringComparison.Ordinal);

    public string EncryptData(string plaintext)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceBytes);
        var plain = Encoding.UTF8.GetBytes(plaintext);
        var cipher = new byte[plain.Length];
        var tag = new byte[TagBytes];

        using (var aes = new AesGcm(_encryptionKey, TagBytes))
            aes.Encrypt(nonce, plain, cipher, tag);

        var payload = new byte[NonceBytes + TagBytes + cipher.Length];
        Buffer.BlockCopy(nonce, 0, payload, 0, NonceBytes);
        Buffer.BlockCopy(tag, 0, payload, NonceBytes, TagBytes);
        Buffer.BlockCopy(cipher, 0, payload, NonceBytes + TagBytes, cipher.Length);
        return CurrentFormatPrefix + Convert.ToBase64String(payload);
    }

    /// <summary>
    /// Decrypts a stored value. Throws <see cref="CryptographicException"/> on tampering, a wrong key, or an
    /// unreadable value — callers must treat that as "no data", never as the number itself.
    /// </summary>
    public string DecryptData(string stored)
    {
        if (string.IsNullOrEmpty(stored))
            throw new CryptographicException("Stored Aadhaar value is empty.");

        if (IsCurrentFormat(stored))
            return DecryptCurrent(stored.Substring(CurrentFormatPrefix.Length));

        // Pre-upgrade plaintext (the old code's "fallback for plaintext during migration"): accepted so the
        // upgrade pass can encrypt it. Twelve digits is a format check, not a proof of authenticity.
        if (TwelveDigits.IsMatch(stored))
            return stored;

        return DecryptLegacyCbc(stored);
    }

    public bool TryDecrypt(string stored, out string plaintext)
    {
        try
        {
            plaintext = DecryptData(stored);
            return true;
        }
        catch (CryptographicException)
        {
            plaintext = string.Empty;
            return false;
        }
    }

    /// <summary>Deterministic lookup hash (HMAC-SHA256 under the blind-index key) for exact-match searches.</summary>
    public string GenerateBlindIndex(string value)
    {
        using var hmac = new HMACSHA256(_blindIndexKey);
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }

    /// <summary>
    /// The hash the pre-upgrade code stored (HMAC under the shared legacy key). Used only to find and re-index
    /// rows written before the key split, and as a bridge lookup until the upgrade pass has run.
    /// </summary>
    public string GenerateLegacyBlindIndex(string value)
    {
        using var hmac = new HMACSHA256(_legacyKey);
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }

    private string DecryptCurrent(string base64)
    {
        byte[] payload;
        try { payload = Convert.FromBase64String(base64); }
        catch (FormatException ex) { throw new CryptographicException("Stored Aadhaar value is not valid base64.", ex); }

        if (payload.Length < NonceBytes + TagBytes)
            throw new CryptographicException("Stored Aadhaar value is truncated.");

        var nonce = payload.AsSpan(0, NonceBytes);
        var tag = payload.AsSpan(NonceBytes, TagBytes);
        var cipher = payload.AsSpan(NonceBytes + TagBytes);
        var plain = new byte[cipher.Length];

        // AesGcm.Decrypt throws AuthenticationTagMismatchException (a CryptographicException) on tamper / wrong key.
        using (var aes = new AesGcm(_encryptionKey, TagBytes))
            aes.Decrypt(nonce, cipher, tag, plain);

        return Encoding.UTF8.GetString(plain);
    }

    private string DecryptLegacyCbc(string base64)
    {
        try
        {
            var fullCipher = Convert.FromBase64String(base64);
            using var aes = Aes.Create();
            aes.Key = _legacyKey;
            var ivLength = aes.BlockSize / 8;
            if (fullCipher.Length <= ivLength)
                throw new CryptographicException("Legacy ciphertext is truncated.");

            var iv = new byte[ivLength];
            Array.Copy(fullCipher, 0, iv, 0, ivLength);
            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
            using var ms = new MemoryStream(fullCipher, ivLength, fullCipher.Length - ivLength);
            using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
            using var sr = new StreamReader(cs, Encoding.UTF8);
            var plaintext = sr.ReadToEnd();

            // CBC has no authentication: a wrong key can still "succeed" with garbage, so validate the shape.
            if (!TwelveDigits.IsMatch(plaintext))
                throw new CryptographicException("Legacy ciphertext did not decrypt to a valid Aadhaar number.");
            return plaintext;
        }
        catch (CryptographicException) { throw; }
        catch (Exception ex) { throw new CryptographicException("Legacy ciphertext could not be decrypted.", ex); }
    }
}
