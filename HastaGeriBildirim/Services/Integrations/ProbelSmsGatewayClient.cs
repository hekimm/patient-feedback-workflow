using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace HastaGeriBildirim.Services.Integrations;

public class ProbelSmsGatewayClient : ISurveyChannelClient
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ProbelSmsGatewayClient> _logger;

    public string ChannelCode => "SMS";
    public string IntegrationSystemCode => "PROBEL_LBYS_SMS";

    public ProbelSmsGatewayClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<ProbelSmsGatewayClient> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<IntegrationSendResult> SendSurveyInvitationAsync(
        IntegrationSendRequest request,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = ReadSetting("Integrations:ProbelSms:BaseUrl", "PROBEL_SMS_BASE_URL");
        var endpoint = ReadSetting("Integrations:ProbelSms:SendPath", "PROBEL_SMS_SEND_PATH") ?? "/api/sms/send";
        var apiKey = ReadSetting("Integrations:ProbelSms:ApiKey", "PROBEL_SMS_API_KEY");
        var bearerToken = ReadSetting("Integrations:ProbelSms:BearerToken", "PROBEL_SMS_BEARER_TOKEN");
        var templateCode = ReadSetting("Integrations:ProbelSms:TemplateCode", "PROBEL_SMS_TEMPLATE_CODE") ?? "SURVEY_INVITE";

        if (string.IsNullOrWhiteSpace(baseUrl))
            return new IntegrationSendResult(false, null, null, "PROBEL_SMS_BASE_URL tanımlı değil", null);

        if (string.IsNullOrWhiteSpace(request.RecipientPhone))
            return new IntegrationSendResult(false, null, null, "Alıcı telefon numarası yok", null);

        ConfigureHttpClient();
        _httpClient.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        _httpClient.DefaultRequestHeaders.Remove("X-API-Key");

        if (!string.IsNullOrWhiteSpace(apiKey))
            _httpClient.DefaultRequestHeaders.Add("X-API-Key", apiKey);

        if (!string.IsNullOrWhiteSpace(bearerToken))
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

        var payload = new
        {
            to = request.RecipientPhone,
            templateCode,
            language = request.LanguageCode,
            message = request.Message,
            variables = new
            {
                survey_link = request.SurveyLink,
                invitation_id = request.InvitationId
            },
            metadata = new
            {
                request.InvitationId,
                request.ChannelCode,
                request.IsReminder
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
            _logger.LogError(ex, "Probel SMS gönderimi başarısız oldu");
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

        throw lastException ?? new HttpRequestException("SMS provider retry limit exceeded.");
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
            if (doc.RootElement.TryGetProperty("messageId", out var messageId))
                return messageId.GetString();
            if (doc.RootElement.TryGetProperty("id", out var id))
                return id.GetString();
        }
        catch
        {
            return null;
        }

        return null;
    }
}
