using Dapper;
using HastaGeriBildirim.Data;
using HastaGeriBildirim.Services.Integrations;

namespace HastaGeriBildirim.Repositories;

public class MaintenanceRepository
{
    private readonly OracleConnectionFactory _connectionFactory;

    public MaintenanceRepository(OracleConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public class OverdueCase
    {
        public int CaseId { get; set; }
        public int ResponseId { get; set; }
        public int DepartmentId { get; set; }
    }

    public class BiExportQueueItem
    {
        public int BiExportId { get; set; }
        public int ResponseId { get; set; }
        public decimal? OverallScore { get; set; }
        public decimal? NpsScore { get; set; }
        public decimal? CsatScore { get; set; }
        public bool IsNegative { get; set; }
        public string? SentimentLabel { get; set; }
        public decimal? SentimentScore { get; set; }
        public int? HospitalId { get; set; }
        public int? BranchId { get; set; }
        public int? DepartmentId { get; set; }
        public int? DoctorId { get; set; }
        public int? ServiceId { get; set; }
        public DateTime? SubmittedAt { get; set; }

        public BiExportPayload ToPayload()
        {
            return new BiExportPayload(
                BiExportId,
                ResponseId,
                OverallScore,
                NpsScore,
                CsatScore,
                IsNegative,
                SentimentLabel,
                SentimentScore,
                HospitalId,
                BranchId,
                DepartmentId,
                DoctorId,
                ServiceId,
                SubmittedAt);
        }
    }

    public async Task<List<OverdueCase>> GetOverdueCasesAsync()
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                RECOVERY_CASE_ID as CaseId,
                RESPONSE_ID as ResponseId,
                DEPARTMENT_ID as DepartmentId
            FROM HGB_SERVICE_RECOVERY_CASES
            WHERE CASE_STATUS IN ('OPEN', 'IN_PROGRESS', 'WAITING_PATIENT')
              AND SLA_DUE_AT < SYSTIMESTAMP
              AND ESCALATION_LEVEL = 0";

        var results = await connection.QueryAsync<OverdueCase>(sql);
        return results.ToList();
    }

    public async Task EscalateCaseAsync(int caseId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            UPDATE HGB_SERVICE_RECOVERY_CASES
            SET CASE_STATUS = 'ESCALATED', ESCALATION_LEVEL = 1
            WHERE RECOVERY_CASE_ID = :CaseId";

        await connection.ExecuteAsync(sql, new { CaseId = caseId });
    }

    public async Task InsertEscalationAsync(int caseId, string reason)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            INSERT INTO HGB_CASE_ESCALATIONS
            (RECOVERY_CASE_ID, ESCALATION_REASON)
            VALUES
            (:CaseId, :Reason)";

        var parameters = new DynamicParameters();
        parameters.Add("CaseId", caseId);
        parameters.Add("Reason", reason);

