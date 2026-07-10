using Dapper;
using HastaGeriBildirim.Data;
using HastaGeriBildirim.Models.Entities;

namespace HastaGeriBildirim.Repositories;

public class KpiRepository
{
    private readonly OracleConnectionFactory _connectionFactory;

    public KpiRepository(OracleConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<List<KpiTarget>> GetTargetsAsync()
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                k.KPI_TARGET_ID as KpiTargetId,
                k.KPI_CODE as KpiCode,
                k.DEPARTMENT_ID as DepartmentId,
                d.DEPARTMENT_NAME as DepartmentName,
                k.TARGET_PERIOD as TargetPeriod,
                k.TARGET_VALUE as TargetValue,
                k.VALID_FROM as ValidFrom,
                k.VALID_TO as ValidTo,
                k.CREATED_AT as CreatedAt
            FROM HGB_KPI_TARGETS k
            LEFT JOIN HGB_DEPARTMENTS d ON k.DEPARTMENT_ID = d.DEPARTMENT_ID
            ORDER BY k.KPI_CODE, k.VALID_FROM DESC";

        var results = await connection.QueryAsync<KpiTarget>(sql);
        return results.ToList();
    }

    public async Task<List<KpiTarget>> GetActiveGlobalTargetsAsync()
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                KPI_TARGET_ID as KpiTargetId,
                KPI_CODE as KpiCode,
                DEPARTMENT_ID as DepartmentId,
                NULL as DepartmentName,
                TARGET_PERIOD as TargetPeriod,
                TARGET_VALUE as TargetValue,
                VALID_FROM as ValidFrom,
                VALID_TO as ValidTo,
                CREATED_AT as CreatedAt
            FROM HGB_KPI_TARGETS
            WHERE DEPARTMENT_ID IS NULL
              AND VALID_FROM <= SYSDATE
              AND (VALID_TO IS NULL OR VALID_TO >= SYSDATE)
            ORDER BY KPI_CODE";

        var results = await connection.QueryAsync<KpiTarget>(sql);
        return results.ToList();
    }

    public async Task AddTargetAsync(KpiTarget target, int? userId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            INSERT INTO HGB_KPI_TARGETS
            (KPI_CODE, DEPARTMENT_ID, TARGET_PERIOD, TARGET_VALUE, VALID_FROM, VALID_TO, CREATED_BY_USER_ID)
            VALUES
            (:KpiCode, :DepartmentId, :TargetPeriod, :TargetValue, :ValidFrom, :ValidTo, :UserId)";

        var parameters = new DynamicParameters();
        parameters.Add("KpiCode", target.KpiCode);
        parameters.Add("DepartmentId", target.DepartmentId);
        parameters.Add("TargetPeriod", target.TargetPeriod);
        parameters.Add("TargetValue", target.TargetValue);
        parameters.Add("ValidFrom", target.ValidFrom);
        parameters.Add("ValidTo", target.ValidTo);
        parameters.Add("UserId", userId);

        await connection.ExecuteAsync(sql, parameters);
    }

    public async Task DeleteTargetAsync(int kpiTargetId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = "DELETE FROM HGB_KPI_TARGETS WHERE KPI_TARGET_ID = :KpiTargetId";

        await connection.ExecuteAsync(sql, new { KpiTargetId = kpiTargetId });
    }
}
