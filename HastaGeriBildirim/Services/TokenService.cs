using System.Security.Cryptography;
using System.Text;

namespace HastaGeriBildirim.Services;

public class TokenService
{
    private readonly string _hashSalt;

    public TokenService(IConfiguration configuration)
    {
        var isProduction = string.Equals(
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
            "Production",
            StringComparison.OrdinalIgnoreCase);

        _hashSalt = FirstNonEmpty(
            Environment.GetEnvironmentVariable("HGB_TOKEN_HASH_SALT"),
            configuration["TokenSettings:HashSalt"],
            isProduction ? null : "HGB_DEVELOPMENT_ONLY_TOKEN_SALT")
            ?? throw new InvalidOperationException("Production ortaminda HGB_TOKEN_HASH_SALT zorunludur.");

        if (isProduction && _hashSalt.Length < 32)
            throw new InvalidOperationException("Production HGB_TOKEN_HASH_SALT en az 32 karakter olmalidir.");
    }

    public string GenerateToken()
    {
        return Guid.NewGuid().ToString("N");
    }

    public string HashToken(string token)
    {
        using var sha256 = SHA256.Create();
        var saltedToken = token + _hashSalt;
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(saltedToken));
        return Convert.ToBase64String(hashBytes);
    }

    public bool ValidateToken(string token, string hash)
    {
        var computedHash = HashToken(token);
        return computedHash == hash;
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }
}