        await connection.ExecuteAsync(sql, parameters);
    }

    public async Task InsertSystemActionAsync(int caseId, string actionType, string actionNote)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            INSERT INTO HGB_RECOVERY_ACTIONS
            (RECOVERY_CASE_ID, ACTION_TYPE, ACTION_NOTE)
            VALUES
            (:CaseId, :ActionType, :ActionNote)";

        var parameters = new DynamicParameters();
        parameters.Add("CaseId", caseId);
        parameters.Add("ActionType", actionType);
        parameters.Add("ActionNote", actionNote);

        await connection.ExecuteAsync(sql, parameters);
    }

    public class RetentionPolicy
    {
        public string DataCategory { get; set; } = string.Empty;
        public int RetentionDays { get; set; }
        public string ActionAfterRetention { get; set; } = string.Empty;
    }

    public async Task<RetentionPolicy?> GetRetentionPolicyAsync(string dataCategory)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                DATA_CATEGORY as DataCategory,
                RETENTION_DAYS as RetentionDays,
                ACTION_AFTER_RETENTION as ActionAfterRetention
            FROM HGB_RETENTION_POLICIES
            WHERE DATA_CATEGORY = :DataCategory AND IS_ACTIVE = 1";

        return await connection.QueryFirstOrDefaultAsync<RetentionPolicy>(sql, new { DataCategory = dataCategory });
    }

    public async Task<int> AnonymizeOldResponsesAsync(int retentionDays)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            UPDATE HGB_SURVEY_RESPONSES
            SET PATIENT_ID = NULL, IS_ANONYMOUS = 1
            WHERE PATIENT_ID IS NOT NULL
              AND SUBMITTED_AT < SYSTIMESTAMP - NUMTODSINTERVAL(:RetentionDays, 'DAY')";

        return await connection.ExecuteAsync(sql, new { RetentionDays = retentionDays });
    }

    public async Task<int> DeleteOldAuditLogsAsync(int retentionDays)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            DELETE FROM HGB_AUDIT_LOGS
            WHERE CREATED_AT < SYSTIMESTAMP - NUMTODSINTERVAL(:RetentionDays, 'DAY')";

        return await connection.ExecuteAsync(sql, new { RetentionDays = retentionDays });
    }

    public async Task EnqueueBiExportAsync(int responseId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            INSERT INTO HGB_BI_EXPORT_QUEUE
            (RESPONSE_ID, EXPORT_STATUS)
            VALUES
            (:ResponseId, 'WAITING')";

        await connection.ExecuteAsync(sql, new { ResponseId = responseId });
    }

    public async Task<int> MarkWaitingBiExportsAsync()
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            UPDATE HGB_BI_EXPORT_QUEUE
            SET EXPORT_STATUS = 'EXPORTED', EXPORTED_AT = SYSTIMESTAMP
            WHERE EXPORT_STATUS = 'WAITING'";

        return await connection.ExecuteAsync(sql);
    }

    public async Task<List<BiExportQueueItem>> GetWaitingBiExportsAsync(int limit = 100, int maxRetryCount = 5)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                q.BI_EXPORT_ID as BiExportId,
                q.RESPONSE_ID as ResponseId,
                r.OVERALL_SCORE as OverallScore,
                r.NPS_SCORE as NpsScore,
                r.CSAT_SCORE as CsatScore,
                r.IS_NEGATIVE as IsNegative,
                r.SENTIMENT_LABEL as SentimentLabel,
                r.SENTIMENT_SCORE as SentimentScore,
                r.HOSPITAL_ID as HospitalId,
                r.BRANCH_ID as BranchId,
                r.DEPARTMENT_ID as DepartmentId,
                r.DOCTOR_ID as DoctorId,
                r.SERVICE_ID as ServiceId,
                r.SUBMITTED_AT as SubmittedAt
            FROM HGB_BI_EXPORT_QUEUE q
            JOIN HGB_SURVEY_RESPONSES r ON q.RESPONSE_ID = r.RESPONSE_ID
            WHERE q.EXPORT_STATUS IN ('WAITING', 'FAILED')
              AND NVL(q.RETRY_COUNT, 0) < :MaxRetryCount
              AND (q.NEXT_RETRY_AT IS NULL OR q.NEXT_RETRY_AT <= SYSTIMESTAMP)
            ORDER BY q.CREATED_AT
            FETCH FIRST :Limit ROWS ONLY";

        var rows = await connection.QueryAsync<BiExportQueueItem>(
            sql,
            new { Limit = limit, MaxRetryCount = maxRetryCount });

        return rows.ToList();
    }

    public async Task MarkBiExportedAsync(int biExportId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            UPDATE HGB_BI_EXPORT_QUEUE
            SET EXPORT_STATUS = 'EXPORTED',
                EXPORTED_AT = SYSTIMESTAMP,
                NEXT_RETRY_AT = NULL,
                LAST_ERROR_MESSAGE = NULL
            WHERE BI_EXPORT_ID = :BiExportId";

        await connection.ExecuteAsync(sql, new { BiExportId = biExportId });
    }

    public async Task MarkBiExportFailedAsync(
        int biExportId,
        string? errorMessage,
        int maxRetryCount = 5,
        int retryDelayMinutes = 15)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            UPDATE HGB_BI_EXPORT_QUEUE
            SET RETRY_COUNT = NVL(RETRY_COUNT, 0) + 1,
                EXPORT_STATUS = CASE
                    WHEN NVL(RETRY_COUNT, 0) + 1 >= :MaxRetryCount THEN 'FAILED_PERMANENT'
                    ELSE 'FAILED'
                END,
                NEXT_RETRY_AT = SYSTIMESTAMP + NUMTODSINTERVAL(:RetryDelayMinutes, 'MINUTE'),
                LAST_ERROR_MESSAGE = SUBSTR(:ErrorMessage, 1, 1000)
            WHERE BI_EXPORT_ID = :BiExportId";

        await connection.ExecuteAsync(sql, new
        {
            BiExportId = biExportId,
            ErrorMessage = errorMessage,
            MaxRetryCount = maxRetryCount,
            RetryDelayMinutes = retryDelayMinutes
        });
    }
}
