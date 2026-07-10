using Dapper;
using HastaGeriBildirim.Data;
using HastaGeriBildirim.Models.Entities;
using HastaGeriBildirim.Models.ViewModels;

namespace HastaGeriBildirim.Repositories;

public class ServiceRecoveryRepository
{
    private readonly OracleConnectionFactory _connectionFactory;

    public ServiceRecoveryRepository(OracleConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<List<ServiceRecoverySummary>> GetOpenCasesAsync(int? userId = null, string? roleCode = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        
        var sql = @"
            SELECT 
                v.RECOVERY_CASE_ID as CaseId,
                v.CASE_STATUS as Status,
                v.PRIORITY as Priority,
                CASE WHEN r.IS_ANONYMOUS = 1 THEN N'Anonim' ELSE p.FULL_NAME END as PatientName,
                r.IS_ANONYMOUS as IsAnonymous,
                v.DEPARTMENT_NAME as DepartmentName,
                v.OVERALL_SCORE as OverallScore,
                v.ASSIGNED_USER_NAME as AssignedToName,
                v.OPENED_AT as CreatedAt,
                v.SLA_DUE_AT as DueDate,
                ROUND(EXTRACT(DAY FROM (v.SLA_DUE_AT - SYSTIMESTAMP)) * 24 + EXTRACT(HOUR FROM (v.SLA_DUE_AT - SYSTIMESTAMP))) as HoursRemaining,
                CASE WHEN v.SLA_DUE_AT < SYSTIMESTAMP + INTERVAL '4' HOUR AND v.SLA_DUE_AT > SYSTIMESTAMP THEN 1 ELSE 0 END as IsSlaWarning,
                v.IS_SLA_BREACHED as IsSlaBreached
            FROM HGB_V_OPEN_RECOVERY_CASES v
            LEFT JOIN HGB_SURVEY_RESPONSES r ON r.RESPONSE_ID = v.RESPONSE_ID
            LEFT JOIN HGB_PATIENTS p ON p.PATIENT_ID = r.PATIENT_ID
            WHERE 1=1";

        var parameters = new DynamicParameters();
        UserScopeRepository.AddResponseScope("r", parameters, userId, roleCode, ref sql);
        sql += " ORDER BY v.PRIORITY DESC, v.SLA_DUE_AT ASC";

        var results = await connection.QueryAsync<ServiceRecoverySummary>(sql, parameters);
        return results.ToList();
    }

    public async Task<ServiceRecoveryDetailViewModel?> GetCaseDetailAsync(
        int caseId,
        int? userId = null,
        string? roleCode = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        
        var sql = @"
            SELECT 
                c.RECOVERY_CASE_ID as CaseId,
                c.CASE_STATUS as Status,
                c.PRIORITY as Priority,
                CASE WHEN r.IS_ANONYMOUS = 1 THEN N'Anonim' ELSE p.FULL_NAME END as PatientName,
                r.IS_ANONYMOUS as IsAnonymous,
                e.EVENT_TYPE as EventType,
                d.DEPARTMENT_NAME as DepartmentName,
                doc.FULL_NAME as DoctorName,
                r.OVERALL_SCORE as OverallScore,
                r.SENTIMENT_LABEL as SentimentLabel,
                u.FULL_NAME as AssignedToName,
                c.OPENED_AT as CreatedAt,
                c.SLA_DUE_AT as DueDate,
                c.CLOSED_AT as ClosedAt,
                c.CLOSURE_NOTE as ClosureNote,
                CASE WHEN c.SLA_DUE_AT < SYSDATE AND c.CASE_STATUS != 'CLOSED' THEN 1 ELSE 0 END as IsSlaBreached
            FROM HGB_SERVICE_RECOVERY_CASES c
            JOIN HGB_SURVEY_RESPONSES r ON c.RESPONSE_ID = r.RESPONSE_ID
            LEFT JOIN HGB_CLINICAL_EVENTS e ON r.CLINICAL_EVENT_ID = e.CLINICAL_EVENT_ID
            LEFT JOIN HGB_DEPARTMENTS d ON c.DEPARTMENT_ID = d.DEPARTMENT_ID
            LEFT JOIN HGB_USERS u ON c.ASSIGNED_USER_ID = u.USER_ID
            LEFT JOIN HGB_PATIENTS p ON r.PATIENT_ID = p.PATIENT_ID
            LEFT JOIN HGB_DOCTORS doc ON r.DOCTOR_ID = doc.DOCTOR_ID
            WHERE c.RECOVERY_CASE_ID = :CaseId";

        var parameters = new DynamicParameters();
        parameters.Add("CaseId", caseId);
        UserScopeRepository.AddResponseScope("r", parameters, userId, roleCode, ref sql);
        
        var caseDetail = await connection.QueryFirstOrDefaultAsync<ServiceRecoveryDetailViewModel>(sql, parameters);

        if (caseDetail != null)
        {
            var commentSql = @"
                SELECT TEXT_VALUE
                FROM HGB_SURVEY_ANSWERS a
                JOIN HGB_SURVEY_QUESTIONS q ON a.QUESTION_ID = q.QUESTION_ID
                WHERE a.RESPONSE_ID = (SELECT RESPONSE_ID FROM HGB_SERVICE_RECOVERY_CASES WHERE RECOVERY_CASE_ID = :CaseId)
                AND q.QUESTION_TYPE = 'FREE_TEXT'
                AND a.TEXT_VALUE IS NOT NULL
                FETCH FIRST 1 ROWS ONLY";
            
            caseDetail.PatientComment = await connection.QueryFirstOrDefaultAsync<string>(commentSql, new { CaseId = caseId });
        }
        
        return caseDetail;
    }

    public async Task<List<RecoveryActionItem>> GetCaseActionsAsync(int caseId)
    {
        using var connection = _connectionFactory.CreateConnection();
        
        var sql = @"
            SELECT 
                a.ACTION_TYPE as ActionType,
                u.FULL_NAME as PerformedByName,
                a.ACTION_NOTE as ActionNote,
                a.ACTION_AT as CreatedAt
            FROM HGB_RECOVERY_ACTIONS a
            JOIN HGB_USERS u ON a.ACTION_BY_USER_ID = u.USER_ID
            WHERE a.RECOVERY_CASE_ID = :CaseId
            ORDER BY a.ACTION_AT DESC";
        
        var results = await connection.QueryAsync<RecoveryActionItem>(sql, new { CaseId = caseId });
        return results.ToList();
    }

    public async Task<int> CreateCaseAsync(ServiceRecoveryCase recoveryCase)
    {
        using var connection = _connectionFactory.CreateConnection();
        
        var sql = @"
            INSERT INTO HGB_SERVICE_RECOVERY_CASES
            (ALERT_ID, RESPONSE_ID, DEPARTMENT_ID, CASE_STATUS, PRIORITY,
             ASSIGNED_USER_ID, SLA_DUE_AT, OPENED_AT)
            VALUES
            (:AlertId, :ResponseId, :DepartmentId, :Status, :Priority,
             :AssignedTo, :DueDate, SYSDATE)
            RETURNING RECOVERY_CASE_ID INTO :CaseId";

        var parameters = new DynamicParameters();
        parameters.Add("AlertId", recoveryCase.AlertId);
        parameters.Add("ResponseId", recoveryCase.ResponseId);
        parameters.Add("DepartmentId", recoveryCase.DepartmentId);
        parameters.Add("Status", recoveryCase.Status);
        parameters.Add("Priority", recoveryCase.Priority);
        parameters.Add("AssignedTo", recoveryCase.AssignedTo);
        parameters.Add("DueDate", recoveryCase.DueDate);
        parameters.Add("CaseId", dbType: System.Data.DbType.Int32, direction: System.Data.ParameterDirection.Output);

        await connection.ExecuteAsync(sql, parameters);
        return parameters.Get<int>("CaseId");
    }

    public async Task AddActionAsync(RecoveryAction action)
    {
        using var connection = _connectionFactory.CreateConnection();
        
        var sql = @"
            INSERT INTO HGB_RECOVERY_ACTIONS
            (RECOVERY_CASE_ID, ACTION_TYPE, ACTION_BY_USER_ID, ACTION_NOTE, ACTION_AT)
            VALUES
            (:CaseId, :ActionType, :PerformedBy, :ActionNote, SYSDATE)";

        var parameters = new DynamicParameters();
        parameters.Add("CaseId", action.CaseId);
        parameters.Add("ActionType", action.ActionType);
        parameters.Add("PerformedBy", action.PerformedBy);
        parameters.Add("ActionNote", action.ActionNote);

        await connection.ExecuteAsync(sql, parameters);
    }

    public async Task UpdateCaseStatusAsync(int caseId, string status)
    {
        using var connection = _connectionFactory.CreateConnection();
        
        var sql = @"
            UPDATE HGB_SERVICE_RECOVERY_CASES 
            SET CASE_STATUS = :Status 
            WHERE RECOVERY_CASE_ID = :CaseId";
        
        await connection.ExecuteAsync(sql, new { CaseId = caseId, Status = status });
    }

    public async Task CloseCaseAsync(int caseId, string closureNote)
    {
        using var connection = _connectionFactory.CreateConnection();
        
        var sql = @"
            UPDATE HGB_SERVICE_RECOVERY_CASES 
            SET CASE_STATUS = 'CLOSED', CLOSED_AT = SYSDATE, CLOSURE_NOTE = :ClosureNote 
            WHERE RECOVERY_CASE_ID = :CaseId";
        
        await connection.ExecuteAsync(sql, new { CaseId = caseId, ClosureNote = closureNote });
    }

    public async Task AssignCaseAsync(int caseId, int userId)
    {
        using var connection = _connectionFactory.CreateConnection();
        
        var sql = @"
            UPDATE HGB_SERVICE_RECOVERY_CASES 
            SET ASSIGNED_USER_ID = :UserId 
            WHERE RECOVERY_CASE_ID = :CaseId";
        
        await connection.ExecuteAsync(sql, new { CaseId = caseId, UserId = userId });
    }
}
