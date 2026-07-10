using HastaGeriBildirim.Data;
using HastaGeriBildirim.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace HastaGeriBildirim.Tests;

public class ProductionReadinessValidatorTests
{
    [Fact]
    public void ValidateConfiguration_ReturnsErrorsForMissingProductionSecrets()
    {
        var validator = CreateValidator(new Dictionary<string, string?>
        {
            ["ConnectionStrings:OracleDb"] = "User Id=patient_app;Password=CHANGE_ME;Data Source=localhost:1521/FREEPDB1;"
        });

        var errors = validator.ValidateConfiguration(includeIntegrationSecrets: true);

        Assert.Contains(errors, error => error.Contains("HGB_PII_ENCRYPTION_KEY"));
        Assert.Contains(errors, error => error.Contains("HGB_WEBHOOK_HMAC_SECRET"));
        Assert.Contains(errors, error => error.Contains("PROBEL_SMS_BASE_URL"));
    }

    [Fact]
    public void ValidateConfiguration_AcceptsStrongProductionSettings()
    {
        var validator = CreateValidator(new Dictionary<string, string?>
        {
            ["ConnectionStrings:OracleDb"] = "User Id=patient_app;Password=StrongPassword;Data Source=prod-db:1521/PROD;",
            ["Security:PiiEncryptionKey"] = "0123456789abcdef0123456789abcdef",
            ["TokenSettings:HashSalt"] = "0123456789abcdef0123456789abcdef",
            ["PublicBaseUrl"] = "https://hgb.example.org",
            ["Integrations:WebhookApiKey"] = "webhook-api-key",
            ["Integrations:WebhookHmacSecret"] = "0123456789abcdef0123456789abcdef",
            ["Integrations:WhatsApp:AppSecret"] = "0123456789abcdef0123456789abcdef",
            ["Integrations:ProbelSms:BaseUrl"] = "https://sms.example.org",
            ["Integrations:ProbelSms:ApiKey"] = "sms-key",
            ["Integrations:WhatsApp:BaseUrl"] = "https://wa.example.org",
            ["Integrations:WhatsApp:BearerToken"] = "wa-token",
            ["Integrations:WhatsApp:VerifyToken"] = "verify-token",
            ["Integrations:ProbelBi:BaseUrl"] = "https://bi.example.org",
            ["Integrations:ProbelBi:BearerToken"] = "bi-token"
        });

        var errors = validator.ValidateConfiguration(includeIntegrationSecrets: true);

        Assert.Empty(errors);
    }

    private static ProductionReadinessValidator CreateValidator(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        return new ProductionReadinessValidator(
            configuration,
            new FakeEnvironment(),
            new OracleConnectionFactory(configuration));
    }

    private sealed class FakeEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "HastaGeriBildirim.Tests";
        public string WebRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
