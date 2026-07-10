using Dapper;
using HastaGeriBildirim.Data;
using HastaGeriBildirim.Models.ViewModels;

namespace HastaGeriBildirim.Repositories;

public class DashboardRepository
{
    private readonly OracleConnectionFactory _connectionFactory;

    public DashboardRepository(OracleConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<DashboardViewModel> GetDashboardDataAsync(
        DateTime? startDate, DateTime? endDate,
        int? branchId, int? departmentId, int? doctorId,
        int? userId = null, string? roleCode = null)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                COUNT(*) as TotalResponses,
                AVG(OVERALL_SCORE) as AverageOverallScore,
                AVG(CSAT_SCORE) as AverageCsat,
                CASE
                    WHEN COUNT(NPS_SCORE) = 0 THEN 0
                    ELSE ROUND(
                        (
                            SUM(CASE WHEN NPS_SCORE >= 9 THEN 1 ELSE 0 END) -
                            SUM(CASE WHEN NPS_SCORE <= 6 THEN 1 ELSE 0 END)
                        ) / COUNT(NPS_SCORE) * 100, 2)
                END as NpsScore,
                CASE
                    WHEN COUNT(*) = 0 THEN 0
                    ELSE ROUND(SUM(CASE WHEN IS_NEGATIVE = 1 THEN 1 ELSE 0 END) / COUNT(*) * 100, 2)
                END as NegativePercentage
            FROM HGB_SURVEY_RESPONSES r
            WHERE r.RESPONSE_STATUS = 'SUBMITTED'";

        var parameters = new DynamicParameters();

        if (startDate.HasValue)
        {
            sql += " AND r.SUBMITTED_AT >= :StartDate";
            parameters.Add("StartDate", startDate.Value);
        }
        if (endDate.HasValue)
        {
            sql += " AND r.SUBMITTED_AT <= :EndDate";
            parameters.Add("EndDate", endDate.Value);
        }
        if (branchId.HasValue)
        {
            sql += " AND r.BRANCH_ID = :BranchId";
            parameters.Add("BranchId", branchId.Value);
        }
        if (departmentId.HasValue)
        {
            sql += " AND r.DEPARTMENT_ID = :DepartmentId";
            parameters.Add("DepartmentId", departmentId.Value);
        }
        if (doctorId.HasValue)
        {
            sql += " AND r.DOCTOR_ID = :DoctorId";
            parameters.Add("DoctorId", doctorId.Value);
        }

        UserScopeRepository.AddResponseScope("r", parameters, userId, roleCode, ref sql);

        var result = await connection.QueryFirstOrDefaultAsync<DashboardViewModel>(sql, parameters);
        return result ?? new DashboardViewModel();
    }

    public async Task<List<DepartmentSummary>> GetDepartmentSummariesAsync(
        DateTime? startDate, DateTime? endDate, int? userId = null, string? roleCode = null)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                DEPARTMENT_NAME as DepartmentName,
                SUM(TOTAL_RESPONSES) as ResponseCount,
                AVG(AVG_OVERALL_SCORE) as AverageScore,
                SUM(NEGATIVE_COUNT) as NegativeCount
            FROM HGB_V_FEEDBACK_DASHBOARD v
            WHERE 1=1";

        var parameters = new DynamicParameters();

        if (startDate.HasValue)
        {
            sql += " AND v.REPORT_DATE >= :StartDate";
            parameters.Add("StartDate", startDate.Value);
        }
        if (endDate.HasValue)
        {
            sql += " AND v.REPORT_DATE <= :EndDate";
            parameters.Add("EndDate", endDate.Value);
        }

        UserScopeRepository.AddOrgScope("v", hasHospitalColumn: false, parameters, userId, roleCode, ref sql);

        sql += " GROUP BY DEPARTMENT_NAME ORDER BY ResponseCount DESC";

