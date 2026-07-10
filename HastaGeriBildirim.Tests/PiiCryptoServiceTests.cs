using HastaGeriBildirim.Services;
using Microsoft.Extensions.Configuration;

namespace HastaGeriBildirim.Tests;

public class PiiCryptoServiceTests
{
    [Fact]
    public void EncryptDecrypt_RoundTripsPlainText()
    {
        var service = CreateService();

        var encrypted = service.Encrypt("+905551112233");

        Assert.NotEqual("+905551112233", encrypted);
        Assert.Equal("+905551112233", service.Decrypt(encrypted));
    }

    [Fact]
    public void HashForLookup_IsDeterministicAndCaseInsensitive()
    {
        var service = CreateService();

        var first = service.HashForLookup("Test@Example.com");
        var second = service.HashForLookup(" test@example.com ");

        Assert.Equal(first, second);
        Assert.NotNull(first);
    }

    [Fact]
    public void Constructor_ThrowsInProductionWithoutPiiKey()
    {
        WithEnvironment("Production", null, () =>
        {
            var configuration = new ConfigurationBuilder().Build();
            Assert.Throws<InvalidOperationException>(() => new PiiCryptoService(configuration));
        });
    }

    [Fact]
    public void Constructor_AcceptsStrongProductionPiiKey()
    {
        WithEnvironment("Production", "0123456789abcdef0123456789abcdef", () =>
        {
            var configuration = new ConfigurationBuilder().Build();
            var service = new PiiCryptoService(configuration);

            Assert.Equal("secret", service.Decrypt(service.Encrypt("secret")));
        });
    }

    private static PiiCryptoService CreateService()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Security:PiiEncryptionKey"] = "unit-test-encryption-key"
            })
            .Build();

        return new PiiCryptoService(configuration);
    }

    private static void WithEnvironment(string environmentName, string? piiKey, Action action)
    {
        var oldEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        var oldKey = Environment.GetEnvironmentVariable("HGB_PII_ENCRYPTION_KEY");

        try
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", environmentName);
            Environment.SetEnvironmentVariable("HGB_PII_ENCRYPTION_KEY", piiKey);
            action();
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", oldEnvironment);
            Environment.SetEnvironmentVariable("HGB_PII_ENCRYPTION_KEY", oldKey);
        }
    }
}
