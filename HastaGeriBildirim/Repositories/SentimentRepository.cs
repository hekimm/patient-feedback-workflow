using Dapper;
using HastaGeriBildirim.Data;

namespace HastaGeriBildirim.Repositories;

public class SentimentRepository
{
    private readonly OracleConnectionFactory _connectionFactory;

    public SentimentRepository(OracleConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public class FreeTextAnswer
    {
        public int AnswerId { get; set; }
        public string TextValue { get; set; } = string.Empty;
    }

    public async Task<List<FreeTextAnswer>> GetFreeTextAnswersAsync(int responseId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                a.ANSWER_ID as AnswerId,
                a.TEXT_VALUE as TextValue
            FROM HGB_SURVEY_ANSWERS a
            JOIN HGB_SURVEY_QUESTIONS q ON a.QUESTION_ID = q.QUESTION_ID
            WHERE a.RESPONSE_ID = :ResponseId
              AND q.QUESTION_TYPE = 'FREE_TEXT'
              AND a.TEXT_VALUE IS NOT NULL";

        var results = await connection.QueryAsync<FreeTextAnswer>(sql, new { ResponseId = responseId });
        return results.ToList();
    }

    public async Task InsertSentimentResultAsync(
        int responseId, int? answerId, string modelName, string label, decimal score)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            INSERT INTO HGB_SENTIMENT_RESULTS
            (RESPONSE_ID, SOURCE_ANSWER_ID, MODEL_NAME, SENTIMENT_LABEL, SENTIMENT_SCORE)
            VALUES
            (:ResponseId, :AnswerId, :ModelName, :Label, :Score)";

        var parameters = new DynamicParameters();
        parameters.Add("ResponseId", responseId);
        parameters.Add("AnswerId", answerId);
        parameters.Add("ModelName", modelName);
        parameters.Add("Label", label);
        parameters.Add("Score", score);

        await connection.ExecuteAsync(sql, parameters);
    }

    public async Task UpdateResponseSentimentAsync(int responseId, string label, decimal score)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            UPDATE HGB_SURVEY_RESPONSES
            SET SENTIMENT_LABEL = :Label, SENTIMENT_SCORE = :Score
            WHERE RESPONSE_ID = :ResponseId";

        var parameters = new DynamicParameters();
        parameters.Add("Label", label);
        parameters.Add("Score", score);
        parameters.Add("ResponseId", responseId);

        await connection.ExecuteAsync(sql, parameters);
    }

    public class ThemeCategory
    {
        public int ThemeCategoryId { get; set; }
        public string ThemeCode { get; set; } = string.Empty;
    }

    public async Task<List<ThemeCategory>> GetActiveThemesAsync()
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                THEME_CATEGORY_ID as ThemeCategoryId,
                THEME_CODE as ThemeCode
            FROM HGB_THEME_CATEGORIES
            WHERE IS_ACTIVE = 1";

        var results = await connection.QueryAsync<ThemeCategory>(sql);
        return results.ToList();
    }

    public async Task InsertThemeMatchAsync(int responseId, int themeCategoryId, decimal confidence)
    {
        using var connection = _connectionFactory.CreateConnection();

        var existsSql = @"
            SELECT COUNT(*)
            FROM HGB_RESPONSE_THEME_MATCHES
            WHERE RESPONSE_ID = :ResponseId AND THEME_CATEGORY_ID = :ThemeCategoryId";

        var existsParameters = new DynamicParameters();
        existsParameters.Add("ResponseId", responseId);
        existsParameters.Add("ThemeCategoryId", themeCategoryId);

        var exists = await connection.ExecuteScalarAsync<int>(existsSql, existsParameters);
        if (exists > 0)
            return;

        var sql = @"
            INSERT INTO HGB_RESPONSE_THEME_MATCHES
            (RESPONSE_ID, THEME_CATEGORY_ID, CONFIDENCE_SCORE)
            VALUES
            (:ResponseId, :ThemeCategoryId, :Confidence)";

        var parameters = new DynamicParameters();
        parameters.Add("ResponseId", responseId);
        parameters.Add("ThemeCategoryId", themeCategoryId);
        parameters.Add("Confidence", confidence);

        await connection.ExecuteAsync(sql, parameters);
    }

    public async Task<List<string>> GetThemesForResponseAsync(int responseId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT t.THEME_NAME
            FROM HGB_RESPONSE_THEME_MATCHES m
            JOIN HGB_THEME_CATEGORIES t ON m.THEME_CATEGORY_ID = t.THEME_CATEGORY_ID
            WHERE m.RESPONSE_ID = :ResponseId";

        var results = await connection.QueryAsync<string>(sql, new { ResponseId = responseId });
        return results.ToList();
    }
}
