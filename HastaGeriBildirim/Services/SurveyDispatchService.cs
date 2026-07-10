using System.Text.Json;
using HastaGeriBildirim.Models.Entities;
using HastaGeriBildirim.Models.ViewModels;
using HastaGeriBildirim.Repositories;
using HastaGeriBildirim.Services.Integrations;

namespace HastaGeriBildirim.Services;

public class SurveyDispatchService
{
    private const int MaxQueueRetryCount = 3;
    private const int RetryDelayMinutes = 5;
    private const string InviteTemplateCode = "SURVEY_INVITE";

    private readonly DispatchRepository _dispatchRepository;
    private readonly TriggerRuleRepository _triggerRuleRepository;
    private readonly SettingsRepository _settingsRepository;
    private readonly TokenService _tokenService;
    private readonly AuditService _auditService;
    private readonly IReadOnlyDictionary<string, ISurveyChannelClient> _channelClients;
    private readonly IConfiguration _configuration;

    public SurveyDispatchService(
        DispatchRepository dispatchRepository,
        TriggerRuleRepository triggerRuleRepository,
        SettingsRepository settingsRepository,
        TokenService tokenService,
        AuditService auditService,
        IEnumerable<ISurveyChannelClient> channelClients,
        IConfiguration configuration)
    {
        _dispatchRepository = dispatchRepository;
        _triggerRuleRepository = triggerRuleRepository;
        _settingsRepository = settingsRepository;
        _tokenService = tokenService;
        _auditService = auditService;
        _channelClients = channelClients.ToDictionary(
            client => client.ChannelCode, StringComparer.OrdinalIgnoreCase);
        _configuration = configuration;
    }

    public async Task RunAllAsync()
    {
        await ProcessPendingEventsAsync();
        await ProcessDueQueueAsync();
        await ProcessRemindersAsync();
        await _dispatchRepository.ExpireOverdueInvitationsAsync();
    }

    public async Task ProcessPendingEventsAsync()
    {
        var events = await _dispatchRepository.GetPendingEventsAsync();

        foreach (var clinicalEvent in events)
        {
            if (clinicalEvent.IsSensitiveCase)
            {
                await _dispatchRepository.SetEventStatusAsync(clinicalEvent.EventId, "IGNORED_SENSITIVE");
                await _auditService.AddLogAsync(
                    "CLINICAL_EVENT", clinicalEvent.EventId, "SKIPPED_SENSITIVE",
                    null, clinicalEvent.PatientId, "Hassas vaka nedeniyle anket gönderilmedi", null);
                continue;
            }

            var rule = await _triggerRuleRepository.GetEnabledRuleForEventAsync(clinicalEvent.EventType);
            if (rule == null)
            {
                await _dispatchRepository.SetEventStatusAsync(clinicalEvent.EventId, "CANCELLED");
                continue;
            }

            var patient = clinicalEvent.PatientId.HasValue
                ? await _dispatchRepository.GetPatientContactAsync(clinicalEvent.PatientId.Value)
                : null;

            if (patient == null || !patient.AllowContact)
            {
                await _dispatchRepository.SetEventStatusAsync(clinicalEvent.EventId, "CANCELLED");
                await _auditService.AddLogAsync(
                    "CLINICAL_EVENT", clinicalEvent.EventId, "SKIPPED_NO_CONTACT",
                    null, clinicalEvent.PatientId, "Hasta iletişim izni yok veya kayıt bulunamadı", null);
                continue;
            }

            var recentCount = await _dispatchRepository.CountRecentInvitationsAsync(
                patient.PatientId, rule.FrequencyCapDays);

            if (recentCount >= rule.FrequencyCapCount)
            {
                await _dispatchRepository.SetEventStatusAsync(clinicalEvent.EventId, "IGNORED_FREQUENCY_CAP");
                continue;
            }

            var scheduledAt = clinicalEvent.EventDate.AddMinutes(rule.DelayMinutes);
            if (scheduledAt < DateTime.Now)
                scheduledAt = DateTime.Now;

            await _dispatchRepository.EnqueueEventAsync(clinicalEvent.EventId, scheduledAt);
            await _dispatchRepository.SetEventStatusAsync(clinicalEvent.EventId, "QUEUED");
        }
    }

