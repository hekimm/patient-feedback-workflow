using System.Security.Cryptography;
using System.Text;
using HastaGeriBildirim.Repositories;

namespace HastaGeriBildirim.Services;

public class WebhookSecurityService
{
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly WebhookReplayRepository _replayRepository;

    public WebhookSecurityService(
        IConfiguration configuration,
        IWebHostEnvironment environment,
        WebhookReplayRepository replayRepository)
    {
        _configuration = configuration;
        _environment = environment;
        _replayRepository = replayRepository;
    }

    public async Task<bool> VerifyHgbAsync(HttpRequest request, string sourceSystem)
    {
        var apiKey = Read("HGB_WEBHOOK_API_KEY", "Integrations:WebhookApiKey");
        var secret = Read("HGB_WEBHOOK_HMAC_SECRET", "Integrations:WebhookHmacSecret");

        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(secret))
            return !_environment.IsProduction();

        if (!request.Headers.TryGetValue("X-HGB-API-Key", out var actualKey) ||
            !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(actualKey.ToString()),
                Encoding.UTF8.GetBytes(apiKey)))
        {
            return false;
        }

        if (!request.Headers.TryGetValue("X-HGB-Timestamp", out var timestampHeader) ||
            !long.TryParse(timestampHeader.ToString(), out var unixSeconds))
        {
            return false;
        }

        var timestamp = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        if (Math.Abs((DateTimeOffset.UtcNow - timestamp).TotalMinutes) > 5)
            return false;

        var body = await ReadRequestBodyAsync(request);
        var expected = ComputeHexHmac(secret, $"{unixSeconds}.{body}");
        var actual = request.Headers.TryGetValue("X-HGB-Signature", out var signature)
            ? NormalizeSignature(signature.ToString())
            : string.Empty;

        if (!FixedEquals(expected, actual))
            return false;

        return await _replayRepository.TryRecordAsync(expected, sourceSystem, DateTime.UtcNow);
    }

    public async Task<bool> VerifyWhatsAppAsync(HttpRequest request)
    {
        var secret = Read("WHATSAPP_APP_SECRET", "Integrations:WhatsApp:AppSecret");
        if (string.IsNullOrWhiteSpace(secret))
            return !_environment.IsProduction();

        if (!request.Headers.TryGetValue("X-Hub-Signature-256", out var signature))
            return false;

        var body = await ReadRequestBodyAsync(request);
        var expected = ComputeHexHmac(secret, body);
        var actual = NormalizeSignature(signature.ToString());

        if (!FixedEquals(expected, actual))
            return false;

        return await _replayRepository.TryRecordAsync(expected, "WHATSAPP_BUSINESS", DateTime.UtcNow);
    }

    public async Task<string> ReadRequestBodyAsync(HttpRequest request)
    {
        request.EnableBuffering();
        request.Body.Position = 0;
        using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        request.Body.Position = 0;
        return body;
    }

    private string? Read(string envKey, string configKey)
    {
        return Environment.GetEnvironmentVariable(envKey) ?? _configuration[configKey];
    }

    private static string ComputeHexHmac(string secret, string payload)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }

    private static string NormalizeSignature(string signature)
    {
        return signature.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase)
            ? signature[7..].Trim().ToLowerInvariant()
            : signature.Trim().ToLowerInvariant();
    }

    private static bool FixedEquals(string expected, string actual)
    {
        if (expected.Length != actual.Length)
            return false;

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(actual));
    }
}
