using Dapper;
using HastaGeriBildirim.Data;
using HastaGeriBildirim.Models.ViewModels;

namespace HastaGeriBildirim.Repositories;

public class FeedbackRepository
{
    private readonly OracleConnectionFactory _connectionFactory;

    public FeedbackRepository(OracleConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<List<FeedbackSummary>> GetFeedbackListAsync(
        FeedbackFilter filter,
        int? userId = null,
        string? roleCode = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        
        var sql = @"
            SELECT 
                r.RESPONSE_ID as ResponseId,
                r.SUBMITTED_AT as CompletedAt,
                CASE WHEN r.IS_ANONYMOUS = 1 THEN N'Anonim' ELSE p.FULL_NAME END as PatientName,
                r.IS_ANONYMOUS as IsAnonymous,
                d.DEPARTMENT_NAME as DepartmentName,
                doc.FULL_NAME as DoctorName,
                r.OVERALL_SCORE as OverallScore,
                r.NPS_SCORE as NpsScore,
                r.CSAT_SCORE as CsatScore,
                r.IS_NEGATIVE as IsNegative,
                r.SENTIMENT_LABEL as SentimentLabel
            FROM HGB_SURVEY_RESPONSES r
            LEFT JOIN HGB_CLINICAL_EVENTS e ON r.CLINICAL_EVENT_ID = e.CLINICAL_EVENT_ID
            LEFT JOIN HGB_DEPARTMENTS d ON r.DEPARTMENT_ID = d.DEPARTMENT_ID
            LEFT JOIN HGB_PATIENTS p ON r.PATIENT_ID = p.PATIENT_ID
            LEFT JOIN HGB_DOCTORS doc ON r.DOCTOR_ID = doc.DOCTOR_ID
            WHERE 1=1";

        var parameters = new DynamicParameters();
        
        if (filter.StartDate.HasValue)
        {
            sql += " AND r.SUBMITTED_AT >= :StartDate";
            parameters.Add("StartDate", filter.StartDate.Value);
        }
        if (filter.EndDate.HasValue)
        {
            sql += " AND r.SUBMITTED_AT <= :EndDate";
            parameters.Add("EndDate", filter.EndDate.Value);
        }
        if (filter.DepartmentId.HasValue)
        {
            sql += " AND r.DEPARTMENT_ID = :DepartmentId";
            parameters.Add("DepartmentId", filter.DepartmentId.Value);
        }
        if (filter.DoctorId.HasValue)
        {
            sql += " AND r.DOCTOR_ID = :DoctorId";
            parameters.Add("DoctorId", filter.DoctorId.Value);
        }
        if (filter.IsNegative.HasValue)
        {
            sql += " AND r.IS_NEGATIVE = :IsNegative";
            parameters.Add("IsNegative", filter.IsNegative.Value ? 1 : 0);
        }

        UserScopeRepository.AddResponseScope("r", parameters, userId, roleCode, ref sql);
        
        sql += " ORDER BY r.SUBMITTED_AT DESC";
        
        var results = await connection.QueryAsync<FeedbackSummary>(sql, parameters);
        return results.ToList();
    }

    public async Task<FeedbackDetailViewModel?> GetFeedbackDetailAsync(
        int responseId,
        int? userId = null,
        string? roleCode = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        
        var sql = @"
            SELECT 
                r.RESPONSE_ID as ResponseId,
                r.SUBMITTED_AT as CompletedAt,
                CASE WHEN r.IS_ANONYMOUS = 1 THEN N'Anonim' ELSE p.FULL_NAME END as PatientName,
                r.IS_ANONYMOUS as IsAnonymous,
                e.EVENT_TYPE as EventType,
                d.DEPARTMENT_NAME as DepartmentName,
                doc.FULL_NAME as DoctorName,
                r.OVERALL_SCORE as OverallScore,
                r.NPS_SCORE as NpsScore,
                r.CSAT_SCORE as CsatScore,
                r.IS_NEGATIVE as IsNegative,
                r.SENTIMENT_LABEL as SentimentLabel,
                r.SENTIMENT_SCORE as SentimentScore
            FROM HGB_SURVEY_RESPONSES r
            LEFT JOIN HGB_CLINICAL_EVENTS e ON r.CLINICAL_EVENT_ID = e.CLINICAL_EVENT_ID
            LEFT JOIN HGB_DEPARTMENTS d ON r.DEPARTMENT_ID = d.DEPARTMENT_ID
            LEFT JOIN HGB_PATIENTS p ON r.PATIENT_ID = p.PATIENT_ID
            LEFT JOIN HGB_DOCTORS doc ON r.DOCTOR_ID = doc.DOCTOR_ID
            WHERE r.RESPONSE_ID = :ResponseId";

        var parameters = new DynamicParameters();
        parameters.Add("ResponseId", responseId);
        UserScopeRepository.AddResponseScope("r", parameters, userId, roleCode, ref sql);

        return await connection.QueryFirstOrDefaultAsync<FeedbackDetailViewModel>(sql, parameters);
    }

    public async Task<List<AnswerDetail>> GetAnswersAsync(int responseId)
    {
        using var connection = _connectionFactory.CreateConnection();
        
        var sql = @"
            SELECT
                q.QUESTION_TEXT_TR as QuestionText,
                q.QUESTION_TYPE as QuestionType,
                a.NUMERIC_VALUE as NumericValue,
                a.TEXT_VALUE as TextValue,
                o.OPTION_TEXT_TR as OptionText
            FROM HGB_SURVEY_ANSWERS a
            JOIN HGB_SURVEY_QUESTIONS q ON a.QUESTION_ID = q.QUESTION_ID
            LEFT JOIN HGB_SURVEY_OPTIONS o ON a.OPTION_ID = o.OPTION_ID
            WHERE a.RESPONSE_ID = :ResponseId
            ORDER BY q.QUESTION_ORDER";
        
        var results = await connection.QueryAsync<AnswerDetail>(sql, new { ResponseId = responseId });
        return results.ToList();
    }

    public async Task<int?> GetRelatedCaseIdAsync(int responseId)
    {
        using var connection = _connectionFactory.CreateConnection();
        
        var sql = @"
            SELECT RECOVERY_CASE_ID
            FROM HGB_SERVICE_RECOVERY_CASES
            WHERE RESPONSE_ID = :ResponseId
            FETCH FIRST 1 ROWS ONLY";
        
        return await connection.QueryFirstOrDefaultAsync<int?>(sql, new { ResponseId = responseId });
    }
}
