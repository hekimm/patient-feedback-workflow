using Dapper;
using HastaGeriBildirim.Data;

namespace HastaGeriBildirim.Repositories;

public class UserScopeRepository
{
    private readonly OracleConnectionFactory _connectionFactory;

    public UserScopeRepository(OracleConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public static void AddResponseScope(
        string alias,
        DynamicParameters parameters,
        int? userId,
        string? roleCode,
        ref string sql)
    {
        if (!userId.HasValue)
            return;

        AddOrgScope(alias, hasHospitalColumn: true, parameters, userId, roleCode, ref sql);
    }

    public static void AddOrgScope(
        string alias,
        bool hasHospitalColumn,
        DynamicParameters parameters,
        int? userId,
        string? roleCode,
        ref string sql)
    {
        if (!userId.HasValue)
            return;

        parameters.Add("ScopeUserId", userId.Value);
        parameters.Add("ScopeRoleCode", roleCode ?? string.Empty);

        var hospitalPredicate = hasHospitalColumn
            ? $"(us.SCOPE_TYPE = 'HOSPITAL' AND {alias}.HOSPITAL_ID = us.SCOPE_ID)"
            : $@"(us.SCOPE_TYPE = 'HOSPITAL' AND EXISTS (
                    SELECT 1 FROM HGB_BRANCHES sb
                    WHERE sb.BRANCH_ID = {alias}.BRANCH_ID
                      AND sb.HOSPITAL_ID = us.SCOPE_ID
                ))";

        sql += $@"
            AND (
                :ScopeRoleCode = 'SYS_ADMIN'
                OR NOT EXISTS (
                    SELECT 1 FROM HGB_USER_SCOPES us0
                    WHERE us0.USER_ID = :ScopeUserId AND us0.IS_ACTIVE = 1
                )
                OR EXISTS (
                    SELECT 1 FROM HGB_USER_SCOPES us
                    WHERE us.USER_ID = :ScopeUserId
                      AND us.IS_ACTIVE = 1
                      AND (
                          {hospitalPredicate}
                          OR (us.SCOPE_TYPE = 'BRANCH' AND {alias}.BRANCH_ID = us.SCOPE_ID)
                          OR (us.SCOPE_TYPE = 'DEPARTMENT' AND {alias}.DEPARTMENT_ID = us.SCOPE_ID)
                      )
                )
            )";
    }

    public async Task<bool> CanAccessResponseAsync(int responseId, int userId, string? roleCode)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = "SELECT COUNT(*) FROM HGB_SURVEY_RESPONSES r WHERE r.RESPONSE_ID = :ResponseId";
        var parameters = new DynamicParameters();
        parameters.Add("ResponseId", responseId);
        AddResponseScope("r", parameters, userId, roleCode, ref sql);
        return await connection.ExecuteScalarAsync<int>(sql, parameters) > 0;
    }
}
