using HastaGeriBildirim.Data;
using Dapper;

namespace HastaGeriBildirim.Services;

public class ProductionReadinessValidator
{
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly OracleConnectionFactory _connectionFactory;

    public ProductionReadinessValidator(
        IConfiguration configuration,
        IWebHostEnvironment environment,
        OracleConnectionFactory connectionFactory)
    {
        _configuration = configuration;
        _environment = environment;
        _connectionFactory = connectionFactory;
    }

    public IReadOnlyList<string> ValidateConfiguration(bool includeIntegrationSecrets)
    {
        var errors = new List<string>();

        if (!_environment.IsProduction())
            return errors;

        Require(errors, _configuration.GetConnectionString("OracleDb"), "ConnectionStrings__OracleDb");
        RequireStrong(errors, Read("HGB_PII_ENCRYPTION_KEY", "Security:PiiEncryptionKey"), "HGB_PII_ENCRYPTION_KEY", 32);
        RequireStrong(errors, Read("HGB_TOKEN_HASH_SALT", "TokenSettings:HashSalt"), "HGB_TOKEN_HASH_SALT", 32);
        Require(errors, Read("PublicBaseUrl", "PublicBaseUrl"), "PublicBaseUrl");
        Require(errors, Read("HGB_WEBHOOK_API_KEY", "Integrations:WebhookApiKey"), "HGB_WEBHOOK_API_KEY");
        RequireStrong(errors, Read("HGB_WEBHOOK_HMAC_SECRET", "Integrations:WebhookHmacSecret"), "HGB_WEBHOOK_HMAC_SECRET", 32);
        RequireStrong(errors, Read("WHATSAPP_APP_SECRET", "Integrations:WhatsApp:AppSecret"), "WHATSAPP_APP_SECRET", 32);

        if (includeIntegrationSecrets)
        {
            Require(errors, Read("PROBEL_SMS_BASE_URL", "Integrations:ProbelSms:BaseUrl"), "PROBEL_SMS_BASE_URL");
            if (string.IsNullOrWhiteSpace(Read("PROBEL_SMS_API_KEY", "Integrations:ProbelSms:ApiKey")) &&
                string.IsNullOrWhiteSpace(Read("PROBEL_SMS_BEARER_TOKEN", "Integrations:ProbelSms:BearerToken")))
            {
                errors.Add("PROBEL_SMS_API_KEY veya PROBEL_SMS_BEARER_TOKEN zorunludur.");
            }

            Require(errors, Read("WHATSAPP_BASE_URL", "Integrations:WhatsApp:BaseUrl"), "WHATSAPP_BASE_URL");
            Require(errors, Read("WHATSAPP_BEARER_TOKEN", "Integrations:WhatsApp:BearerToken"), "WHATSAPP_BEARER_TOKEN");
            Require(errors, Read("WHATSAPP_VERIFY_TOKEN", "Integrations:WhatsApp:VerifyToken"), "WHATSAPP_VERIFY_TOKEN");
            Require(errors, Read("PROBEL_BI_BASE_URL", "Integrations:ProbelBi:BaseUrl"), "PROBEL_BI_BASE_URL");
            Require(errors, Read("PROBEL_BI_BEARER_TOKEN", "Integrations:ProbelBi:BearerToken"), "PROBEL_BI_BEARER_TOKEN");
        }

        return errors;
    }

    public async Task<IReadOnlyList<string>> ValidateDatabaseAsync(CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();

        try
        {
            using var connection = _connectionFactory.CreateConnection();
            var tableCount = await connection.ExecuteScalarAsync<int>(
                new CommandDefinition(
                    "SELECT COUNT(*) FROM USER_TABLES WHERE TABLE_NAME IN ('HGB_SCHEMA_VERSION','HGB_USER_SCOPES','HGB_WEBHOOK_REPLAY')",
                    commandTimeout: 5,
                    cancellationToken: cancellationToken));

            if (tableCount < 3)
                errors.Add("Production hardening migration eksik: HGB_SCHEMA_VERSION/HGB_USER_SCOPES/HGB_WEBHOOK_REPLAY bekleniyor.");

            if (_environment.IsProduction())
            {
                var demoUserCount = await connection.ExecuteScalarAsync<int>(
                    new CommandDefinition(
                        "SELECT COUNT(*) FROM HGB_USERS WHERE USERNAME IN ('admin.demo','kalite.demo','birim.demo') AND STATUS = 'ACTIVE'",
                        commandTimeout: 5,
                        cancellationToken: cancellationToken));

                if (demoUserCount > 0)
                    errors.Add("Production ortaminda aktif demo kullanici bulunamaz.");
            }
        }
        catch (Exception ex)
        {
            errors.Add("Oracle readiness kontrolu basarisiz: " + ex.Message);
        }

        return errors;
    }

    public void ThrowIfProductionInvalid()
    {
        var errors = ValidateConfiguration(includeIntegrationSecrets: true);
        if (errors.Count > 0)
            throw new InvalidOperationException("Production readiness hatasi: " + string.Join(" | ", errors));
    }

    private string? Read(string envKey, string configKey)
    {
        return Environment.GetEnvironmentVariable(envKey) ?? _configuration[configKey];
    }

    private static void Require(List<string> errors, string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value) || IsUnsafePlaceholder(value))
            errors.Add($"{name} zorunludur ve demo/placeholder olamaz.");
    }

    private static void RequireStrong(List<string> errors, string? value, string name, int minimumLength)
    {
        Require(errors, value, name);
        if (!string.IsNullOrWhiteSpace(value) && value.Trim().Length < minimumLength)
            errors.Add($"{name} en az {minimumLength} karakter olmalidir.");
    }

    private static bool IsUnsafePlaceholder(string value)
    {
        var normalized = value.Trim().ToUpperInvariant();
        return normalized.Contains("CHANGE_ME") ||
               normalized.Contains("DEMO") ||
               normalized.Contains("DEVELOPMENT_ONLY") ||
               normalized.Contains("LOCALHOST") ||
               normalized == "";
    }
}
