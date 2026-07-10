using System.Data;
using Dapper;
using HastaGeriBildirim.Data;
using HastaGeriBildirim.Models.Entities;

namespace HastaGeriBildirim.Repositories;

public class SurveyTemplateRepository
{
    private readonly OracleConnectionFactory _connectionFactory;

    public SurveyTemplateRepository(OracleConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<List<SurveyTemplate>> GetAllTemplatesAsync()
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                SURVEY_TEMPLATE_ID as TemplateId,
                TEMPLATE_NAME as TemplateName,
                DESCRIPTION as Description,
                CASE WHEN STATUS = 'ACTIVE' THEN 1 ELSE 0 END as IsActive,
                CREATED_AT as CreatedAt
            FROM HGB_SURVEY_TEMPLATES
            ORDER BY CREATED_AT DESC";

        var results = await connection.QueryAsync<SurveyTemplate>(sql);
        return results.ToList();
    }

    public async Task<SurveyTemplate?> GetTemplateByIdAsync(int templateId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                SURVEY_TEMPLATE_ID as TemplateId,
                TEMPLATE_NAME as TemplateName,
                DESCRIPTION as Description,
                CASE WHEN STATUS = 'ACTIVE' THEN 1 ELSE 0 END as IsActive,
                CREATED_AT as CreatedAt
            FROM HGB_SURVEY_TEMPLATES
            WHERE SURVEY_TEMPLATE_ID = :TemplateId";

        return await connection.QueryFirstOrDefaultAsync<SurveyTemplate>(sql, new { TemplateId = templateId });
    }

    public async Task<int> CreateTemplateAsync(string templateName, string? description, int? userId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var templateCode = "TPL_" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();

        var sql = @"
            INSERT INTO HGB_SURVEY_TEMPLATES
            (TEMPLATE_CODE, TEMPLATE_NAME, DESCRIPTION, STATUS, CREATED_BY_USER_ID)
            VALUES
            (:TemplateCode, :TemplateName, :Description, 'ACTIVE', :UserId)
            RETURNING SURVEY_TEMPLATE_ID INTO :TemplateId";

        var parameters = new DynamicParameters();
        parameters.Add("TemplateCode", templateCode);
        parameters.Add("TemplateName", templateName);
        parameters.Add("Description", description);
        parameters.Add("UserId", userId);
        parameters.Add("TemplateId", dbType: DbType.Int32, direction: ParameterDirection.Output);

        await connection.ExecuteAsync(sql, parameters);
        var templateId = parameters.Get<int>("TemplateId");

        var versionSql = @"
            INSERT INTO HGB_SURVEY_TEMPLATE_VERSIONS
            (SURVEY_TEMPLATE_ID, VERSION_NO, VERSION_LABEL, STATUS)
            VALUES
            (:TemplateId2, 1, 'v1.0', 'DRAFT')";

        await connection.ExecuteAsync(versionSql, new { TemplateId2 = templateId });

        return templateId;
    }

    public async Task<List<SurveyTemplateVersion>> GetVersionsAsync(int templateId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                v.TEMPLATE_VERSION_ID as VersionId,
                v.SURVEY_TEMPLATE_ID as TemplateId,
                v.VERSION_NO as VersionNo,
                v.VERSION_LABEL as VersionLabel,
                v.STATUS as Status,
                v.PUBLISHED_AT as PublishedAt,
                v.CREATED_AT as CreatedAt,
                (SELECT COUNT(*) FROM HGB_SURVEY_QUESTIONS q
                 WHERE q.TEMPLATE_VERSION_ID = v.TEMPLATE_VERSION_ID) as QuestionCount
            FROM HGB_SURVEY_TEMPLATE_VERSIONS v
            WHERE v.SURVEY_TEMPLATE_ID = :TemplateId
            ORDER BY v.VERSION_NO DESC";

        var results = await connection.QueryAsync<SurveyTemplateVersion>(sql, new { TemplateId = templateId });
        return results.ToList();
    }

    public async Task<SurveyTemplateVersion?> GetVersionAsync(int versionId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                TEMPLATE_VERSION_ID as VersionId,
                SURVEY_TEMPLATE_ID as TemplateId,
                VERSION_NO as VersionNo,
                VERSION_LABEL as VersionLabel,
                STATUS as Status,
                PUBLISHED_AT as PublishedAt,
                CREATED_AT as CreatedAt,
                0 as QuestionCount
            FROM HGB_SURVEY_TEMPLATE_VERSIONS
            WHERE TEMPLATE_VERSION_ID = :VersionId";

        return await connection.QueryFirstOrDefaultAsync<SurveyTemplateVersion>(sql, new { VersionId = versionId });
    }

