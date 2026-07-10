using Dapper;
using HastaGeriBildirim.Data;
using HastaGeriBildirim.Models.Entities;

namespace HastaGeriBildirim.Repositories;

public class AlertRepository
{
    private readonly OracleConnectionFactory _connectionFactory;

    public AlertRepository(OracleConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<List<Alert>> GetAlertsAsync(
        string? alertType,
        string? severity,
        string? status,
        int? userId = null,
        string? roleCode = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        
        var sql = @"
            SELECT 
                a.ALERT_ID as AlertId,
                a.ALERT_TYPE as AlertType,
                a.SEVERITY as Severity,
                a.ALERT_STATUS as Status,
                a.RESPONSE_ID as ResponseId,
                a.MESSAGE as Message,
                a.TARGET_USER_ID as AssignedTo,
                a.ACKNOWLEDGED_AT as AcknowledgedAt,
                a.CREATED_AT as CreatedAt
            FROM HGB_ALERTS a
            LEFT JOIN HGB_SURVEY_RESPONSES r ON a.RESPONSE_ID = r.RESPONSE_ID
            WHERE 1=1";

        var parameters = new DynamicParameters();
        
        if (!string.IsNullOrEmpty(alertType))
        {
            sql += " AND a.ALERT_TYPE = :AlertType";
            parameters.Add("AlertType", alertType);
        }
        if (!string.IsNullOrEmpty(severity))
        {
            sql += " AND a.SEVERITY = :Severity";
            parameters.Add("Severity", severity);
        }
        if (!string.IsNullOrEmpty(status))
        {
            sql += " AND a.ALERT_STATUS = :Status";
            parameters.Add("Status", status);
        }
        
        UserScopeRepository.AddResponseScope("r", parameters, userId, roleCode, ref sql);

        sql += " ORDER BY a.CREATED_AT DESC";
        
        var results = await connection.QueryAsync<Alert>(sql, parameters);
        return results.ToList();
    }

    public async Task<int> CreateAlertAsync(Alert alert)
    {
        using var connection = _connectionFactory.CreateConnection();
        
        var sql = @"
            INSERT INTO HGB_ALERTS
            (ALERT_TYPE, SEVERITY, ALERT_STATUS, RESPONSE_ID,
             MESSAGE, TARGET_USER_ID, CREATED_AT)
            VALUES
            (:AlertType, :Severity, :Status, :ResponseId,
             :Message, :AssignedTo, SYSDATE)
            RETURNING ALERT_ID INTO :AlertId";

        var parameters = new DynamicParameters();
        parameters.Add("AlertType", alert.AlertType);
        parameters.Add("Severity", alert.Severity);
        parameters.Add("Status", alert.Status);
        parameters.Add("ResponseId", alert.ResponseId);
        parameters.Add("Message", alert.Message);
        parameters.Add("AssignedTo", alert.AssignedTo);
        parameters.Add("AlertId", dbType: System.Data.DbType.Int32, direction: System.Data.ParameterDirection.Output);

        await connection.ExecuteAsync(sql, parameters);
        return parameters.Get<int>("AlertId");
    }

    public async Task AcknowledgeAlertAsync(int alertId)
    {
        using var connection = _connectionFactory.CreateConnection();
        
        var sql = @"
            UPDATE HGB_ALERTS 
            SET ALERT_STATUS = 'ACKNOWLEDGED', ACKNOWLEDGED_AT = SYSDATE 
            WHERE ALERT_ID = :AlertId";
        
        await connection.ExecuteAsync(sql, new { AlertId = alertId });
    }

    public async Task CloseAlertAsync(int alertId)
    {
        using var connection = _connectionFactory.CreateConnection();
        
        var sql = @"
            UPDATE HGB_ALERTS 
            SET ALERT_STATUS = 'CLOSED' 
            WHERE ALERT_ID = :AlertId";
        
        await connection.ExecuteAsync(sql, new { AlertId = alertId });
    }

    public async Task<bool> CanAccessAlertAsync(int alertId, int userId, string? roleCode)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT COUNT(*)
            FROM HGB_ALERTS a
            LEFT JOIN HGB_SURVEY_RESPONSES r ON a.RESPONSE_ID = r.RESPONSE_ID
            WHERE a.ALERT_ID = :AlertId";

        var parameters = new DynamicParameters();
        parameters.Add("AlertId", alertId);
        UserScopeRepository.AddResponseScope("r", parameters, userId, roleCode, ref sql);

        return await connection.ExecuteScalarAsync<int>(sql, parameters) > 0;
    }
}
