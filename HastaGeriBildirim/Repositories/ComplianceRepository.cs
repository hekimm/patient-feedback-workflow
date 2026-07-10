using System.Data;
using Dapper;
using HastaGeriBildirim.Data;
using HastaGeriBildirim.Models.Entities;

namespace HastaGeriBildirim.Repositories;

public class ComplianceRepository
{
    private readonly OracleConnectionFactory _connectionFactory;

    public ComplianceRepository(OracleConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<List<ConsentRecord>> GetConsentRecordsAsync(DateTime? startDate, DateTime? endDate)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                CONSENT_RECORD_ID as ConsentId,
                PATIENT_ID as PatientId,
                INVITATION_ID as InvitationId,
                CONSENT_TEXT_ID as ConsentTextId,
                CASE WHEN CONSENT_STATUS = 'ACCEPTED' THEN 1 ELSE 0 END as IsConsentGiven,
                ANONYMOUS_SELECTED as IsAnonymous,
                IP_HASH as IpAddress,
                GIVEN_AT as ConsentDate,
                GIVEN_AT as CreatedAt
            FROM HGB_CONSENT_RECORDS
            WHERE 1=1";

        var parameters = new DynamicParameters();

        if (startDate.HasValue)
        {
            sql += " AND GIVEN_AT >= :StartDate";
            parameters.Add("StartDate", startDate.Value);
        }
        if (endDate.HasValue)
        {
            sql += " AND GIVEN_AT <= :EndDate";
            parameters.Add("EndDate", endDate.Value);
        }

        sql += " ORDER BY GIVEN_AT DESC";

        var results = await connection.QueryAsync<ConsentRecord>(sql, parameters);
        return results.ToList();
    }

    public async Task<List<DataSubjectRequest>> GetDataSubjectRequestsAsync()
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                r.DSR_ID as DsrId,
                r.PATIENT_ID as PatientId,
                COALESCE(p.FULL_NAME, TO_NCHAR(p.PSEUDONYM_CODE)) as PatientName,
                r.REQUEST_TYPE as RequestType,
                r.REQUEST_STATUS as RequestStatus,
                r.REQUESTED_AT as RequestedAt,
                r.COMPLETED_AT as CompletedAt,
                r.REQUESTED_BY_NOTE as RequestedByNote,
                u.FULL_NAME as HandledByName,
                r.RESOLUTION_NOTE as ResolutionNote
            FROM HGB_DATA_SUBJECT_REQUESTS r
            LEFT JOIN HGB_PATIENTS p ON r.PATIENT_ID = p.PATIENT_ID
            LEFT JOIN HGB_USERS u ON r.HANDLED_BY_USER_ID = u.USER_ID
            ORDER BY r.REQUESTED_AT DESC";

        var results = await connection.QueryAsync<DataSubjectRequest>(sql);
        return results.ToList();
    }

    public async Task<DataSubjectRequest?> GetRequestAsync(int dsrId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                r.DSR_ID as DsrId,
                r.PATIENT_ID as PatientId,
                COALESCE(p.FULL_NAME, TO_NCHAR(p.PSEUDONYM_CODE)) as PatientName,
                r.REQUEST_TYPE as RequestType,
                r.REQUEST_STATUS as RequestStatus,
                r.REQUESTED_AT as RequestedAt,
                r.COMPLETED_AT as CompletedAt,
                r.REQUESTED_BY_NOTE as RequestedByNote,
                NULL as HandledByName,
                r.RESOLUTION_NOTE as ResolutionNote
            FROM HGB_DATA_SUBJECT_REQUESTS r
            LEFT JOIN HGB_PATIENTS p ON r.PATIENT_ID = p.PATIENT_ID
            WHERE r.DSR_ID = :DsrId";

        return await connection.QueryFirstOrDefaultAsync<DataSubjectRequest>(sql, new { DsrId = dsrId });
    }

    public async Task CreateRequestAsync(int patientId, string requestType, string? note)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            INSERT INTO HGB_DATA_SUBJECT_REQUESTS
            (PATIENT_ID, REQUEST_TYPE, REQUEST_STATUS, REQUESTED_BY_NOTE)
            VALUES
            (:PatientId, :RequestType, 'OPEN', :Note)";

        var parameters = new DynamicParameters();
        parameters.Add("PatientId", patientId);
        parameters.Add("RequestType", requestType);
        parameters.Add("Note", note);

        await connection.ExecuteAsync(sql, parameters);
    }

    public async Task CompleteRequestAsync(int dsrId, int handledByUserId, string status, string resolutionNote)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            UPDATE HGB_DATA_SUBJECT_REQUESTS
            SET REQUEST_STATUS = :Status,
                HANDLED_BY_USER_ID = :UserId,
                RESOLUTION_NOTE = :ResolutionNote,
                COMPLETED_AT = SYSTIMESTAMP
            WHERE DSR_ID = :DsrId";

        var parameters = new DynamicParameters();
        parameters.Add("Status", status);
        parameters.Add("UserId", handledByUserId);
        parameters.Add("ResolutionNote", resolutionNote);
        parameters.Add("DsrId", dsrId);

        await connection.ExecuteAsync(sql, parameters);
    }

    public async Task<int> AnonymizeResponsesForPatientAsync(int patientId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            UPDATE HGB_SURVEY_RESPONSES
            SET PATIENT_ID = NULL, IS_ANONYMOUS = 1
            WHERE PATIENT_ID = :PatientId";

        return await connection.ExecuteAsync(sql, new { PatientId = patientId });
    }

    public async Task ScrubPatientAsync(int patientId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            UPDATE HGB_PATIENTS
            SET FULL_NAME = N'Anonimlestirildi',
                PHONE = NULL,
                PHONE_HASH = NULL,
                EMAIL = NULL,
                EMAIL_HASH = NULL,
                EXTERNAL_PATIENT_REF = NULL,
                ALLOW_CONTACT = 0,
                IS_DELETED = 1,
                UPDATED_AT = SYSTIMESTAMP
            WHERE PATIENT_ID = :PatientId";

        await connection.ExecuteAsync(sql, new { PatientId = patientId });
    }

    public class PatientExportRow
    {
        public int ResponseId { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public string? DepartmentName { get; set; }
        public decimal? OverallScore { get; set; }
        public string? QuestionText { get; set; }
        public decimal? NumericValue { get; set; }
        public string? TextValue { get; set; }
    }

    public async Task<List<PatientExportRow>> GetPatientExportAsync(int patientId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                r.RESPONSE_ID as ResponseId,
                r.SUBMITTED_AT as SubmittedAt,
                d.DEPARTMENT_NAME as DepartmentName,
                r.OVERALL_SCORE as OverallScore,
                q.QUESTION_TEXT_TR as QuestionText,
                a.NUMERIC_VALUE as NumericValue,
                a.TEXT_VALUE as TextValue
            FROM HGB_SURVEY_RESPONSES r
            LEFT JOIN HGB_DEPARTMENTS d ON r.DEPARTMENT_ID = d.DEPARTMENT_ID
            LEFT JOIN HGB_SURVEY_ANSWERS a ON a.RESPONSE_ID = r.RESPONSE_ID
            LEFT JOIN HGB_SURVEY_QUESTIONS q ON a.QUESTION_ID = q.QUESTION_ID
            WHERE r.PATIENT_ID = :PatientId
            ORDER BY r.RESPONSE_ID";

        var results = await connection.QueryAsync<PatientExportRow>(sql, new { PatientId = patientId });
        return results.ToList();
    }
}