    public async Task ProcessDueQueueAsync()
    {
        var dueItems = await _dispatchRepository.GetDueQueueItemsAsync();

        foreach (var item in dueItems)
        {
            await _dispatchRepository.SetQueueStatusAsync(item.QueueId, "PROCESSING", null);

            try
            {
                var rule = await _triggerRuleRepository.GetEnabledRuleForEventAsync(item.EventType);
                if (rule == null)
                {
                    await _dispatchRepository.SetQueueStatusAsync(item.QueueId, "CANCELLED", "Etkin tetikleme kuralı yok");
                    continue;
                }

                var versionId = await _dispatchRepository.GetLatestPublishedVersionIdAsync(rule.SurveyTemplateId);
                if (versionId == null)
                {
                    await FailOrRescheduleAsync(item, "Şablonun yayınlanmış sürümü yok");
                    continue;
                }

                var token = _tokenService.GenerateToken();
                var tokenHash = _tokenService.HashToken(token);
                var expiresAt = DateTime.Now.AddHours(await GetTokenTtlHoursAsync());

                var invitationId = await _dispatchRepository.CreateInvitationAsync(
                    item.ClinicalEventId, item.PatientId, versionId.Value,
                    rule.TriggerRuleId, rule.PrimaryChannelId, tokenHash, expiresAt);

                var sent = await SendWithFallbackAsync(invitationId, rule, item.PatientId, token);

                if (sent)
                {
                    await _dispatchRepository.SetInvitationStatusAsync(invitationId, "SENT");
                    await _dispatchRepository.SetEventStatusAsync(item.ClinicalEventId, "INVITED");
                    await _dispatchRepository.SetQueueStatusAsync(item.QueueId, "COMPLETED", null);

                    await _auditService.AddLogAsync(
                        "SURVEY_INVITATION", invitationId, "SENT",
                        null, item.PatientId, $"Anket daveti gönderildi. Olay: {item.EventType}", null);
                }
                else
                {
                    await _dispatchRepository.SetInvitationStatusAsync(invitationId, "FAILED");
                    await FailOrRescheduleAsync(item, "Hiçbir kanaldan gönderilemedi");
                }
            }
            catch (Exception ex)
            {
                await FailOrRescheduleAsync(item, ex.Message);
            }
        }
    }

    private async Task<bool> SendWithFallbackAsync(int invitationId, TriggerRule rule, int patientId, string token)
    {
        var patient = await _dispatchRepository.GetPatientContactAsync(patientId);
        var language = patient?.PreferredLanguage ?? "tr";

        var primarySent = await TrySendOnChannelAsync(invitationId, rule.PrimaryChannelId, patient, language, token, false);
        if (primarySent)
            return true;

        if (rule.FallbackChannelId.HasValue)
        {
            var fallbackSent = await TrySendOnChannelAsync(invitationId, rule.FallbackChannelId.Value, patient, language, token, false);
            if (fallbackSent)
            {
                await _dispatchRepository.SetInvitationChannelAsync(invitationId, rule.FallbackChannelId.Value);
                return true;
            }
        }

        return false;
    }

    private async Task<bool> TrySendOnChannelAsync(
        int invitationId, int channelId, PatientContact? patient, string language, string token, bool isReminder)
    {
        var attemptNo = await _dispatchRepository.GetNextAttemptNoAsync(invitationId);
        var channel = await _dispatchRepository.GetChannelByIdAsync(channelId);

        if (channel == null || !channel.IsActive)
        {
            await _dispatchRepository.AddDeliveryAttemptAsync(
                invitationId, channelId, attemptNo, "FAILED", "Kanal pasif veya tanımsız");
            return false;
        }

        var body = await _dispatchRepository.GetChannelTemplateBodyAsync(channelId, InviteTemplateCode, language);
        if (body == null)
        {
            await _dispatchRepository.AddDeliveryAttemptAsync(
                invitationId, channelId, attemptNo, "FAILED", "Kanal mesaj şablonu bulunamadı");
            return false;
        }

        var surveyLink = await BuildSurveyLinkAsync(token, language);
        var message = body
            .Replace("{{survey_link}}", surveyLink)
            .Replace("{{patient_name}}", patient?.FullName ?? "Hastamız");

        var client = _channelClients.GetValueOrDefault(channel.ChannelCode);
        var systemCode = client?.IntegrationSystemCode ?? "PROBEL_HBYS";

        var operation = isReminder ? "SURVEY_REMINDER" : "SURVEY_INVITE";
        var correlationId = Guid.NewGuid().ToString("N");
        var payload = JsonSerializer.Serialize(new
        {
            correlationId,
            kanal = channel.ChannelCode,
            invitationId,
            reminder = isReminder
        });

        var request = new IntegrationSendRequest(
            invitationId,
            channel.ChannelCode,
            patient?.Phone,
            message,
            surveyLink,
            language,
            isReminder);

        var sendResult = client != null
            ? await client.SendSurveyInvitationAsync(request)
            : new IntegrationSendResult(true, 200, null, null, "{\"localChannel\":true}");

        await _dispatchRepository.AddDeliveryAttemptAsync(
            invitationId,
            channelId,
            attemptNo,
            sendResult.Success ? "SENT" : "FAILED",
            sendResult.ErrorMessage);

        await _dispatchRepository.InsertIntegrationLogAsync(
            systemCode,
            "OUTBOUND",
            operation,
            payload,
            sendResult.Success,
            sendResult.ErrorMessage,
            sendResult.ResponsePayload,
            sendResult.HttpStatusCode,
            sendResult.ProviderMessageId,
            correlationId);

        return sendResult.Success;
    }

