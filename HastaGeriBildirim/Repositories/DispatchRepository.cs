using System.Data;
using Dapper;
using HastaGeriBildirim.Data;
using HastaGeriBildirim.Models.Entities;
using HastaGeriBildirim.Models.ViewModels;
using HastaGeriBildirim.Services;

namespace HastaGeriBildirim.Repositories;

public class DispatchRepository
{
    private readonly OracleConnectionFactory _connectionFactory;
    private readonly IPiiCryptoService _piiCryptoService;

    public DispatchRepository(
        OracleConnectionFactory connectionFactory,
        IPiiCryptoService piiCryptoService)
    {
        _connectionFactory = connectionFactory;
        _piiCryptoService = piiCryptoService;
    }

    public async Task<List<ClinicalEvent>> GetPendingEventsAsync(int limit = 100)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                CLINICAL_EVENT_ID as EventId,
                EXTERNAL_EVENT_REF as ExternalEventId,
                EVENT_TYPE as EventType,
                PATIENT_ID as PatientId,
                HOSPITAL_ID as HospitalId,
                BRANCH_ID as BranchId,
                DEPARTMENT_ID as DepartmentId,
                DOCTOR_ID as DoctorId,
                SERVICE_ID as ServiceId,
                EVENT_TIME as EventDate,
                IS_SENSITIVE as IsSensitiveCase,
                0 as IsFrequencyCapped,
                CREATED_AT as CreatedAt
            FROM HGB_CLINICAL_EVENTS
            WHERE STATUS = 'RECEIVED'
            ORDER BY EVENT_TIME
            FETCH FIRST :Limit ROWS ONLY";

