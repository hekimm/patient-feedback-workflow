using HastaGeriBildirim.Services;
using Microsoft.Extensions.Configuration;

namespace HastaGeriBildirim.Tests;

public class TokenServiceTests
{
    [Fact]
    public void GenerateToken_ProducesUrlSafeRandomToken()
    {
        var service = CreateService();

        var token = service.GenerateToken();

        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.DoesNotContain("+", token);
        Assert.DoesNotContain("/", token);
        Assert.DoesNotContain("=", token);
    }

    [Fact]
    public void ValidateToken_ReturnsTrueOnlyForMatchingHash()
    {
        var service = CreateService();
        var token = service.GenerateToken();
        var hash = service.HashToken(token);

        Assert.True(service.ValidateToken(token, hash));
        Assert.False(service.ValidateToken(token + "x", hash));
    }

    [Fact]
    public void Constructor_ThrowsInProductionWithoutTokenSalt()
    {
        WithEnvironment("Production", null, () =>
        {
            var configuration = new ConfigurationBuilder().Build();
            Assert.Throws<InvalidOperationException>(() => new TokenService(configuration));
        });
    }

    [Fact]
    public void Constructor_AcceptsStrongProductionTokenSalt()
    {
        WithEnvironment("Production", "0123456789abcdef0123456789abcdef", () =>
        {
            var service = new TokenService(new ConfigurationBuilder().Build());
            Assert.False(string.IsNullOrWhiteSpace(service.HashToken("token")));
        });
    }

    private static TokenService CreateService()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TokenSettings:HashSalt"] = "unit-test-salt"
            })
            .Build();

        return new TokenService(configuration);
    }

    private static void WithEnvironment(string environmentName, string? tokenSalt, Action action)
    {
        var oldEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        var oldSalt = Environment.GetEnvironmentVariable("HGB_TOKEN_HASH_SALT");

        try
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", environmentName);
            Environment.SetEnvironmentVariable("HGB_TOKEN_HASH_SALT", tokenSalt);
            action();
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", oldEnvironment);
            Environment.SetEnvironmentVariable("HGB_TOKEN_HASH_SALT", oldSalt);
        }
    }
}
