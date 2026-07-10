using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace HastaGeriBildirim.Services.Integrations;

public class ProbelBiExportClient : IBiExportClient
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ProbelBiExportClient> _logger;

    public ProbelBiExportClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<ProbelBiExportClient> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<IntegrationSendResult> ExportFeedbackAsync(
        BiExportPayload payload,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = ReadSetting("Integrations:ProbelBi:BaseUrl", "PROBEL_BI_BASE_URL");
        var endpoint = ReadSetting("Integrations:ProbelBi:ExportPath", "PROBEL_BI_EXPORT_PATH") ?? "/api/hgb/feedback";
        var bearerToken = ReadSetting("Integrations:ProbelBi:BearerToken", "PROBEL_BI_BEARER_TOKEN");
        var isProduction = string.Equals(
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
            "Production",
            StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            if (isProduction)
                return new IntegrationSendResult(false, null, null, "PROBEL_BI_BASE_URL is not configured", null);

            return new IntegrationSendResult(true, 204, "ORACLE_VIEW_READY", null, "{\"skipped\":\"BI endpoint not configured\"}");
        }

        if (string.IsNullOrWhiteSpace(bearerToken) && isProduction)
            return new IntegrationSendResult(false, null, null, "PROBEL_BI_BEARER_TOKEN is not configured", null);

        ConfigureHttpClient();
        _httpClient.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        _httpClient.DefaultRequestHeaders.Authorization = string.IsNullOrWhiteSpace(bearerToken)
            ? null
            : new AuthenticationHeaderValue("Bearer", bearerToken);

        var request = new
        {
            correlationId = Guid.NewGuid().ToString("N"),
            payload.BiExportId,
            payload.ResponseId,
            payload.OverallScore,
            payload.NpsScore,
            payload.CsatScore,
            payload.IsNegative,
            payload.SentimentLabel,
            payload.SentimentScore,
            payload.HospitalId,
            payload.BranchId,
            payload.DepartmentId,
            payload.DoctorId,
            payload.ServiceId,
            payload.SubmittedAt
        };

        try
        {
            using var response = await SendJsonWithRetryAsync(endpoint.TrimStart('/'), request, cancellationToken);
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
            _logger.LogError(ex, "Probel BI export failed");
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

        throw lastException ?? new HttpRequestException("BI provider retry limit exceeded.");
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
            if (doc.RootElement.TryGetProperty("exportId", out var exportId))
                return exportId.GetString();
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
