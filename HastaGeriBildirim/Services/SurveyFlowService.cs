using HastaGeriBildirim.Models.Entities;
using HastaGeriBildirim.Repositories;

namespace HastaGeriBildirim.Services;

public class SurveyFlowService
{
    private const decimal DefaultNegativeThreshold = 6m;

    private readonly SurveyRepository _surveyRepository;
    private readonly AlertRepository _alertRepository;
    private readonly ServiceRecoveryRepository _recoveryRepository;
    private readonly ClinicalEventRepository _clinicalEventRepository;
    private readonly TriggerRuleRepository _triggerRuleRepository;
    private readonly SettingsRepository _settingsRepository;
    private readonly SentimentService _sentimentService;
    private readonly MaintenanceRepository _maintenanceRepository;
    private readonly AuditService _auditService;
    private readonly TokenService _tokenService;
    private readonly int _defaultRecoveryHours;

    public SurveyFlowService(
        SurveyRepository surveyRepository,
        AlertRepository alertRepository,
        ServiceRecoveryRepository recoveryRepository,
        ClinicalEventRepository clinicalEventRepository,
        TriggerRuleRepository triggerRuleRepository,
        SettingsRepository settingsRepository,
        SentimentService sentimentService,
        MaintenanceRepository maintenanceRepository,
        AuditService auditService,
        TokenService tokenService,
        IConfiguration configuration)
    {
        _surveyRepository = surveyRepository;
        _alertRepository = alertRepository;
        _recoveryRepository = recoveryRepository;
        _clinicalEventRepository = clinicalEventRepository;
        _triggerRuleRepository = triggerRuleRepository;
        _settingsRepository = settingsRepository;
        _sentimentService = sentimentService;
        _maintenanceRepository = maintenanceRepository;
        _auditService = auditService;
        _tokenService = tokenService;
        _defaultRecoveryHours = configuration.GetValue("SlaSettings:DefaultRecoveryHours", 24);
    }

    public async Task<(bool isValid, SurveyInvitation? invitation, string? errorMessage)>
        ValidateTokenAsync(string token)
    {
        var tokenHash = _tokenService.HashToken(token);
        var invitation = await _surveyRepository.GetInvitationByTokenAsync(tokenHash);

        if (invitation == null)
            return (false, null, "Geçersiz davet bağlantısı");

        if (invitation.ExpiresAt < DateTime.Now)
            return (false, null, "Davet süresi dolmuş");

        if (invitation.IsUsed)
            return (false, null, "Bu anketi daha önce yanıtladınız");

        return (true, invitation, null);
    }

    public async Task MarkOpenedAsync(int invitationId)
    {
        await _surveyRepository.MarkInvitationOpenedAsync(invitationId);
    }

    public async Task<string?> GetConsentTextAsync(string languageCode)
    {
        return await _surveyRepository.GetConsentTextAsync(languageCode);
    }

    public async Task<int> SaveConsentAndStartResponseAsync(
        SurveyInvitation invitation,
        bool isConsentGiven,
        bool isAnonymous,
        string? ipAddress,
        string languageCode)
    {
        var consent = new ConsentRecord
        {
            PatientId = invitation.PatientId,
            InvitationId = invitation.InvitationId,
            IsConsentGiven = isConsentGiven,
            IsAnonymous = isAnonymous,
            IpAddress = ipAddress,
            ConsentDate = DateTime.Now,
            CreatedAt = DateTime.Now
        };

        var consentRecordId = await _surveyRepository.SaveConsentRecordAsync(consent, languageCode);

        await _auditService.AddLogAsync(
            "CONSENT_RECORD",
            invitation.InvitationId,
            "CONSENT_GIVEN",
            null,
            invitation.PatientId,
            $"KVKK rızası verildi. Anonim: {isAnonymous}",
            ipAddress);

        var existingResponseId = await _surveyRepository.GetResponseIdForInvitationAsync(invitation.InvitationId);
        if (existingResponseId.HasValue)
            return existingResponseId.Value;

        var clinicalEvent = await _clinicalEventRepository.GetEventByIdAsync(invitation.ClinicalEventId);

        var response = new SurveyResponse
        {
            InvitationId = invitation.InvitationId,
            PatientId = isAnonymous ? null : invitation.PatientId,
            ClinicalEventId = invitation.ClinicalEventId,
            TemplateVersionId = invitation.TemplateId,
            HospitalId = clinicalEvent?.HospitalId,
            BranchId = clinicalEvent?.BranchId,
            DepartmentId = clinicalEvent?.DepartmentId,
            DoctorId = clinicalEvent?.DoctorId,
            ServiceId = clinicalEvent?.ServiceId,
            ConsentRecordId = consentRecordId,
            IsAnonymous = isAnonymous
        };

        return await _surveyRepository.CreateStartedResponseAsync(response);
    }

