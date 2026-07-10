using Dapper;
using HastaGeriBildirim.Data;
using HastaGeriBildirim.Models.Entities;

namespace HastaGeriBildirim.Repositories;

public class IntegrationLogRepository
{
    private readonly OracleConnectionFactory _connectionFactory;

    public IntegrationLogRepository(OracleConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<List<IntegrationLog>> GetIntegrationLogsAsync(
        int? systemId, string? direction, bool? isSuccess, int limit = 100)
    {
        using var connection = _connectionFactory.CreateConnection();
        
        var sql = @"
            SELECT 
                INTEGRATION_LOG_ID as LogId,
                INTEGRATION_SYSTEM_ID as SystemId,
                DIRECTION as Direction,
                OPERATION_NAME as Endpoint,
                REQUEST_PAYLOAD as RequestPayload,
                RESPONSE_PAYLOAD as ResponsePayload,
                HTTP_STATUS_CODE as HttpStatusCode,
                SUCCESS_FLAG as IsSuccess,
                ERROR_MESSAGE as ErrorMessage,
                CREATED_AT as CreatedAt
            FROM HGB_INTEGRATION_LOGS
            WHERE 1=1";
        
        var parameters = new DynamicParameters();
        
        if (systemId.HasValue)
        {
            sql += " AND INTEGRATION_SYSTEM_ID = :SystemId";
            parameters.Add("SystemId", systemId.Value);
        }
        if (!string.IsNullOrEmpty(direction))
        {
            sql += " AND DIRECTION = :Direction";
            parameters.Add("Direction", direction);
        }
        if (isSuccess.HasValue)
        {
            sql += " AND SUCCESS_FLAG = :IsSuccess";
            parameters.Add("IsSuccess", isSuccess.Value ? 1 : 0);
        }
        
        sql += " ORDER BY CREATED_AT DESC FETCH FIRST :Limit ROWS ONLY";
        parameters.Add("Limit", limit);
        
        var results = await connection.QueryAsync<IntegrationLog>(sql, parameters);
        return results.ToList();
    }
}
