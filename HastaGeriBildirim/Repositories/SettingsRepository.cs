using Dapper;
using HastaGeriBildirim.Data;
using HastaGeriBildirim.Models.Entities;

namespace HastaGeriBildirim.Repositories;

public class SettingsRepository
{
    private readonly OracleConnectionFactory _connectionFactory;

    public SettingsRepository(OracleConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<List<AppSetting>> GetAllSettingsAsync()
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                SETTING_ID as SettingId,
                SETTING_KEY as SettingKey,
                SETTING_VALUE as SettingValue,
                DESCRIPTION as Description,
                UPDATED_AT as UpdatedAt
            FROM HGB_APP_SETTINGS
            ORDER BY SETTING_KEY";

        var results = await connection.QueryAsync<AppSetting>(sql);
        return results.ToList();
    }

    public async Task<string?> GetValueAsync(string settingKey)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT SETTING_VALUE
            FROM HGB_APP_SETTINGS
            WHERE SETTING_KEY = :SettingKey";

        return await connection.QueryFirstOrDefaultAsync<string>(sql, new { SettingKey = settingKey });
    }

    public async Task UpdateSettingAsync(string settingKey, string settingValue, int? userId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            UPDATE HGB_APP_SETTINGS
            SET SETTING_VALUE = :SettingValue,
                UPDATED_BY_USER_ID = :UserId,
                UPDATED_AT = SYSTIMESTAMP
            WHERE SETTING_KEY = :SettingKey";

        var parameters = new DynamicParameters();
        parameters.Add("SettingValue", settingValue);
        parameters.Add("UserId", userId);
        parameters.Add("SettingKey", settingKey);

        await connection.ExecuteAsync(sql, parameters);
    }

    public async Task<List<IntegrationSystem>> GetIntegrationSystemsAsync()
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                INTEGRATION_SYSTEM_ID as IntegrationSystemId,
                SYSTEM_CODE as SystemCode,
                SYSTEM_NAME as SystemName,
                BASE_URL as BaseUrl,
                IS_ENABLED as IsEnabled,
                AUTH_TYPE as AuthType,
                CREATED_AT as CreatedAt
            FROM HGB_INTEGRATION_SYSTEMS
            ORDER BY SYSTEM_NAME";

        var results = await connection.QueryAsync<IntegrationSystem>(sql);
        return results.ToList();
    }

    public async Task UpdateIntegrationSystemAsync(int integrationSystemId, string? baseUrl, bool isEnabled)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            UPDATE HGB_INTEGRATION_SYSTEMS
            SET BASE_URL = :BaseUrl, IS_ENABLED = :IsEnabled
            WHERE INTEGRATION_SYSTEM_ID = :IntegrationSystemId";

        var parameters = new DynamicParameters();
        parameters.Add("BaseUrl", baseUrl);
        parameters.Add("IsEnabled", isEnabled ? 1 : 0);
        parameters.Add("IntegrationSystemId", integrationSystemId);

        await connection.ExecuteAsync(sql, parameters);
    }
}