    public async Task<SurveyQuestion?> GetFirstQuestionAsync(int templateVersionId, string languageCode)
    {
        var questions = await _surveyRepository.GetQuestionsForTemplateAsync(templateVersionId, languageCode);
        if (questions.Count == 0)
            return null;

        var initial = questions.FirstOrDefault(q => q.IsInitialQuestion);
        if (initial != null)
            return initial;

        var targetIds = await _surveyRepository.GetBranchTargetQuestionIdsAsync(templateVersionId);
        return questions.FirstOrDefault(q => !targetIds.Contains(q.QuestionId)) ?? questions[0];
    }

    public async Task<SurveyQuestion?> GetQuestionAsync(int questionId, string languageCode)
    {
        return await _surveyRepository.GetQuestionAsync(questionId, languageCode);
    }

    public async Task<List<SurveyOption>> GetOptionsAsync(int questionId, string languageCode)
    {
        return await _surveyRepository.GetOptionsForQuestionAsync(questionId, languageCode);
    }

    public async Task SaveAnswerAsync(
        int responseId,
        int questionId,
        int? selectedOptionId,
        decimal? numericValue,
        string? textValue)
    {
        var answer = new SurveyAnswer
        {
            ResponseId = responseId,
            QuestionId = questionId,
            SelectedOptionId = selectedOptionId,
            NumericValue = numericValue,
            TextValue = textValue,
            CreatedAt = DateTime.Now
        };

        await _surveyRepository.SaveAnswerAsync(answer);
    }

    public async Task<int?> GetNextQuestionIdAsync(
        int templateVersionId,
        int responseId,
        int answeredQuestionId,
        decimal? numericValue,
        int? selectedOptionId)
    {
        var answeredIds = await _surveyRepository.GetAnsweredQuestionIdsAsync(responseId);

        var compareValue = numericValue;
        if (compareValue == null && selectedOptionId.HasValue)
            compareValue = await _surveyRepository.GetOptionNumericValueAsync(selectedOptionId.Value);

        var branchingRules = await _surveyRepository.GetBranchingRulesForQuestionAsync(answeredQuestionId);
        foreach (var rule in branchingRules)
        {
            if (RuleMatches(rule, compareValue, selectedOptionId) &&
                !answeredIds.Contains(rule.TargetQuestionId))
            {
                return rule.TargetQuestionId;
            }
        }

        var questions = await _surveyRepository.GetQuestionsForTemplateAsync(templateVersionId);
        var targetIds = await _surveyRepository.GetBranchTargetQuestionIdsAsync(templateVersionId);
        var answeredQuestion = questions.FirstOrDefault(q => q.QuestionId == answeredQuestionId);
        var answeredOrder = answeredQuestion?.SortOrder ?? 0;

        var next = questions.FirstOrDefault(q =>
            q.SortOrder > answeredOrder &&
            !targetIds.Contains(q.QuestionId) &&
            !answeredIds.Contains(q.QuestionId));

        return next?.QuestionId;
    }

    private static bool RuleMatches(BranchingRule rule, decimal? compareValue, int? selectedOptionId)
    {
        if (rule.CompareOptionId.HasValue)
            return selectedOptionId.HasValue && rule.CompareOptionId.Value == selectedOptionId.Value;

        if (compareValue == null || rule.CompareNumericValue == null)
            return false;

        return rule.OperatorCode switch
        {
            "EQ" => compareValue == rule.CompareNumericValue,
            "NE" => compareValue != rule.CompareNumericValue,
            "LT" => compareValue < rule.CompareNumericValue,
            "LTE" => compareValue <= rule.CompareNumericValue,
            "GT" => compareValue > rule.CompareNumericValue,
            "GTE" => compareValue >= rule.CompareNumericValue,
            _ => false
        };
    }

