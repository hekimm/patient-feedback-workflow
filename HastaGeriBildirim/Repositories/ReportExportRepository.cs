using System.Data;
using Dapper;
using HastaGeriBildirim.Data;

namespace HastaGeriBildirim.Repositories;

public class ReportExportRepository
{
    private readonly OracleConnectionFactory _connectionFactory;

    public ReportExportRepository(OracleConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<int> CreateExportAsync(int userId, string reportType, string exportFormat, string? filterJson)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            INSERT INTO HGB_REPORT_EXPORTS
            (REQUESTED_BY_USER_ID, REPORT_TYPE, EXPORT_FORMAT, FILTER_JSON, EXPORT_STATUS)
            VALUES
            (:UserId, :ReportType, :ExportFormat, :FilterJson, 'PROCESSING')
            RETURNING REPORT_EXPORT_ID INTO :ExportId";

        var parameters = new DynamicParameters();
        parameters.Add("UserId", userId);
        parameters.Add("ReportType", reportType);
        parameters.Add("ExportFormat", exportFormat);
        parameters.Add("FilterJson", filterJson);
        parameters.Add("ExportId", dbType: DbType.Int32, direction: ParameterDirection.Output);

        await connection.ExecuteAsync(sql, parameters);
        return parameters.Get<int>("ExportId");
    }

    public async Task MarkCompletedAsync(int reportExportId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            UPDATE HGB_REPORT_EXPORTS
            SET EXPORT_STATUS = 'COMPLETED', COMPLETED_AT = SYSTIMESTAMP
            WHERE REPORT_EXPORT_ID = :ReportExportId";

        await connection.ExecuteAsync(sql, new { ReportExportId = reportExportId });
    }

    public async Task MarkFailedAsync(int reportExportId, string errorMessage)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            UPDATE HGB_REPORT_EXPORTS
            SET EXPORT_STATUS = 'FAILED', ERROR_MESSAGE = :ErrorMessage
            WHERE REPORT_EXPORT_ID = :ReportExportId";

        var parameters = new DynamicParameters();
        parameters.Add("ErrorMessage", errorMessage);
        parameters.Add("ReportExportId", reportExportId);

        await connection.ExecuteAsync(sql, parameters);
    }
}
