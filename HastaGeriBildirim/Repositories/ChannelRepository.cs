using Dapper;
using HastaGeriBildirim.Data;
using HastaGeriBildirim.Models.Entities;

namespace HastaGeriBildirim.Repositories;

public class ChannelRepository
{
    private readonly OracleConnectionFactory _connectionFactory;

    public ChannelRepository(OracleConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<List<Channel>> GetAllChannelsAsync()
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                CHANNEL_ID as ChannelId,
                CHANNEL_CODE as ChannelCode,
                CHANNEL_NAME as ChannelName,
                CASE WHEN IS_ENABLED = 1 THEN 1 ELSE 0 END as IsActive,
                CREATED_AT as CreatedAt
            FROM HGB_CHANNELS
            ORDER BY CHANNEL_NAME";

        var results = await connection.QueryAsync<Channel>(sql);
        return results.ToList();
    }

    public async Task SetChannelEnabledAsync(int channelId, bool isEnabled)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            UPDATE HGB_CHANNELS
            SET IS_ENABLED = :IsEnabled
            WHERE CHANNEL_ID = :ChannelId";

        var parameters = new DynamicParameters();
        parameters.Add("IsEnabled", isEnabled ? 1 : 0);
        parameters.Add("ChannelId", channelId);

        await connection.ExecuteAsync(sql, parameters);
    }
}