        var results = await connection.QueryAsync<ClinicalEvent>(sql, new { Limit = limit });
        return results.ToList();
    }

    public async Task SetEventStatusAsync(int clinicalEventId, string status)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            UPDATE HGB_CLINICAL_EVENTS
            SET STATUS = :Status, PROCESSED_AT = SYSTIMESTAMP
            WHERE CLINICAL_EVENT_ID = :ClinicalEventId";

        var parameters = new DynamicParameters();
        parameters.Add("Status", status);
        parameters.Add("ClinicalEventId", clinicalEventId);

        await connection.ExecuteAsync(sql, parameters);
    }

    public async Task<PatientContact?> GetPatientContactAsync(int patientId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                PATIENT_ID as PatientId,
                FULL_NAME as FullName,
                PHONE as Phone,
                PHONE_ENC as PhoneEncrypted,
                PREFERRED_LANGUAGE as PreferredLanguage,
                ALLOW_CONTACT as AllowContact
            FROM HGB_PATIENTS
            WHERE PATIENT_ID = :PatientId AND IS_DELETED = 0";

        var row = await connection.QueryFirstOrDefaultAsync<PatientContactRow>(sql, new { PatientId = patientId });
        if (row == null)
            return null;

        return new PatientContact
        {
            PatientId = row.PatientId,
            FullName = row.FullName,
            Phone = !string.IsNullOrWhiteSpace(row.PhoneEncrypted)
                ? _piiCryptoService.Decrypt(row.PhoneEncrypted)
                : row.Phone,
            PreferredLanguage = row.PreferredLanguage,
            AllowContact = row.AllowContact
        };
    }

    private class PatientContactRow : PatientContact
    {
        public string? PhoneEncrypted { get; set; }
    }

    public async Task<string?> GetInvitationPatientLanguageAsync(int invitationId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT p.PREFERRED_LANGUAGE
            FROM HGB_SURVEY_INVITATIONS i
            JOIN HGB_PATIENTS p ON i.PATIENT_ID = p.PATIENT_ID
            WHERE i.INVITATION_ID = :InvitationId AND p.IS_DELETED = 0";

        return await connection.QueryFirstOrDefaultAsync<string>(sql, new { InvitationId = invitationId });
    }

    public async Task<SurveyInvitation?> GetActiveWhatsAppInvitationByPhoneHashesAsync(IEnumerable<string> phoneHashes)
    {
        var hashes = phoneHashes
            .Where(hash => !string.IsNullOrWhiteSpace(hash))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (hashes.Length == 0)
            return null;

        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                i.INVITATION_ID as InvitationId,
                i.CLINICAL_EVENT_ID as ClinicalEventId,
                i.TEMPLATE_VERSION_ID as TemplateId,
                i.PATIENT_ID as PatientId,
                i.SELECTED_CHANNEL_ID as ChannelId,
                i.TOKEN_HASH as TokenHash,
                i.TOKEN_EXPIRES_AT as ExpiresAt,
                i.SENT_AT as SentAt,
                CASE WHEN i.TOKEN_USED_AT IS NOT NULL OR i.INVITATION_STATUS = 'COMPLETED' THEN 1 ELSE 0 END as IsUsed,
                i.TOKEN_USED_AT as UsedAt,
                i.CREATED_AT as CreatedAt
            FROM HGB_SURVEY_INVITATIONS i
            JOIN HGB_PATIENTS p ON i.PATIENT_ID = p.PATIENT_ID
            JOIN HGB_CHANNELS c ON i.SELECTED_CHANNEL_ID = c.CHANNEL_ID
            WHERE c.CHANNEL_CODE = 'WHATSAPP'
              AND p.PHONE_HASH IN :PhoneHashes
              AND p.ALLOW_CONTACT = 1
              AND p.IS_DELETED = 0
              AND i.TOKEN_USED_AT IS NULL
              AND i.TOKEN_EXPIRES_AT > SYSTIMESTAMP
              AND i.INVITATION_STATUS IN ('CREATED', 'QUEUED', 'SENT', 'DELIVERED', 'OPENED')
            ORDER BY i.CREATED_AT DESC
            FETCH FIRST 1 ROWS ONLY";

        return await connection.QueryFirstOrDefaultAsync<SurveyInvitation>(sql, new { PhoneHashes = hashes });
    }

    public async Task<int> CountRecentInvitationsAsync(int patientId, int days)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT COUNT(*)
            FROM HGB_SURVEY_INVITATIONS
            WHERE PATIENT_ID = :PatientId
              AND INVITATION_STATUS NOT IN ('CANCELLED', 'FAILED')
              AND CREATED_AT > SYSTIMESTAMP - NUMTODSINTERVAL(:Days, 'DAY')";

        var parameters = new DynamicParameters();
        parameters.Add("PatientId", patientId);
        parameters.Add("Days", days);

        return await connection.ExecuteScalarAsync<int>(sql, parameters);
    }

    public async Task EnqueueEventAsync(int clinicalEventId, DateTime scheduledAt)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            INSERT INTO HGB_EVENT_QUEUE
            (CLINICAL_EVENT_ID, QUEUE_STATUS, SCHEDULED_AT)
            VALUES
            (:ClinicalEventId, 'WAITING', :ScheduledAt)";

        var parameters = new DynamicParameters();
        parameters.Add("ClinicalEventId", clinicalEventId);
        parameters.Add("ScheduledAt", scheduledAt);

        await connection.ExecuteAsync(sql, parameters);
    }

    public async Task<List<EventQueueItem>> GetDueQueueItemsAsync(int limit = 50)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                q.QUEUE_ID as QueueId,
                q.CLINICAL_EVENT_ID as ClinicalEventId,
                e.EVENT_TYPE as EventType,
                e.PATIENT_ID as PatientId,
                q.RETRY_COUNT as RetryCount,
                q.SCHEDULED_AT as ScheduledAt
            FROM HGB_EVENT_QUEUE q
            JOIN HGB_CLINICAL_EVENTS e ON q.CLINICAL_EVENT_ID = e.CLINICAL_EVENT_ID
            WHERE q.QUEUE_STATUS = 'WAITING' AND q.SCHEDULED_AT <= SYSTIMESTAMP
            ORDER BY q.SCHEDULED_AT
            FETCH FIRST :Limit ROWS ONLY";

        var results = await connection.QueryAsync<EventQueueItem>(sql, new { Limit = limit });
        return results.ToList();
    }

    public async Task SetQueueStatusAsync(int queueId, string status, string? errorMessage)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            UPDATE HGB_EVENT_QUEUE
            SET QUEUE_STATUS = :Status,
                LAST_ERROR_MESSAGE = :ErrorMessage,
                STARTED_AT = NVL(STARTED_AT, SYSTIMESTAMP),
                COMPLETED_AT = CASE WHEN :Status2 IN ('COMPLETED', 'FAILED', 'CANCELLED') THEN SYSTIMESTAMP ELSE NULL END
            WHERE QUEUE_ID = :QueueId";

        var parameters = new DynamicParameters();
        parameters.Add("Status", status);
        parameters.Add("ErrorMessage", errorMessage);
        parameters.Add("Status2", status);
        parameters.Add("QueueId", queueId);

        await connection.ExecuteAsync(sql, parameters);
    }

    public async Task RescheduleQueueItemAsync(int queueId, DateTime scheduledAt, string errorMessage)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            UPDATE HGB_EVENT_QUEUE
            SET QUEUE_STATUS = 'WAITING',
                SCHEDULED_AT = :ScheduledAt,
                RETRY_COUNT = RETRY_COUNT + 1,
                LAST_ERROR_MESSAGE = :ErrorMessage
            WHERE QUEUE_ID = :QueueId";

        var parameters = new DynamicParameters();
        parameters.Add("ScheduledAt", scheduledAt);
        parameters.Add("ErrorMessage", errorMessage);
        parameters.Add("QueueId", queueId);

        await connection.ExecuteAsync(sql, parameters);
    }

    public async Task<int?> GetLatestPublishedVersionIdAsync(int surveyTemplateId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT TEMPLATE_VERSION_ID
            FROM HGB_SURVEY_TEMPLATE_VERSIONS
            WHERE SURVEY_TEMPLATE_ID = :SurveyTemplateId AND STATUS = 'PUBLISHED'
            ORDER BY VERSION_NO DESC
            FETCH FIRST 1 ROWS ONLY";

        return await connection.QueryFirstOrDefaultAsync<int?>(sql, new { SurveyTemplateId = surveyTemplateId });
    }

    public async Task<int> CreateInvitationAsync(
        int clinicalEventId,
        int patientId,
        int templateVersionId,
        int? triggerRuleId,
        int channelId,
        string tokenHash,
        DateTime expiresAt)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            INSERT INTO HGB_SURVEY_INVITATIONS
            (CLINICAL_EVENT_ID, PATIENT_ID, TEMPLATE_VERSION_ID, TRIGGER_RULE_ID,
             SELECTED_CHANNEL_ID, TOKEN_HASH, TOKEN_EXPIRES_AT, INVITATION_STATUS)
            VALUES
            (:ClinicalEventId, :PatientId, :TemplateVersionId, :TriggerRuleId,
             :ChannelId, :TokenHash, :ExpiresAt, 'CREATED')
            RETURNING INVITATION_ID INTO :InvitationId";

        var parameters = new DynamicParameters();
        parameters.Add("ClinicalEventId", clinicalEventId);
        parameters.Add("PatientId", patientId);
        parameters.Add("TemplateVersionId", templateVersionId);
        parameters.Add("TriggerRuleId", triggerRuleId);
        parameters.Add("ChannelId", channelId);
        parameters.Add("TokenHash", tokenHash);
        parameters.Add("ExpiresAt", expiresAt);
        parameters.Add("InvitationId", dbType: DbType.Int32, direction: ParameterDirection.Output);

        await connection.ExecuteAsync(sql, parameters);
        return parameters.Get<int>("InvitationId");
    }

    public async Task SetInvitationStatusAsync(int invitationId, string status)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            UPDATE HGB_SURVEY_INVITATIONS
            SET INVITATION_STATUS = :Status,
                SENT_AT = CASE WHEN :Status2 = 'SENT' THEN SYSTIMESTAMP ELSE SENT_AT END
            WHERE INVITATION_ID = :InvitationId";

        var parameters = new DynamicParameters();
        parameters.Add("Status", status);
        parameters.Add("Status2", status);
        parameters.Add("InvitationId", invitationId);

        await connection.ExecuteAsync(sql, parameters);
    }

    public async Task SetInvitationChannelAsync(int invitationId, int channelId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            UPDATE HGB_SURVEY_INVITATIONS
            SET SELECTED_CHANNEL_ID = :ChannelId
            WHERE INVITATION_ID = :InvitationId";

        var parameters = new DynamicParameters();
        parameters.Add("ChannelId", channelId);
        parameters.Add("InvitationId", invitationId);

        await connection.ExecuteAsync(sql, parameters);
    }

    public async Task UpdateInvitationTokenAsync(int invitationId, string tokenHash, DateTime expiresAt)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            UPDATE HGB_SURVEY_INVITATIONS
            SET TOKEN_HASH = :TokenHash, TOKEN_EXPIRES_AT = :ExpiresAt
            WHERE INVITATION_ID = :InvitationId";

        var parameters = new DynamicParameters();
        parameters.Add("TokenHash", tokenHash);
        parameters.Add("ExpiresAt", expiresAt);
        parameters.Add("InvitationId", invitationId);

        await connection.ExecuteAsync(sql, parameters);
    }

    public async Task<int> GetNextAttemptNoAsync(int invitationId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT NVL(MAX(ATTEMPT_NO), 0) + 1
            FROM HGB_DELIVERY_ATTEMPTS
            WHERE INVITATION_ID = :InvitationId";

        return await connection.ExecuteScalarAsync<int>(sql, new { InvitationId = invitationId });
    }

    public async Task AddDeliveryAttemptAsync(
        int invitationId, int channelId, int attemptNo, string status, string? errorMessage)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            INSERT INTO HGB_DELIVERY_ATTEMPTS
            (INVITATION_ID, CHANNEL_ID, ATTEMPT_NO, DELIVERY_STATUS, SENT_AT, FAILED_AT, ERROR_MESSAGE)
            VALUES
            (:InvitationId, :ChannelId, :AttemptNo, :Status,
             CASE WHEN :Status2 = 'SENT' THEN SYSTIMESTAMP ELSE NULL END,
             CASE WHEN :Status3 = 'FAILED' THEN SYSTIMESTAMP ELSE NULL END,
             :ErrorMessage)";

        var parameters = new DynamicParameters();
        parameters.Add("InvitationId", invitationId);
        parameters.Add("ChannelId", channelId);
        parameters.Add("AttemptNo", attemptNo);
        parameters.Add("Status", status);
        parameters.Add("Status2", status);
        parameters.Add("Status3", status);
        parameters.Add("ErrorMessage", errorMessage);

        await connection.ExecuteAsync(sql, parameters);
    }

    public async Task<Channel?> GetChannelByIdAsync(int channelId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                CHANNEL_ID as ChannelId,
                CHANNEL_CODE as ChannelCode,
                CHANNEL_NAME as ChannelName,
                IS_ENABLED as IsActive,
                CREATED_AT as CreatedAt
            FROM HGB_CHANNELS
            WHERE CHANNEL_ID = :ChannelId";

        return await connection.QueryFirstOrDefaultAsync<Channel>(sql, new { ChannelId = channelId });
    }

    public async Task<Channel?> GetChannelByCodeAsync(string channelCode)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                CHANNEL_ID as ChannelId,
                CHANNEL_CODE as ChannelCode,
                CHANNEL_NAME as ChannelName,
                IS_ENABLED as IsActive,
                CREATED_AT as CreatedAt
            FROM HGB_CHANNELS
            WHERE CHANNEL_CODE = :ChannelCode";

        return await connection.QueryFirstOrDefaultAsync<Channel>(sql, new { ChannelCode = channelCode });
    }

    public async Task<string?> GetChannelTemplateBodyAsync(int channelId, string templateCode, string languageCode)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT BODY_TEMPLATE
            FROM HGB_CHANNEL_TEMPLATES
            WHERE CHANNEL_ID = :ChannelId
              AND TEMPLATE_CODE = :TemplateCode
              AND LANGUAGE_CODE = :LanguageCode
              AND IS_ACTIVE = 1
            FETCH FIRST 1 ROWS ONLY";

        var parameters = new DynamicParameters();
        parameters.Add("ChannelId", channelId);
        parameters.Add("TemplateCode", templateCode);
        parameters.Add("LanguageCode", languageCode);

        var body = await connection.QueryFirstOrDefaultAsync<string>(sql, parameters);

        if (body == null && languageCode != "tr")
        {
            var fallbackParameters = new DynamicParameters();
            fallbackParameters.Add("ChannelId", channelId);
            fallbackParameters.Add("TemplateCode", templateCode);
            fallbackParameters.Add("LanguageCode", "tr");

            body = await connection.QueryFirstOrDefaultAsync<string>(sql, fallbackParameters);
        }

        return body;
    }

    public async Task<List<EventQueueItem>> GetReminderCandidatesAsync(int limit = 50)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                i.INVITATION_ID as QueueId,
                i.CLINICAL_EVENT_ID as ClinicalEventId,
                e.EVENT_TYPE as EventType,
                i.PATIENT_ID as PatientId,
                0 as RetryCount,
                i.CREATED_AT as ScheduledAt
            FROM HGB_SURVEY_INVITATIONS i
            JOIN HGB_CLINICAL_EVENTS e ON i.CLINICAL_EVENT_ID = e.CLINICAL_EVENT_ID
            JOIN HGB_TRIGGER_RULES t ON i.TRIGGER_RULE_ID = t.TRIGGER_RULE_ID
            WHERE i.INVITATION_STATUS = 'SENT'
              AND i.TOKEN_USED_AT IS NULL
              AND i.TOKEN_EXPIRES_AT > SYSTIMESTAMP
              AND t.REMINDER_ENABLED = 1
              AND (SELECT COUNT(*) FROM HGB_DELIVERY_ATTEMPTS d
                   WHERE d.INVITATION_ID = i.INVITATION_ID AND d.DELIVERY_STATUS = 'SENT')
                  < 1 + t.REMINDER_COUNT
              AND (SELECT MAX(d.CREATED_AT) FROM HGB_DELIVERY_ATTEMPTS d
                   WHERE d.INVITATION_ID = i.INVITATION_ID)
                  < SYSTIMESTAMP - NUMTODSINTERVAL(t.REMINDER_INTERVAL_MINUTES, 'MINUTE')
            FETCH FIRST :Limit ROWS ONLY";

        var results = await connection.QueryAsync<EventQueueItem>(sql, new { Limit = limit });
        return results.ToList();
    }

    public async Task<int> ExpireOverdueInvitationsAsync()
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            UPDATE HGB_SURVEY_INVITATIONS
            SET INVITATION_STATUS = 'EXPIRED'
            WHERE INVITATION_STATUS IN ('CREATED', 'QUEUED', 'SENT', 'DELIVERED', 'OPENED')
              AND TOKEN_USED_AT IS NULL
              AND TOKEN_EXPIRES_AT < SYSTIMESTAMP";

        return await connection.ExecuteAsync(sql);
    }

    public async Task<List<InvitationSummary>> GetInvitationListAsync(
        string? status,
        int limit = 200,
        int? userId = null,
        string? roleCode = null)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                i.INVITATION_ID as InvitationId,
                COALESCE(p.FULL_NAME, N'Bilinmiyor') as PatientName,
                e.EVENT_TYPE as EventType,
                st.TEMPLATE_NAME as TemplateName,
                c.CHANNEL_NAME as ChannelName,
                i.INVITATION_STATUS as Status,
                i.CREATED_AT as CreatedAt,
                i.SENT_AT as SentAt,
                i.TOKEN_EXPIRES_AT as ExpiresAt,
                i.COMPLETED_AT as CompletedAt
            FROM HGB_SURVEY_INVITATIONS i
            JOIN HGB_CLINICAL_EVENTS e ON i.CLINICAL_EVENT_ID = e.CLINICAL_EVENT_ID
            JOIN HGB_CHANNELS c ON i.SELECTED_CHANNEL_ID = c.CHANNEL_ID
            JOIN HGB_SURVEY_TEMPLATE_VERSIONS v ON i.TEMPLATE_VERSION_ID = v.TEMPLATE_VERSION_ID
            JOIN HGB_SURVEY_TEMPLATES st ON v.SURVEY_TEMPLATE_ID = st.SURVEY_TEMPLATE_ID
            LEFT JOIN HGB_PATIENTS p ON i.PATIENT_ID = p.PATIENT_ID
            WHERE 1=1";

        var parameters = new DynamicParameters();

        if (!string.IsNullOrEmpty(status))
        {
            sql += " AND i.INVITATION_STATUS = :Status";
            parameters.Add("Status", status);
        }

        UserScopeRepository.AddOrgScope("e", hasHospitalColumn: true, parameters, userId, roleCode, ref sql);

        sql += " ORDER BY i.CREATED_AT DESC FETCH FIRST :Limit ROWS ONLY";
        parameters.Add("Limit", limit);

        var results = await connection.QueryAsync<InvitationSummary>(sql, parameters);
        return results.ToList();
    }

    public async Task<InvitationSummary?> GetInvitationSummaryAsync(
        int invitationId,
        int? userId = null,
        string? roleCode = null)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                i.INVITATION_ID as InvitationId,
                COALESCE(p.FULL_NAME, N'Bilinmiyor') as PatientName,
                e.EVENT_TYPE as EventType,
                st.TEMPLATE_NAME as TemplateName,
                c.CHANNEL_NAME as ChannelName,
                i.INVITATION_STATUS as Status,
                i.CREATED_AT as CreatedAt,
                i.SENT_AT as SentAt,
                i.TOKEN_EXPIRES_AT as ExpiresAt,
                i.COMPLETED_AT as CompletedAt
            FROM HGB_SURVEY_INVITATIONS i
            JOIN HGB_CLINICAL_EVENTS e ON i.CLINICAL_EVENT_ID = e.CLINICAL_EVENT_ID
            JOIN HGB_CHANNELS c ON i.SELECTED_CHANNEL_ID = c.CHANNEL_ID
            JOIN HGB_SURVEY_TEMPLATE_VERSIONS v ON i.TEMPLATE_VERSION_ID = v.TEMPLATE_VERSION_ID
            JOIN HGB_SURVEY_TEMPLATES st ON v.SURVEY_TEMPLATE_ID = st.SURVEY_TEMPLATE_ID
            LEFT JOIN HGB_PATIENTS p ON i.PATIENT_ID = p.PATIENT_ID
            WHERE i.INVITATION_ID = :InvitationId";

        var parameters = new DynamicParameters();
        parameters.Add("InvitationId", invitationId);
        UserScopeRepository.AddOrgScope("e", hasHospitalColumn: true, parameters, userId, roleCode, ref sql);

        return await connection.QueryFirstOrDefaultAsync<InvitationSummary>(sql, parameters);
    }

    public async Task<List<DeliveryAttempt>> GetDeliveryAttemptsAsync(int invitationId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                d.DELIVERY_ATTEMPT_ID as DeliveryAttemptId,
                d.INVITATION_ID as InvitationId,
                d.CHANNEL_ID as ChannelId,
                c.CHANNEL_NAME as ChannelName,
                d.ATTEMPT_NO as AttemptNo,
                d.DELIVERY_STATUS as DeliveryStatus,
                d.SENT_AT as SentAt,
                d.ERROR_MESSAGE as ErrorMessage,
                d.CREATED_AT as CreatedAt
            FROM HGB_DELIVERY_ATTEMPTS d
            JOIN HGB_CHANNELS c ON d.CHANNEL_ID = c.CHANNEL_ID
            WHERE d.INVITATION_ID = :InvitationId
            ORDER BY d.ATTEMPT_NO";

        var results = await connection.QueryAsync<DeliveryAttempt>(sql, new { InvitationId = invitationId });
        return results.ToList();
    }

    public async Task InsertIntegrationLogAsync(
        string systemCode, string direction, string operationName,
        string? requestPayload, bool isSuccess, string? errorMessage,
        string? responsePayload = null,
        int? httpStatusCode = null,
        string? providerMessageId = null,
        string? correlationId = null)
    {
        using var connection = _connectionFactory.CreateConnection();

        var systemId = await connection.QueryFirstOrDefaultAsync<int?>(
            "SELECT INTEGRATION_SYSTEM_ID FROM HGB_INTEGRATION_SYSTEMS WHERE SYSTEM_CODE = :SystemCode",
            new { SystemCode = systemCode });

        var sql = @"
            INSERT INTO HGB_INTEGRATION_LOGS
            (INTEGRATION_SYSTEM_ID, DIRECTION, OPERATION_NAME, REQUEST_PAYLOAD,
             RESPONSE_PAYLOAD, HTTP_STATUS_CODE, SUCCESS_FLAG, ERROR_MESSAGE,
             PROVIDER_MESSAGE_ID, CORRELATION_ID)
            VALUES
            (:SystemId, :Direction, :OperationName, :RequestPayload,
             :ResponsePayload, :HttpStatusCode, :SuccessFlag, :ErrorMessage,
             :ProviderMessageId, :CorrelationId)";

        var parameters = new DynamicParameters();
        parameters.Add("SystemId", systemId);
        parameters.Add("Direction", direction);
        parameters.Add("OperationName", operationName);
        parameters.Add("RequestPayload", requestPayload);
        parameters.Add("ResponsePayload", responsePayload);
        parameters.Add("HttpStatusCode", httpStatusCode ?? (isSuccess ? 200 : 500));
        parameters.Add("SuccessFlag", isSuccess ? 1 : 0);
        parameters.Add("ErrorMessage", errorMessage);
        parameters.Add("ProviderMessageId", providerMessageId);
        parameters.Add("CorrelationId", correlationId);

        await connection.ExecuteAsync(sql, parameters);
    }
}