    public async Task<int> CreateNewVersionAsync(int templateId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var lastVersion = await connection.QueryFirstOrDefaultAsync<(int VersionId, int VersionNo)>(@"
            SELECT TEMPLATE_VERSION_ID as VersionId, VERSION_NO as VersionNo
            FROM HGB_SURVEY_TEMPLATE_VERSIONS
            WHERE SURVEY_TEMPLATE_ID = :TemplateId
            ORDER BY VERSION_NO DESC
            FETCH FIRST 1 ROWS ONLY", new { TemplateId = templateId });

        var newVersionNo = lastVersion.VersionNo + 1;

        var insertVersionSql = @"
            INSERT INTO HGB_SURVEY_TEMPLATE_VERSIONS
            (SURVEY_TEMPLATE_ID, VERSION_NO, VERSION_LABEL, STATUS)
            VALUES
            (:TemplateId, :VersionNo, :VersionLabel, 'DRAFT')
            RETURNING TEMPLATE_VERSION_ID INTO :NewVersionId";

        var versionParameters = new DynamicParameters();
        versionParameters.Add("TemplateId", templateId);
        versionParameters.Add("VersionNo", newVersionNo);
        versionParameters.Add("VersionLabel", $"v{newVersionNo}.0");
        versionParameters.Add("NewVersionId", dbType: DbType.Int32, direction: ParameterDirection.Output);

        await connection.ExecuteAsync(insertVersionSql, versionParameters);
        var newVersionId = versionParameters.Get<int>("NewVersionId");

        if (lastVersion.VersionId == 0)
            return newVersionId;

        var oldQuestions = (await connection.QueryAsync<dynamic>(@"
            SELECT QUESTION_ID, QUESTION_CODE, QUESTION_ORDER, QUESTION_TYPE, METRIC_TYPE,
                   QUESTION_TEXT_TR, QUESTION_TEXT_EN, QUESTION_TEXT_AR, HELP_TEXT,
                   IS_REQUIRED, IS_INITIAL_QUESTION, MIN_VALUE, MAX_VALUE
            FROM HGB_SURVEY_QUESTIONS
            WHERE TEMPLATE_VERSION_ID = :OldVersionId
            ORDER BY QUESTION_ORDER", new { OldVersionId = lastVersion.VersionId })).ToList();

        var questionIdMap = new Dictionary<int, int>();

        foreach (var q in oldQuestions)
        {
            var questionParameters = new DynamicParameters();
            questionParameters.Add("VersionId", newVersionId);
            questionParameters.Add("QuestionCode", (string)q.QUESTION_CODE);
            questionParameters.Add("QuestionOrder", Convert.ToInt32(q.QUESTION_ORDER));
            questionParameters.Add("QuestionType", (string)q.QUESTION_TYPE);
            questionParameters.Add("MetricType", (string?)q.METRIC_TYPE);
            questionParameters.Add("TextTr", (string)q.QUESTION_TEXT_TR);
            questionParameters.Add("TextEn", (string?)q.QUESTION_TEXT_EN);
            questionParameters.Add("TextAr", (string?)q.QUESTION_TEXT_AR);
            questionParameters.Add("HelpText", (string?)q.HELP_TEXT);
            questionParameters.Add("IsRequired", Convert.ToInt32(q.IS_REQUIRED));
            questionParameters.Add("IsInitial", Convert.ToInt32(q.IS_INITIAL_QUESTION));
            questionParameters.Add("MinValue", q.MIN_VALUE == null ? (decimal?)null : Convert.ToDecimal(q.MIN_VALUE));
            questionParameters.Add("MaxValue", q.MAX_VALUE == null ? (decimal?)null : Convert.ToDecimal(q.MAX_VALUE));
            questionParameters.Add("NewQuestionId", dbType: DbType.Int32, direction: ParameterDirection.Output);

            await connection.ExecuteAsync(@"
                INSERT INTO HGB_SURVEY_QUESTIONS
                (TEMPLATE_VERSION_ID, QUESTION_CODE, QUESTION_ORDER, QUESTION_TYPE, METRIC_TYPE,
                 QUESTION_TEXT_TR, QUESTION_TEXT_EN, QUESTION_TEXT_AR, HELP_TEXT,
                 IS_REQUIRED, IS_INITIAL_QUESTION, MIN_VALUE, MAX_VALUE)
                VALUES
                (:VersionId, :QuestionCode, :QuestionOrder, :QuestionType, :MetricType,
                 :TextTr, :TextEn, :TextAr, :HelpText,
                 :IsRequired, :IsInitial, :MinValue, :MaxValue)
                RETURNING QUESTION_ID INTO :NewQuestionId", questionParameters);

            int oldId = Convert.ToInt32(q.QUESTION_ID);
            questionIdMap[oldId] = questionParameters.Get<int>("NewQuestionId");
        }

        foreach (var (oldQuestionId, newQuestionId) in questionIdMap)
        {
            var copyOptionsParameters = new DynamicParameters();
            copyOptionsParameters.Add("NewQuestionId", newQuestionId);
            copyOptionsParameters.Add("OldQuestionId", oldQuestionId);

            await connection.ExecuteAsync(@"
                INSERT INTO HGB_SURVEY_OPTIONS
                (QUESTION_ID, OPTION_ORDER, OPTION_VALUE, OPTION_TEXT_TR, OPTION_TEXT_EN, OPTION_TEXT_AR, NUMERIC_VALUE)
                SELECT :NewQuestionId, OPTION_ORDER, OPTION_VALUE, OPTION_TEXT_TR, OPTION_TEXT_EN, OPTION_TEXT_AR, NUMERIC_VALUE
                FROM HGB_SURVEY_OPTIONS
                WHERE QUESTION_ID = :OldQuestionId", copyOptionsParameters);
        }

        var oldRules = (await connection.QueryAsync<dynamic>(@"
            SELECT b.SOURCE_QUESTION_ID, b.OPERATOR_CODE, b.COMPARE_NUMERIC_VALUE,
                   b.TARGET_QUESTION_ID, b.RULE_ORDER
            FROM HGB_BRANCHING_RULES b
            JOIN HGB_SURVEY_QUESTIONS q ON b.SOURCE_QUESTION_ID = q.QUESTION_ID
            WHERE q.TEMPLATE_VERSION_ID = :OldVersionId
              AND b.COMPARE_OPTION_ID IS NULL
              AND b.IS_ACTIVE = 1", new { OldVersionId = lastVersion.VersionId })).ToList();

        foreach (var rule in oldRules)
        {
            int oldSource = Convert.ToInt32(rule.SOURCE_QUESTION_ID);
            int oldTarget = Convert.ToInt32(rule.TARGET_QUESTION_ID);

            if (!questionIdMap.TryGetValue(oldSource, out var newSource) ||
                !questionIdMap.TryGetValue(oldTarget, out var newTarget))
                continue;

            var ruleParameters = new DynamicParameters();
            ruleParameters.Add("SourceQuestionId", newSource);
            ruleParameters.Add("OperatorCode", (string)rule.OPERATOR_CODE);
            ruleParameters.Add("CompareValue", rule.COMPARE_NUMERIC_VALUE == null ? (decimal?)null : Convert.ToDecimal(rule.COMPARE_NUMERIC_VALUE));
            ruleParameters.Add("TargetQuestionId", newTarget);
            ruleParameters.Add("RuleOrder", Convert.ToInt32(rule.RULE_ORDER));

            await connection.ExecuteAsync(@"
                INSERT INTO HGB_BRANCHING_RULES
                (SOURCE_QUESTION_ID, OPERATOR_CODE, COMPARE_NUMERIC_VALUE, TARGET_QUESTION_ID, RULE_ORDER)
                VALUES
                (:SourceQuestionId, :OperatorCode, :CompareValue, :TargetQuestionId, :RuleOrder)", ruleParameters);
        }

        return newVersionId;
    }

    public async Task PublishVersionAsync(int versionId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var archiveSql = @"
            UPDATE HGB_SURVEY_TEMPLATE_VERSIONS
            SET STATUS = 'ARCHIVED'
            WHERE STATUS = 'PUBLISHED'
              AND SURVEY_TEMPLATE_ID = (SELECT SURVEY_TEMPLATE_ID
                                        FROM HGB_SURVEY_TEMPLATE_VERSIONS
                                        WHERE TEMPLATE_VERSION_ID = :VersionId)";

        await connection.ExecuteAsync(archiveSql, new { VersionId = versionId });

        var publishSql = @"
            UPDATE HGB_SURVEY_TEMPLATE_VERSIONS
            SET STATUS = 'PUBLISHED', PUBLISHED_AT = SYSTIMESTAMP
            WHERE TEMPLATE_VERSION_ID = :VersionId";

        await connection.ExecuteAsync(publishSql, new { VersionId = versionId });
    }

    public async Task<List<SurveyQuestion>> GetQuestionsForBuilderAsync(int versionId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                QUESTION_ID as QuestionId,
                TEMPLATE_VERSION_ID as VersionId,
                QUESTION_CODE as QuestionCode,
                QUESTION_TYPE as QuestionType,
                METRIC_TYPE as MetricType,
                QUESTION_TEXT_TR as QuestionText,
                HELP_TEXT as HelpText,
                QUESTION_ORDER as SortOrder,
                IS_REQUIRED as IsRequired,
                IS_INITIAL_QUESTION as IsInitialQuestion,
                MIN_VALUE as MinValue,
                MAX_VALUE as MaxValue,
                CREATED_AT as CreatedAt
            FROM HGB_SURVEY_QUESTIONS
            WHERE TEMPLATE_VERSION_ID = :VersionId
            ORDER BY QUESTION_ORDER";

        var results = await connection.QueryAsync<SurveyQuestion>(sql, new { VersionId = versionId });
        return results.ToList();
    }

    public async Task<List<SurveyOption>> GetOptionsForBuilderAsync(int questionId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                OPTION_ID as OptionId,
                QUESTION_ID as QuestionId,
                OPTION_ORDER as OptionOrder,
                OPTION_VALUE as OptionValue,
                OPTION_TEXT_TR as OptionText,
                NUMERIC_VALUE as NumericValue
            FROM HGB_SURVEY_OPTIONS
            WHERE QUESTION_ID = :QuestionId
            ORDER BY OPTION_ORDER";

        var results = await connection.QueryAsync<SurveyOption>(sql, new { QuestionId = questionId });
        return results.ToList();
    }

    public async Task<int> CountQuestionsAsync(int versionId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = "SELECT COUNT(*) FROM HGB_SURVEY_QUESTIONS WHERE TEMPLATE_VERSION_ID = :VersionId";

        return await connection.ExecuteScalarAsync<int>(sql, new { VersionId = versionId });
    }

    public async Task<int> AddQuestionAsync(
        int versionId, string questionType, string? metricType,
        string textTr, string? textEn, string? textAr,
        bool isRequired, bool isInitialQuestion, decimal? minValue, decimal? maxValue)
    {
        using var connection = _connectionFactory.CreateConnection();

        var nextOrder = await connection.ExecuteScalarAsync<int>(@"
            SELECT NVL(MAX(QUESTION_ORDER), 0) + 1
            FROM HGB_SURVEY_QUESTIONS
            WHERE TEMPLATE_VERSION_ID = :VersionId", new { VersionId = versionId });

        var sql = @"
            INSERT INTO HGB_SURVEY_QUESTIONS
            (TEMPLATE_VERSION_ID, QUESTION_CODE, QUESTION_ORDER, QUESTION_TYPE, METRIC_TYPE,
             QUESTION_TEXT_TR, QUESTION_TEXT_EN, QUESTION_TEXT_AR,
             IS_REQUIRED, IS_INITIAL_QUESTION, MIN_VALUE, MAX_VALUE)
            VALUES
            (:VersionId, :QuestionCode, :QuestionOrder, :QuestionType, :MetricType,
             :TextTr, :TextEn, :TextAr,
             :IsRequired, :IsInitial, :MinValue, :MaxValue)
            RETURNING QUESTION_ID INTO :QuestionId";

        var parameters = new DynamicParameters();
        parameters.Add("VersionId", versionId);
        parameters.Add("QuestionCode", $"Q_{nextOrder}_{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}");
        parameters.Add("QuestionOrder", nextOrder);
        parameters.Add("QuestionType", questionType);
        parameters.Add("MetricType", metricType);
        parameters.Add("TextTr", textTr);
        parameters.Add("TextEn", textEn);
        parameters.Add("TextAr", textAr);
        parameters.Add("IsRequired", isRequired ? 1 : 0);
        parameters.Add("IsInitial", isInitialQuestion ? 1 : 0);
        parameters.Add("MinValue", minValue);
        parameters.Add("MaxValue", maxValue);
        parameters.Add("QuestionId", dbType: DbType.Int32, direction: ParameterDirection.Output);

        await connection.ExecuteAsync(sql, parameters);
        return parameters.Get<int>("QuestionId");
    }

    public async Task DeleteQuestionAsync(int questionId)
    {
        using var connection = _connectionFactory.CreateConnection();

        await connection.ExecuteAsync(
            "DELETE FROM HGB_SURVEY_QUESTIONS WHERE QUESTION_ID = :QuestionId",
            new { QuestionId = questionId });
    }

    public async Task AddOptionAsync(int questionId, string optionText, decimal? numericValue)
    {
        using var connection = _connectionFactory.CreateConnection();

        var nextOrder = await connection.ExecuteScalarAsync<int>(@"
            SELECT NVL(MAX(OPTION_ORDER), 0) + 1
            FROM HGB_SURVEY_OPTIONS
            WHERE QUESTION_ID = :QuestionId", new { QuestionId = questionId });

        var sql = @"
            INSERT INTO HGB_SURVEY_OPTIONS
            (QUESTION_ID, OPTION_ORDER, OPTION_VALUE, OPTION_TEXT_TR, NUMERIC_VALUE)
            VALUES
            (:QuestionId, :OptionOrder, :OptionValue, :OptionText, :NumericValue)";

        var parameters = new DynamicParameters();
        parameters.Add("QuestionId", questionId);
        parameters.Add("OptionOrder", nextOrder);
        parameters.Add("OptionValue", $"OPT_{nextOrder}");
        parameters.Add("OptionText", optionText);
        parameters.Add("NumericValue", numericValue);

        await connection.ExecuteAsync(sql, parameters);
    }

    public async Task DeleteOptionAsync(int optionId)
    {
        using var connection = _connectionFactory.CreateConnection();

        await connection.ExecuteAsync(
            "DELETE FROM HGB_SURVEY_OPTIONS WHERE OPTION_ID = :OptionId",
            new { OptionId = optionId });
    }

    public async Task<List<BranchingRule>> GetBranchingRulesForVersionAsync(int versionId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                b.BRANCHING_RULE_ID as BranchingRuleId,
                b.SOURCE_QUESTION_ID as SourceQuestionId,
                sq.QUESTION_CODE as SourceQuestionCode,
                b.OPERATOR_CODE as OperatorCode,
                b.COMPARE_NUMERIC_VALUE as CompareNumericValue,
                b.COMPARE_OPTION_ID as CompareOptionId,
                b.TARGET_QUESTION_ID as TargetQuestionId,
                tq.QUESTION_CODE as TargetQuestionCode,
                b.RULE_ORDER as RuleOrder,
                b.IS_ACTIVE as IsActive
            FROM HGB_BRANCHING_RULES b
            JOIN HGB_SURVEY_QUESTIONS sq ON b.SOURCE_QUESTION_ID = sq.QUESTION_ID
            JOIN HGB_SURVEY_QUESTIONS tq ON b.TARGET_QUESTION_ID = tq.QUESTION_ID
            WHERE sq.TEMPLATE_VERSION_ID = :VersionId
            ORDER BY sq.QUESTION_ORDER, b.RULE_ORDER";

        var results = await connection.QueryAsync<BranchingRule>(sql, new { VersionId = versionId });
        return results.ToList();
    }

    public async Task AddBranchingRuleAsync(
        int sourceQuestionId, string operatorCode, decimal compareValue, int targetQuestionId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var nextOrder = await connection.ExecuteScalarAsync<int>(@"
            SELECT NVL(MAX(RULE_ORDER), 0) + 1
            FROM HGB_BRANCHING_RULES
            WHERE SOURCE_QUESTION_ID = :SourceQuestionId", new { SourceQuestionId = sourceQuestionId });

        var sql = @"
            INSERT INTO HGB_BRANCHING_RULES
            (SOURCE_QUESTION_ID, OPERATOR_CODE, COMPARE_NUMERIC_VALUE, TARGET_QUESTION_ID, RULE_ORDER)
            VALUES
            (:SourceQuestionId, :OperatorCode, :CompareValue, :TargetQuestionId, :RuleOrder)";

        var parameters = new DynamicParameters();
        parameters.Add("SourceQuestionId", sourceQuestionId);
        parameters.Add("OperatorCode", operatorCode);
        parameters.Add("CompareValue", compareValue);
        parameters.Add("TargetQuestionId", targetQuestionId);
        parameters.Add("RuleOrder", nextOrder);

        await connection.ExecuteAsync(sql, parameters);
    }

    public async Task DeleteBranchingRuleAsync(int branchingRuleId)
    {
        using var connection = _connectionFactory.CreateConnection();

        await connection.ExecuteAsync(
            "DELETE FROM HGB_BRANCHING_RULES WHERE BRANCHING_RULE_ID = :BranchingRuleId",
            new { BranchingRuleId = branchingRuleId });
    }
}
