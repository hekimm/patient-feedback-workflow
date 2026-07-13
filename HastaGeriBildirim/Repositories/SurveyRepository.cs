using System.Data;
using Dapper;
using HastaGeriBildirim.Data;
using HastaGeriBildirim.Models.Entities;

namespace HastaGeriBildirim.Repositories;

public class SurveyRepository
{
    private readonly OracleConnectionFactory _connectionFactory;

    public SurveyRepository(OracleConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<SurveyInvitation?> GetInvitationByTokenAsync(string tokenHash)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                INVITATION_ID as InvitationId,
                CLINICAL_EVENT_ID as ClinicalEventId,
                TEMPLATE_VERSION_ID as TemplateId,
                PATIENT_ID as PatientId,
                SELECTED_CHANNEL_ID as ChannelId,
                TOKEN_HASH as TokenHash,
                TOKEN_EXPIRES_AT as ExpiresAt,
                SENT_AT as SentAt,
                CASE WHEN TOKEN_USED_AT IS NOT NULL OR INVITATION_STATUS = 'COMPLETED' THEN 1 ELSE 0 END as IsUsed,
                TOKEN_USED_AT as UsedAt,
                CREATED_AT as CreatedAt
            FROM HGB_SURVEY_INVITATIONS
            WHERE TOKEN_HASH = :TokenHash";

        return await connection.QueryFirstOrDefaultAsync<SurveyInvitation>(sql, new { TokenHash = tokenHash });
    }

    public async Task<string?> GetConsentTextAsync(string languageCode)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT BODY
            FROM HGB_CONSENT_TEXTS
            WHERE STATUS = 'ACTIVE'
            ORDER BY CASE WHEN LANGUAGE_CODE = :LanguageCode THEN 0 ELSE 1 END, CREATED_AT DESC
            FETCH FIRST 1 ROWS ONLY";