    public async Task ProcessRemindersAsync()
    {
        var candidates = await _dispatchRepository.GetReminderCandidatesAsync();

        foreach (var candidate in candidates)
        {
            var invitationId = candidate.QueueId;

            var rule = await _triggerRuleRepository.GetRuleByInvitationAsync(invitationId);
            if (rule == null)
                continue;

            var patient = await _dispatchRepository.GetPatientContactAsync(candidate.PatientId);
            if (patient == null || !patient.AllowContact)
                continue;

            var token = _tokenService.GenerateToken();
            var tokenHash = _tokenService.HashToken(token);
            var expiresAt = DateTime.Now.AddHours(await GetTokenTtlHoursAsync());

            await _dispatchRepository.UpdateInvitationTokenAsync(invitationId, tokenHash, expiresAt);

            var sent = await TrySendOnChannelAsync(
                invitationId, rule.PrimaryChannelId, patient, patient.PreferredLanguage, token, true);

            if (sent)
            {
                await _auditService.AddLogAsync(
                    "SURVEY_INVITATION", invitationId, "REMINDER_SENT",
                    null, candidate.PatientId, "Anket hatırlatması gönderildi", null);
            }
        }
    }

    public async Task<string> RegenerateInvitationLinkAsync(int invitationId, int performedByUserId)
    {
        var token = _tokenService.GenerateToken();
        var tokenHash = _tokenService.HashToken(token);
        var expiresAt = DateTime.Now.AddHours(await GetTokenTtlHoursAsync());

        await _dispatchRepository.UpdateInvitationTokenAsync(invitationId, tokenHash, expiresAt);

        await _auditService.AddLogAsync(
            "SURVEY_INVITATION", invitationId, "TOKEN_REGENERATED",
            performedByUserId, null, "Anket erişim bağlantısı yenilendi", null);

        var language = await _dispatchRepository.GetInvitationPatientLanguageAsync(invitationId);
        return await BuildSurveyLinkAsync(token, language);
    }

    public async Task<string> BuildSurveyLinkAsync(string token, string? languageCode = null)
    {
        var baseUrl = await _settingsRepository.GetValueAsync("PUBLIC_BASE_URL")
            ?? _configuration["PublicBaseUrl"]
            ?? "http://localhost:5080";

        var link = $"{baseUrl.TrimEnd('/')}/Survey/Start?token={token}";

        var language = SurveyTexts.NormalizeLang(languageCode?.Trim().ToLowerInvariant());
        return language == "tr" ? link : $"{link}&lang={language}";
    }

    private async Task<int> GetTokenTtlHoursAsync()
    {
        var settingValue = await _settingsRepository.GetValueAsync("DEFAULT_TOKEN_TTL_HOURS");

        if (int.TryParse(settingValue, out var hours) && hours > 0)
            return hours;

        return _configuration.GetValue("TokenSettings:ExpirationHours", 72);
    }

    private async Task FailOrRescheduleAsync(EventQueueItem item, string errorMessage)
    {
        if (item.RetryCount < MaxQueueRetryCount)
        {
            await _dispatchRepository.RescheduleQueueItemAsync(
                item.QueueId, DateTime.Now.AddMinutes(RetryDelayMinutes), errorMessage);
        }
        else
        {
            await _dispatchRepository.SetQueueStatusAsync(item.QueueId, "FAILED", errorMessage);
            await _dispatchRepository.SetEventStatusAsync(item.ClinicalEventId, "ERROR");
        }
    }
}
