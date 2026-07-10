using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace HastaGeriBildirim.Services.Integrations;

public class WhatsAppSurveyClient : IWhatsAppSurveyClient
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<WhatsAppSurveyClient> _logger;

    public string ChannelCode => "WHATSAPP";
    public string IntegrationSystemCode => "WHATSAPP_BUSINESS";

    public WhatsAppSurveyClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<WhatsAppSurveyClient> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<IntegrationSendResult> SendSurveyInvitationAsync(
        IntegrationSendRequest request,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = ReadSetting("Integrations:WhatsApp:BaseUrl", "WHATSAPP_BASE_URL");
        var endpoint = ReadSetting("Integrations:WhatsApp:SendPath", "WHATSAPP_SEND_PATH") ?? "/messages";
        var bearerToken = ReadSetting("Integrations:WhatsApp:BearerToken", "WHATSAPP_BEARER_TOKEN");
        var templateName = ReadSetting("Integrations:WhatsApp:TemplateName", "WHATSAPP_TEMPLATE_NAME") ?? "survey_invite";

        if (string.IsNullOrWhiteSpace(baseUrl))
            return new IntegrationSendResult(false, null, null, "WHATSAPP_BASE_URL tanımlı değil", null);

        if (string.IsNullOrWhiteSpace(bearerToken))
            return new IntegrationSendResult(false, null, null, "WHATSAPP_BEARER_TOKEN tanımlı değil", null);

        if (string.IsNullOrWhiteSpace(request.RecipientPhone))
            return new IntegrationSendResult(false, null, null, "Alıcı telefon numarası yok", null);

        ConfigureHttpClient();
        _httpClient.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

        var payload = new
        {
            to = request.RecipientPhone,
            type = "template",
            template = new
            {
                name = templateName,
                language = new { code = request.LanguageCode == "tr" ? "tr" : request.LanguageCode },
                components = new object[]
                {
                    new
                    {
                        type = "body",
                        parameters = new object[]
                        {
                            new { type = "text", text = request.SurveyLink }
                        }
                    }
                }
            },
            metadata = new
            {
                request.InvitationId,
                request.IsReminder,
                message = request.Message
            }
        };

        try
        {
            using var response = await SendJsonWithRetryAsync(endpoint.TrimStart('/'), payload, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            return new IntegrationSendResult(
                response.IsSuccessStatusCode,
                (int)response.StatusCode,
                TryReadProviderId(body),
                response.IsSuccessStatusCode ? null : body,
                body);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WhatsApp gönderimi başarısız oldu");
            return new IntegrationSendResult(false, null, null, ex.Message, null);
        }
    }

    public async Task<IntegrationSendResult> SendTextMessageAsync(
        string recipientPhone,
        string message,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = ReadSetting("Integrations:WhatsApp:BaseUrl", "WHATSAPP_BASE_URL");
        var endpoint = ReadSetting("Integrations:WhatsApp:SendPath", "WHATSAPP_SEND_PATH") ?? "/messages";
        var bearerToken = ReadSetting("Integrations:WhatsApp:BearerToken", "WHATSAPP_BEARER_TOKEN");

        if (string.IsNullOrWhiteSpace(baseUrl))
            return new IntegrationSendResult(false, null, null, "WHATSAPP_BASE_URL is not configured", null);

        if (string.IsNullOrWhiteSpace(bearerToken))
            return new IntegrationSendResult(false, null, null, "WHATSAPP_BEARER_TOKEN is not configured", null);

        if (string.IsNullOrWhiteSpace(recipientPhone))
            return new IntegrationSendResult(false, null, null, "Recipient phone is missing", null);

        ConfigureHttpClient();
        _httpClient.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

        var payload = new
        {
            to = recipientPhone,
            type = "text",
            text = new
            {
                preview_url = false,
                body = message
            }
        };

        try
        {
            using var response = await SendJsonWithRetryAsync(endpoint.TrimStart('/'), payload, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            return new IntegrationSendResult(
                response.IsSuccessStatusCode,
                (int)response.StatusCode,
                TryReadProviderId(body),
                response.IsSuccessStatusCode ? null : body,
                body);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WhatsApp text message failed");
            return new IntegrationSendResult(false, null, null, ex.Message, null);
        }
    }

    private string? ReadSetting(string configKey, string envKey)
    {
        return _configuration[configKey] ?? Environment.GetEnvironmentVariable(envKey);
    }

    private void ConfigureHttpClient()
    {
        var timeoutSeconds = _configuration.GetValue("Integrations:HttpTimeoutSeconds", 15);
        _httpClient.Timeout = TimeSpan.FromSeconds(Math.Clamp(timeoutSeconds, 3, 120));
    }

    private async Task<HttpResponseMessage> SendJsonWithRetryAsync<T>(
        string endpoint,
        T payload,
        CancellationToken cancellationToken)
    {
        var maxAttempts = Math.Max(1, _configuration.GetValue("Integrations:RetryCount", 3));
        Exception? lastException = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
                {
                    Content = JsonContent.Create(payload)
                };

                var response = await _httpClient.SendAsync(request, cancellationToken);
                if (!IsTransient(response.StatusCode) || attempt == maxAttempts)
                    return response;

                response.Dispose();
            }
            catch (Exception ex) when (IsTransientException(ex, cancellationToken) && attempt < maxAttempts)
            {
                lastException = ex;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250 * attempt * attempt), cancellationToken);
        }

        throw lastException ?? new HttpRequestException("WhatsApp provider retry limit exceeded.");
    }

    private static bool IsTransient(HttpStatusCode statusCode)
    {
        var code = (int)statusCode;
        return statusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests
            || code >= 500;
    }

    private static bool IsTransientException(Exception ex, CancellationToken cancellationToken)
    {
        return !cancellationToken.IsCancellationRequested
            && (ex is HttpRequestException or TaskCanceledException);
    }

    private static string? TryReadProviderId(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("messages", out var messages) &&
                messages.ValueKind == JsonValueKind.Array &&
                messages.GetArrayLength() > 0 &&
                messages[0].TryGetProperty("id", out var id))
            {
                return id.GetString();
            }
        }
        catch
        {
            return null;
        }

        return null;
    }
}
