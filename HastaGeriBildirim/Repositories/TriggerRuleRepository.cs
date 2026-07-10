using Dapper;
using HastaGeriBildirim.Data;
using HastaGeriBildirim.Models.Entities;

namespace HastaGeriBildirim.Repositories;

public class TriggerRuleRepository
{
    private const string BaseSelect = @"
        SELECT
            t.TRIGGER_RULE_ID as TriggerRuleId,
            t.EVENT_TYPE as EventType,
            t.SURVEY_TEMPLATE_ID as SurveyTemplateId,
            st.TEMPLATE_NAME as TemplateName,
            t.PRIMARY_CHANNEL_ID as PrimaryChannelId,
            pc.CHANNEL_NAME as PrimaryChannelName,
            t.FALLBACK_CHANNEL_ID as FallbackChannelId,
            fc.CHANNEL_NAME as FallbackChannelName,
            t.IS_ENABLED as IsEnabled,
            t.DELAY_MINUTES as DelayMinutes,
            t.LOW_SCORE_THRESHOLD as LowScoreThreshold,
            t.FREQUENCY_CAP_DAYS as FrequencyCapDays,
            t.FREQUENCY_CAP_COUNT as FrequencyCapCount,
            t.REMINDER_ENABLED as ReminderEnabled,
            t.REMINDER_COUNT as ReminderCount,
            t.REMINDER_INTERVAL_MINUTES as ReminderIntervalMinutes,
            t.SERVICE_RECOVERY_SLA_HOURS as ServiceRecoverySlaHours,
            t.CREATED_AT as CreatedAt
        FROM HGB_TRIGGER_RULES t
        JOIN HGB_SURVEY_TEMPLATES st ON t.SURVEY_TEMPLATE_ID = st.SURVEY_TEMPLATE_ID
        JOIN HGB_CHANNELS pc ON t.PRIMARY_CHANNEL_ID = pc.CHANNEL_ID
        LEFT JOIN HGB_CHANNELS fc ON t.FALLBACK_CHANNEL_ID = fc.CHANNEL_ID";

    private readonly OracleConnectionFactory _connectionFactory;

    public TriggerRuleRepository(OracleConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<List<TriggerRule>> GetAllRulesAsync()
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = BaseSelect + " ORDER BY t.EVENT_TYPE";

        var results = await connection.QueryAsync<TriggerRule>(sql);
        return results.ToList();
    }

    public async Task<TriggerRule?> GetRuleByIdAsync(int triggerRuleId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = BaseSelect + " WHERE t.TRIGGER_RULE_ID = :TriggerRuleId";

        return await connection.QueryFirstOrDefaultAsync<TriggerRule>(sql, new { TriggerRuleId = triggerRuleId });
    }

    public async Task<TriggerRule?> GetEnabledRuleForEventAsync(string eventType)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = BaseSelect + @"
            WHERE t.EVENT_TYPE = :EventType AND t.IS_ENABLED = 1
            ORDER BY t.TRIGGER_RULE_ID
            FETCH FIRST 1 ROWS ONLY";

        return await connection.QueryFirstOrDefaultAsync<TriggerRule>(sql, new { EventType = eventType });
    }

    public async Task<TriggerRule?> GetRuleByInvitationAsync(int invitationId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = BaseSelect + @"
            JOIN HGB_SURVEY_INVITATIONS i ON i.TRIGGER_RULE_ID = t.TRIGGER_RULE_ID
            WHERE i.INVITATION_ID = :InvitationId";

        return await connection.QueryFirstOrDefaultAsync<TriggerRule>(sql, new { InvitationId = invitationId });
    }

    public async Task UpdateRuleAsync(TriggerRule rule)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            UPDATE HGB_TRIGGER_RULES SET
                PRIMARY_CHANNEL_ID = :PrimaryChannelId,
                FALLBACK_CHANNEL_ID = :FallbackChannelId,
                IS_ENABLED = :IsEnabled,
                DELAY_MINUTES = :DelayMinutes,
                LOW_SCORE_THRESHOLD = :LowScoreThreshold,
                FREQUENCY_CAP_DAYS = :FrequencyCapDays,
                FREQUENCY_CAP_COUNT = :FrequencyCapCount,
                REMINDER_ENABLED = :ReminderEnabled,
                REMINDER_COUNT = :ReminderCount,
                REMINDER_INTERVAL_MINUTES = :ReminderIntervalMinutes,
                SERVICE_RECOVERY_SLA_HOURS = :ServiceRecoverySlaHours,
                UPDATED_AT = SYSTIMESTAMP
            WHERE TRIGGER_RULE_ID = :TriggerRuleId";

        var parameters = new DynamicParameters();
        parameters.Add("PrimaryChannelId", rule.PrimaryChannelId);
        parameters.Add("FallbackChannelId", rule.FallbackChannelId);
        parameters.Add("IsEnabled", rule.IsEnabled ? 1 : 0);
        parameters.Add("DelayMinutes", rule.DelayMinutes);
        parameters.Add("LowScoreThreshold", rule.LowScoreThreshold);
        parameters.Add("FrequencyCapDays", rule.FrequencyCapDays);
        parameters.Add("FrequencyCapCount", rule.FrequencyCapCount);
        parameters.Add("ReminderEnabled", rule.ReminderEnabled ? 1 : 0);
        parameters.Add("ReminderCount", rule.ReminderCount);
        parameters.Add("ReminderIntervalMinutes", rule.ReminderIntervalMinutes);
        parameters.Add("ServiceRecoverySlaHours", rule.ServiceRecoverySlaHours);
        parameters.Add("TriggerRuleId", rule.TriggerRuleId);

        await connection.ExecuteAsync(sql, parameters);
    }

    public async Task SetRuleEnabledAsync(int triggerRuleId, bool isEnabled)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            UPDATE HGB_TRIGGER_RULES
            SET IS_ENABLED = :IsEnabled, UPDATED_AT = SYSTIMESTAMP
            WHERE TRIGGER_RULE_ID = :TriggerRuleId";

        var parameters = new DynamicParameters();
        parameters.Add("IsEnabled", isEnabled ? 1 : 0);
        parameters.Add("TriggerRuleId", triggerRuleId);

        await connection.ExecuteAsync(sql, parameters);
    }
}