        var results = await connection.QueryAsync<DepartmentSummary>(sql, parameters);
        return results.ToList();
    }

    public async Task<List<DoctorSummary>> GetDoctorSummariesAsync(
        DateTime? startDate, DateTime? endDate, int? userId = null, string? roleCode = null)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                DOCTOR_NAME as DoctorName,
                SUM(TOTAL_RESPONSES) as ResponseCount,
                AVG(AVG_OVERALL_SCORE) as AverageScore
            FROM HGB_V_FEEDBACK_DASHBOARD v
            WHERE v.DOCTOR_NAME IS NOT NULL";

        var parameters = new DynamicParameters();

        if (startDate.HasValue)
        {
            sql += " AND v.REPORT_DATE >= :StartDate";
            parameters.Add("StartDate", startDate.Value);
        }
        if (endDate.HasValue)
        {
            sql += " AND v.REPORT_DATE <= :EndDate";
            parameters.Add("EndDate", endDate.Value);
        }

        UserScopeRepository.AddOrgScope("v", hasHospitalColumn: false, parameters, userId, roleCode, ref sql);

        sql += " GROUP BY DOCTOR_NAME ORDER BY ResponseCount DESC FETCH FIRST 10 ROWS ONLY";

        var results = await connection.QueryAsync<DoctorSummary>(sql, parameters);
        return results.ToList();
    }

    public async Task<List<TrendDataPoint>> GetTrendAsync(
        DateTime? startDate, DateTime? endDate, string period, int? userId = null, string? roleCode = null)
    {
        using var connection = _connectionFactory.CreateConnection();

        var truncFormat = period switch
        {
            "WEEK" => "IW",
            "MONTH" => "MM",
            _ => "DD"
        };

        var sql = $@"
            SELECT
                TRUNC(REPORT_DATE, '{truncFormat}') as ReportDate,
                AVG(AVG_OVERALL_SCORE) as AverageScore,
                SUM(TOTAL_RESPONSES) as ResponseCount,
                AVG(NPS_VALUE) as NpsValue
            FROM HGB_V_FEEDBACK_DASHBOARD v
            WHERE 1=1";

        var parameters = new DynamicParameters();

        if (startDate.HasValue)
        {
            sql += " AND v.REPORT_DATE >= :StartDate";
            parameters.Add("StartDate", startDate.Value);
        }
        if (endDate.HasValue)
        {
            sql += " AND v.REPORT_DATE <= :EndDate";
            parameters.Add("EndDate", endDate.Value);
        }

        UserScopeRepository.AddOrgScope("v", hasHospitalColumn: false, parameters, userId, roleCode, ref sql);

        sql += $" GROUP BY TRUNC(v.REPORT_DATE, '{truncFormat}') ORDER BY 1";

        var results = await connection.QueryAsync<(DateTime ReportDate, decimal? AverageScore, int ResponseCount, decimal? NpsValue)>(sql, parameters);

        return results.Select(r => new TrendDataPoint
        {
            Date = r.ReportDate,
            AverageScore = Math.Round(r.AverageScore ?? 0, 2),
            ResponseCount = r.ResponseCount,
            NpsValue = r.NpsValue.HasValue ? Math.Round(r.NpsValue.Value, 2) : null
        }).ToList();
    }

    public async Task<decimal?> GetCesAverageAsync(DateTime? startDate, DateTime? endDate, int? userId = null, string? roleCode = null)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT ROUND(AVG(NVL(a.NUMERIC_VALUE, o.NUMERIC_VALUE)), 2)
            FROM HGB_SURVEY_ANSWERS a
            JOIN HGB_SURVEY_QUESTIONS q ON a.QUESTION_ID = q.QUESTION_ID
            JOIN HGB_SURVEY_RESPONSES r ON a.RESPONSE_ID = r.RESPONSE_ID
            LEFT JOIN HGB_SURVEY_OPTIONS o ON a.OPTION_ID = o.OPTION_ID
            WHERE q.METRIC_TYPE = 'CES'
              AND r.RESPONSE_STATUS = 'SUBMITTED'";

        var parameters = new DynamicParameters();

        if (startDate.HasValue)
        {
            sql += " AND r.SUBMITTED_AT >= :StartDate";
            parameters.Add("StartDate", startDate.Value);
        }
        if (endDate.HasValue)
        {
            sql += " AND r.SUBMITTED_AT <= :EndDate";
            parameters.Add("EndDate", endDate.Value);
        }

        UserScopeRepository.AddResponseScope("r", parameters, userId, roleCode, ref sql);

        return await connection.ExecuteScalarAsync<decimal?>(sql, parameters);
    }

    public class ResponseRateInfo
    {
        public int SentCount { get; set; }
        public int CompletedCount { get; set; }
    }

    public async Task<ResponseRateInfo> GetResponseRateAsync(DateTime? startDate, DateTime? endDate, int? userId = null, string? roleCode = null)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                SUM(CASE WHEN INVITATION_STATUS IN ('SENT', 'DELIVERED', 'OPENED', 'COMPLETED', 'EXPIRED') THEN 1 ELSE 0 END) as SentCount,
                SUM(CASE WHEN INVITATION_STATUS = 'COMPLETED' THEN 1 ELSE 0 END) as CompletedCount
            FROM HGB_SURVEY_INVITATIONS i
            JOIN HGB_CLINICAL_EVENTS e ON i.CLINICAL_EVENT_ID = e.CLINICAL_EVENT_ID
            WHERE 1=1";

        var parameters = new DynamicParameters();

        if (startDate.HasValue)
        {
            sql += " AND i.CREATED_AT >= :StartDate";
            parameters.Add("StartDate", startDate.Value);
        }
        if (endDate.HasValue)
        {
            sql += " AND i.CREATED_AT <= :EndDate";
            parameters.Add("EndDate", endDate.Value);
        }

        UserScopeRepository.AddOrgScope("e", hasHospitalColumn: true, parameters, userId, roleCode, ref sql);

        var result = await connection.QueryFirstOrDefaultAsync<ResponseRateInfo>(sql, parameters);
        return result ?? new ResponseRateInfo();
    }

    public async Task<List<SentimentSlice>> GetSentimentDistributionAsync(DateTime? startDate, DateTime? endDate, int? userId = null, string? roleCode = null)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                SENTIMENT_LABEL as Label,
                COUNT(*) as ResponseCount
            FROM HGB_SURVEY_RESPONSES r
            WHERE r.RESPONSE_STATUS = 'SUBMITTED'
              AND r.SENTIMENT_LABEL IS NOT NULL";

        var parameters = new DynamicParameters();

        if (startDate.HasValue)
        {
            sql += " AND r.SUBMITTED_AT >= :StartDate";
            parameters.Add("StartDate", startDate.Value);
        }
        if (endDate.HasValue)
        {
            sql += " AND r.SUBMITTED_AT <= :EndDate";
            parameters.Add("EndDate", endDate.Value);
        }

        UserScopeRepository.AddResponseScope("r", parameters, userId, roleCode, ref sql);

        sql += " GROUP BY r.SENTIMENT_LABEL";

        var results = await connection.QueryAsync<SentimentSlice>(sql, parameters);
        return results.ToList();
    }
}
