using System.Security.Cryptography;
using System.Text;

namespace HastaGeriBildirim.Services;

public class PiiCryptoService : IPiiCryptoService
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private readonly byte[] _key;
    private readonly byte[] _hashKey;

    public PiiCryptoService(IConfiguration configuration)
    {
        var isProduction = string.Equals(
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
            "Production",
            StringComparison.OrdinalIgnoreCase);

        var configuredKey =
            FirstNonEmpty(
                Environment.GetEnvironmentVariable("HGB_PII_ENCRYPTION_KEY"),
                configuration["Security:PiiEncryptionKey"]);

        if (isProduction && string.IsNullOrWhiteSpace(configuredKey))
            throw new InvalidOperationException("Production ortaminda HGB_PII_ENCRYPTION_KEY zorunludur.");

        configuredKey =
            FirstNonEmpty(
                configuredKey,
                configuration["TokenSettings:HashSalt"],
                "HGB_DEVELOPMENT_ONLY_KEY")!;

        if (isProduction && configuredKey.Length < 32)
            throw new InvalidOperationException("Production HGB_PII_ENCRYPTION_KEY en az 32 karakter olmalidir.");

        _key = NormalizeKey(configuredKey);
        _hashKey = SHA256.HashData(Encoding.UTF8.GetBytes(configuredKey + ":lookup"));
    }

    public string? Encrypt(string? plainText)
    {
        if (string.IsNullOrWhiteSpace(plainText))
            return plainText;

        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = new byte[plainBytes.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plainBytes, cipherBytes, tag);

        var combined = new byte[NonceSize + TagSize + cipherBytes.Length];
        Buffer.BlockCopy(nonce, 0, combined, 0, NonceSize);
        Buffer.BlockCopy(tag, 0, combined, NonceSize, TagSize);
        Buffer.BlockCopy(cipherBytes, 0, combined, NonceSize + TagSize, cipherBytes.Length);

        return Convert.ToBase64String(combined);
    }

    public string? Decrypt(string? cipherText)
    {
        if (string.IsNullOrWhiteSpace(cipherText))
            return cipherText;

        try
        {
            var combined = Convert.FromBase64String(cipherText);
            if (combined.Length <= NonceSize + TagSize)
                return cipherText;

            var nonce = combined[..NonceSize];
            var tag = combined[NonceSize..(NonceSize + TagSize)];
            var cipherBytes = combined[(NonceSize + TagSize)..];
            var plainBytes = new byte[cipherBytes.Length];

            using var aes = new AesGcm(_key, TagSize);
            aes.Decrypt(nonce, cipherBytes, tag, plainBytes);

            return Encoding.UTF8.GetString(plainBytes);
        }
        catch
        {
            return cipherText;
        }
    }

    public string? HashForLookup(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Trim().ToUpperInvariant();
        using var hmac = new HMACSHA256(_hashKey);
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(normalized)));
    }

    private static byte[] NormalizeKey(string configuredKey)
    {
        try
        {
            var raw = Convert.FromBase64String(configuredKey);
            if (raw.Length is 16 or 24 or 32)
                return raw.Length == 32 ? raw : SHA256.HashData(raw);
        }
        catch
        {
        }

        return SHA256.HashData(Encoding.UTF8.GetBytes(configuredKey));
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }
}