    public async Task<(int current, int total)> GetProgressAsync(
        int templateVersionId, int responseId, int currentQuestionId)
    {
        var questions = await _surveyRepository.GetQuestionsForTemplateAsync(templateVersionId);
        var targetIds = await _surveyRepository.GetBranchTargetQuestionIdsAsync(templateVersionId);
        var answeredIds = await _surveyRepository.GetAnsweredQuestionIdsAsync(responseId);

        var currentQuestion = questions.FirstOrDefault(q => q.QuestionId == currentQuestionId);
        var currentOrder = currentQuestion?.SortOrder ?? 0;

        var remaining = questions.Count(q =>
            q.SortOrder > currentOrder &&
            !targetIds.Contains(q.QuestionId) &&
            !answeredIds.Contains(q.QuestionId));

        var current = answeredIds.Count + 1;
        return (current, current + remaining);
    }

    public async Task<int> FinalizeResponseAsync(SurveyInvitation invitation, int responseId, bool isAnonymous)
    {
        var scoringAnswers = await _surveyRepository.GetScoringAnswersAsync(responseId);

        var npsScore = scoringAnswers.FirstOrDefault(a => a.MetricType == "NPS")?.NumericValue;
        var csatScore = scoringAnswers.FirstOrDefault(a => a.MetricType == "CSAT")?.NumericValue;

        var overallScore = scoringAnswers.FirstOrDefault(a => a.IsInitialQuestion && a.NumericValue.HasValue)?.NumericValue;
        if (overallScore == null)
        {
            var numericValues = scoringAnswers
                .Where(a => a.NumericValue.HasValue)
                .Select(a => a.NumericValue!.Value)
                .ToList();

            if (numericValues.Count > 0)
                overallScore = Math.Round(numericValues.Average(), 2);
        }

        var rule = await _triggerRuleRepository.GetRuleByInvitationAsync(invitation.InvitationId);
        var threshold = rule?.LowScoreThreshold ?? await GetDefaultThresholdAsync();

        var isNegative = overallScore.HasValue && overallScore.Value <= threshold;

        await _surveyRepository.FinalizeResponseAsync(responseId, overallScore, npsScore, csatScore, isNegative);
        await _surveyRepository.MarkInvitationUsedAsync(invitation.InvitationId);

        await _sentimentService.AnalyzeResponseAsync(responseId);

        if (isNegative)
        {
            var clinicalEvent = await _clinicalEventRepository.GetEventByIdAsync(invitation.ClinicalEventId);
            var slaHours = rule?.ServiceRecoverySlaHours ?? _defaultRecoveryHours;
            await CreateRecoveryCaseAsync(invitation, responseId, isAnonymous,
                overallScore ?? 0, clinicalEvent?.DepartmentId, slaHours);
        }

        await _maintenanceRepository.EnqueueBiExportAsync(responseId);

        await _auditService.AddLogAsync(
            "SURVEY_RESPONSE",
            responseId,
            "COMPLETED",
            null,
            isAnonymous ? null : invitation.PatientId,
            $"Anket tamamlandı. Puan: {overallScore}",
            null);

        return responseId;
    }

    private async Task<decimal> GetDefaultThresholdAsync()
    {
        var settingValue = await _settingsRepository.GetValueAsync("DEFAULT_LOW_SCORE_THRESHOLD");

        if (decimal.TryParse(settingValue, out var threshold))
            return threshold;

        return DefaultNegativeThreshold;
    }

    private async Task CreateRecoveryCaseAsync(
        SurveyInvitation invitation,
        int responseId,
        bool isAnonymous,
        decimal overallScore,
        int? departmentId,
        int slaHours)
    {
        var alert = new Alert
        {
            AlertType = "LOW_SCORE",
            Severity = "HIGH",
            Status = "OPEN",
            ResponseId = responseId,
            Message = $"Düşük memnuniyet puanı: {overallScore}",
            CreatedAt = DateTime.Now
        };

        var alertId = await _alertRepository.CreateAlertAsync(alert);

        var recoveryCase = new ServiceRecoveryCase
        {
            AlertId = alertId,
            ResponseId = responseId,
            PatientId = isAnonymous ? null : invitation.PatientId,
            DepartmentId = departmentId,
            Status = "OPEN",
            Priority = "MEDIUM",
            DueDate = DateTime.Now.AddHours(slaHours),
            CreatedAt = DateTime.Now
        };

        await _recoveryRepository.CreateCaseAsync(recoveryCase);
    }
}