        return await connection.QueryFirstOrDefaultAsync<string>(sql, new { LanguageCode = languageCode });
    }

    public async Task<int> SaveConsentRecordAsync(ConsentRecord consent, string languageCode)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            INSERT INTO HGB_CONSENT_RECORDS
            (INVITATION_ID, PATIENT_ID, CONSENT_TEXT_ID, CONSENT_STATUS,
             CONSENT_SCOPE, ANONYMOUS_SELECTED, IP_HASH, GIVEN_AT)
            VALUES
            (:InvitationId, :PatientId,
             (SELECT CONSENT_TEXT_ID FROM
                (SELECT CONSENT_TEXT_ID FROM HGB_CONSENT_TEXTS
                 WHERE STATUS = 'ACTIVE'
                 ORDER BY CASE WHEN LANGUAGE_CODE = :LanguageCode THEN 0 ELSE 1 END, CREATED_AT DESC)
              WHERE ROWNUM = 1),
             :ConsentStatus, :ConsentScope, :AnonymousSelected, :IpHash, :GivenAt)
            RETURNING CONSENT_RECORD_ID INTO :ConsentId";

        var parameters = new DynamicParameters();
        parameters.Add("InvitationId", consent.InvitationId);
        parameters.Add("PatientId", consent.PatientId);
        parameters.Add("LanguageCode", languageCode);
        parameters.Add("ConsentStatus", consent.IsConsentGiven ? "ACCEPTED" : "REJECTED");
        parameters.Add("ConsentScope", "SURVEY_FEEDBACK");
        parameters.Add("AnonymousSelected", consent.IsAnonymous ? 1 : 0);
        parameters.Add("IpHash", consent.IpAddress);
        parameters.Add("GivenAt", consent.ConsentDate);
        parameters.Add("ConsentId", dbType: DbType.Int32, direction: ParameterDirection.Output);

        await connection.ExecuteAsync(sql, parameters);
        return parameters.Get<int>("ConsentId");
    }

    private const string QuestionSelect = @"
        SELECT
            QUESTION_ID as QuestionId,
            TEMPLATE_VERSION_ID as VersionId,
            QUESTION_CODE as QuestionCode,
            QUESTION_TYPE as QuestionType,
            METRIC_TYPE as MetricType,
            CASE :Lang
                WHEN 'en' THEN NVL(QUESTION_TEXT_EN, QUESTION_TEXT_TR)
                WHEN 'ar' THEN NVL(QUESTION_TEXT_AR, QUESTION_TEXT_TR)
                ELSE QUESTION_TEXT_TR
            END as QuestionText,
            HELP_TEXT as HelpText,
            QUESTION_ORDER as SortOrder,
            IS_REQUIRED as IsRequired,
            IS_INITIAL_QUESTION as IsInitialQuestion,
            MIN_VALUE as MinValue,
            MAX_VALUE as MaxValue,
            CREATED_AT as CreatedAt
        FROM HGB_SURVEY_QUESTIONS";

    public async Task<List<SurveyQuestion>> GetQuestionsForTemplateAsync(int templateVersionId, string languageCode = "tr")
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = QuestionSelect + @"
            WHERE TEMPLATE_VERSION_ID = :TemplateVersionId
            ORDER BY QUESTION_ORDER";

        var parameters = new DynamicParameters();
        parameters.Add("Lang", languageCode);
        parameters.Add("TemplateVersionId", templateVersionId);

        var results = await connection.QueryAsync<SurveyQuestion>(sql, parameters);
        return results.ToList();
    }

    public async Task<SurveyQuestion?> GetQuestionAsync(int questionId, string languageCode = "tr")
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = QuestionSelect + " WHERE QUESTION_ID = :QuestionId";

        var parameters = new DynamicParameters();
        parameters.Add("Lang", languageCode);
        parameters.Add("QuestionId", questionId);

        return await connection.QueryFirstOrDefaultAsync<SurveyQuestion>(sql, parameters);
    }

    public async Task<List<SurveyOption>> GetOptionsForQuestionAsync(int questionId, string languageCode = "tr")
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                OPTION_ID as OptionId,
                QUESTION_ID as QuestionId,
                OPTION_ORDER as OptionOrder,
                OPTION_VALUE as OptionValue,
                CASE :Lang
                    WHEN 'en' THEN NVL(OPTION_TEXT_EN, OPTION_TEXT_TR)
                    WHEN 'ar' THEN NVL(OPTION_TEXT_AR, OPTION_TEXT_TR)
                    ELSE OPTION_TEXT_TR
                END as OptionText,
                NUMERIC_VALUE as NumericValue
            FROM HGB_SURVEY_OPTIONS
            WHERE QUESTION_ID = :QuestionId
            ORDER BY OPTION_ORDER";

        var parameters = new DynamicParameters();
        parameters.Add("Lang", languageCode);
        parameters.Add("QuestionId", questionId);

        var results = await connection.QueryAsync<SurveyOption>(sql, parameters);
        return results.ToList();
    }

    public async Task<List<BranchingRule>> GetBranchingRulesForQuestionAsync(int questionId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                BRANCHING_RULE_ID as BranchingRuleId,
                SOURCE_QUESTION_ID as SourceQuestionId,
                OPERATOR_CODE as OperatorCode,
                COMPARE_NUMERIC_VALUE as CompareNumericValue,
                COMPARE_OPTION_ID as CompareOptionId,
                TARGET_QUESTION_ID as TargetQuestionId,
                RULE_ORDER as RuleOrder,
                IS_ACTIVE as IsActive
            FROM HGB_BRANCHING_RULES
            WHERE SOURCE_QUESTION_ID = :QuestionId AND IS_ACTIVE = 1
            ORDER BY RULE_ORDER";

        var results = await connection.QueryAsync<BranchingRule>(sql, new { QuestionId = questionId });
        return results.ToList();
    }

    public async Task<List<int>> GetBranchTargetQuestionIdsAsync(int templateVersionId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT DISTINCT b.TARGET_QUESTION_ID
            FROM HGB_BRANCHING_RULES b
            JOIN HGB_SURVEY_QUESTIONS q ON b.SOURCE_QUESTION_ID = q.QUESTION_ID
            WHERE q.TEMPLATE_VERSION_ID = :TemplateVersionId AND b.IS_ACTIVE = 1";

        var results = await connection.QueryAsync<int>(sql, new { TemplateVersionId = templateVersionId });
        return results.ToList();
    }

    public async Task<decimal?> GetOptionNumericValueAsync(int optionId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = "SELECT NUMERIC_VALUE FROM HGB_SURVEY_OPTIONS WHERE OPTION_ID = :OptionId";

        return await connection.QueryFirstOrDefaultAsync<decimal?>(sql, new { OptionId = optionId });
    }

    public async Task<int> CreateStartedResponseAsync(SurveyResponse response)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            INSERT INTO HGB_SURVEY_RESPONSES
            (INVITATION_ID, CLINICAL_EVENT_ID, PATIENT_ID, TEMPLATE_VERSION_ID,
             HOSPITAL_ID, BRANCH_ID, DEPARTMENT_ID, DOCTOR_ID, SERVICE_ID,
             CONSENT_RECORD_ID, IS_ANONYMOUS, RESPONSE_STATUS)
            VALUES
            (:InvitationId, :ClinicalEventId, :PatientId, :TemplateVersionId,
             :HospitalId, :BranchId, :DepartmentId, :DoctorId, :ServiceId,
             :ConsentRecordId, :IsAnonymous, 'STARTED')
            RETURNING RESPONSE_ID INTO :ResponseId";

        var parameters = new DynamicParameters();
        parameters.Add("InvitationId", response.InvitationId);
        parameters.Add("ClinicalEventId", response.ClinicalEventId);
        parameters.Add("PatientId", response.PatientId);
        parameters.Add("TemplateVersionId", response.TemplateVersionId);
        parameters.Add("HospitalId", response.HospitalId);
        parameters.Add("BranchId", response.BranchId);
        parameters.Add("DepartmentId", response.DepartmentId);
        parameters.Add("DoctorId", response.DoctorId);
        parameters.Add("ServiceId", response.ServiceId);
        parameters.Add("ConsentRecordId", response.ConsentRecordId);
        parameters.Add("IsAnonymous", response.IsAnonymous ? 1 : 0);
        parameters.Add("ResponseId", dbType: DbType.Int32, direction: ParameterDirection.Output);

        await connection.ExecuteAsync(sql, parameters);
        return parameters.Get<int>("ResponseId");
    }

    public async Task<int?> GetResponseIdForInvitationAsync(int invitationId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT RESPONSE_ID
            FROM HGB_SURVEY_RESPONSES
            WHERE INVITATION_ID = :InvitationId";

        return await connection.QueryFirstOrDefaultAsync<int?>(sql, new { InvitationId = invitationId });
    }

    public async Task<List<int>> GetAnsweredQuestionIdsAsync(int responseId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT QUESTION_ID
            FROM HGB_SURVEY_ANSWERS
            WHERE RESPONSE_ID = :ResponseId";

        var results = await connection.QueryAsync<int>(sql, new { ResponseId = responseId });
        return results.ToList();
    }

    public async Task<SurveyAnswer?> GetLastAnswerForResponseAsync(int responseId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                ANSWER_ID as AnswerId,
                RESPONSE_ID as ResponseId,
                QUESTION_ID as QuestionId,
                OPTION_ID as SelectedOptionId,
                NUMERIC_VALUE as NumericValue,
                TEXT_VALUE as TextValue,
                ANSWERED_AT as CreatedAt
            FROM HGB_SURVEY_ANSWERS
            WHERE RESPONSE_ID = :ResponseId
            ORDER BY ANSWERED_AT DESC, ANSWER_ID DESC
            FETCH FIRST 1 ROWS ONLY";

        return await connection.QueryFirstOrDefaultAsync<SurveyAnswer>(sql, new { ResponseId = responseId });
    }

    public async Task SaveAnswerAsync(SurveyAnswer answer)
    {
        using var connection = _connectionFactory.CreateConnection();

        var deleteSql = @"
            DELETE FROM HGB_SURVEY_ANSWERS
            WHERE RESPONSE_ID = :ResponseId AND QUESTION_ID = :QuestionId";

        var deleteParameters = new DynamicParameters();
        deleteParameters.Add("ResponseId", answer.ResponseId);
        deleteParameters.Add("QuestionId", answer.QuestionId);

        await connection.ExecuteAsync(deleteSql, deleteParameters);

        var sql = @"
            INSERT INTO HGB_SURVEY_ANSWERS
            (RESPONSE_ID, QUESTION_ID, OPTION_ID, NUMERIC_VALUE, TEXT_VALUE, ANSWERED_AT)
            VALUES
            (:ResponseId, :QuestionId, :OptionId, :NumericValue, :TextValue, :AnsweredAt)";

        var parameters = new DynamicParameters();
        parameters.Add("ResponseId", answer.ResponseId);
        parameters.Add("QuestionId", answer.QuestionId);
        parameters.Add("OptionId", answer.SelectedOptionId);
        parameters.Add("NumericValue", answer.NumericValue);
        parameters.Add("TextValue", answer.TextValue);
        parameters.Add("AnsweredAt", answer.CreatedAt);

        await connection.ExecuteAsync(sql, parameters);
    }

    public class ScoringAnswer
    {
        public int QuestionId { get; set; }
        public string QuestionType { get; set; } = string.Empty;
        public string? MetricType { get; set; }
        public bool IsInitialQuestion { get; set; }
        public decimal? NumericValue { get; set; }
    }

    public async Task<List<ScoringAnswer>> GetScoringAnswersAsync(int responseId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                q.QUESTION_ID as QuestionId,
                q.QUESTION_TYPE as QuestionType,
                q.METRIC_TYPE as MetricType,
                q.IS_INITIAL_QUESTION as IsInitialQuestion,
                NVL(a.NUMERIC_VALUE, o.NUMERIC_VALUE) as NumericValue
            FROM HGB_SURVEY_ANSWERS a
            JOIN HGB_SURVEY_QUESTIONS q ON a.QUESTION_ID = q.QUESTION_ID
            LEFT JOIN HGB_SURVEY_OPTIONS o ON a.OPTION_ID = o.OPTION_ID
            WHERE a.RESPONSE_ID = :ResponseId";

        var results = await connection.QueryAsync<ScoringAnswer>(sql, new { ResponseId = responseId });
        return results.ToList();
    }

    public async Task FinalizeResponseAsync(
        int responseId, decimal? overallScore, decimal? npsScore, decimal? csatScore, bool isNegative)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            UPDATE HGB_SURVEY_RESPONSES
            SET OVERALL_SCORE = :OverallScore,
                NPS_SCORE = :NpsScore,
                CSAT_SCORE = :CsatScore,
                IS_NEGATIVE = :IsNegative,
                RESPONSE_STATUS = 'SUBMITTED',
                SUBMITTED_AT = SYSTIMESTAMP
            WHERE RESPONSE_ID = :ResponseId";

        var parameters = new DynamicParameters();
        parameters.Add("OverallScore", overallScore);
        parameters.Add("NpsScore", npsScore);
        parameters.Add("CsatScore", csatScore);
        parameters.Add("IsNegative", isNegative ? 1 : 0);
        parameters.Add("ResponseId", responseId);

        await connection.ExecuteAsync(sql, parameters);
    }

    public async Task MarkInvitationUsedAsync(int invitationId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            UPDATE HGB_SURVEY_INVITATIONS
            SET TOKEN_USED_AT = SYSDATE, INVITATION_STATUS = 'COMPLETED', COMPLETED_AT = SYSDATE
            WHERE INVITATION_ID = :InvitationId";

        await connection.ExecuteAsync(sql, new { InvitationId = invitationId });
    }

    public async Task MarkInvitationOpenedAsync(int invitationId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            UPDATE HGB_SURVEY_INVITATIONS
            SET INVITATION_STATUS = 'OPENED'
            WHERE INVITATION_ID = :InvitationId AND INVITATION_STATUS IN ('SENT', 'DELIVERED')";

        await connection.ExecuteAsync(sql, new { InvitationId = invitationId });
    }
}
