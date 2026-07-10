using Dapper;
using HastaGeriBildirim.Data;
using HastaGeriBildirim.Models.Entities;

namespace HastaGeriBildirim.Repositories;

public class AuditLogRepository
{
    private readonly OracleConnectionFactory _connectionFactory;

    public AuditLogRepository(OracleConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<List<AuditLog>> GetAuditLogsAsync(
        DateTime? startDate, DateTime? endDate, 
        string? entityName, string? actionCode, int? userId)
    {
        using var connection = _connectionFactory.CreateConnection();
        
        var sql = @"
            SELECT 
                AUDIT_LOG_ID as LogId,
                ENTITY_NAME as EntityName,
                ENTITY_ID as EntityId,
                ACTION_CODE as ActionCode,
                ACTOR_USER_ID as PerformedBy,
                PATIENT_ID as PatientId,
                CHANGE_DESCRIPTION as ChangeDescription,
                IP_HASH as IpAddress,
                CREATED_AT as CreatedAt
            FROM HGB_AUDIT_LOGS
            WHERE 1=1";
        
        var parameters = new DynamicParameters();
        
        if (startDate.HasValue)
        {
            sql += " AND CREATED_AT >= :StartDate";
            parameters.Add("StartDate", startDate.Value);
        }
        if (endDate.HasValue)
        {
            sql += " AND CREATED_AT <= :EndDate";
            parameters.Add("EndDate", endDate.Value);
        }
        if (!string.IsNullOrEmpty(entityName))
        {
            sql += " AND ENTITY_NAME = :EntityName";
            parameters.Add("EntityName", entityName);
        }

        if (!string.IsNullOrEmpty(actionCode))
        {
            sql += " AND ACTION_CODE = :ActionCode";
            parameters.Add("ActionCode", actionCode);
        }
        if (userId.HasValue)
        {
            sql += " AND ACTOR_USER_ID = :UserId";
            parameters.Add("UserId", userId.Value);
        }
        
        sql += " ORDER BY CREATED_AT DESC FETCH FIRST 500 ROWS ONLY";
        
        var results = await connection.QueryAsync<AuditLog>(sql, parameters);
        return results.ToList();
    }

    public async Task AddLogAsync(AuditLog log)
    {
        using var connection = _connectionFactory.CreateConnection();
        
        var sql = @"
            INSERT INTO HGB_AUDIT_LOGS
            (ENTITY_NAME, ENTITY_ID, ACTION_CODE, ACTOR_USER_ID, PATIENT_ID,
             CHANGE_DESCRIPTION, IP_HASH, CREATED_AT)
            VALUES
            (:EntityName, :EntityId, :ActionCode, :PerformedBy, :PatientId,
             :ChangeDescription, :IpAddress, SYSDATE)";

        var parameters = new DynamicParameters();
        parameters.Add("EntityName", log.EntityName);
        parameters.Add("EntityId", log.EntityId);
        parameters.Add("ActionCode", log.ActionCode);
        parameters.Add("PerformedBy", log.PerformedBy);
        parameters.Add("PatientId", log.PatientId);
        parameters.Add("ChangeDescription", log.ChangeDescription);
        parameters.Add("IpAddress", log.IpAddress);

        await connection.ExecuteAsync(sql, parameters);
    }
}
