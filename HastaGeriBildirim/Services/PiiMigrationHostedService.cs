using Dapper;
using HastaGeriBildirim.Data;

namespace HastaGeriBildirim.Services;

public class PiiMigrationHostedService : IHostedService
{
    private readonly OracleConnectionFactory _connectionFactory;
    private readonly IPiiCryptoService _piiCryptoService;
    private readonly ILogger<PiiMigrationHostedService> _logger;

    public PiiMigrationHostedService(
        OracleConnectionFactory connectionFactory,
        IPiiCryptoService piiCryptoService,
        ILogger<PiiMigrationHostedService> logger)
    {
        _connectionFactory = connectionFactory;
        _piiCryptoService = piiCryptoService;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            var rows = (await connection.QueryAsync<PatientPiiRow>(@"
                SELECT PATIENT_ID PatientId, PHONE Phone, EMAIL Email
                FROM HGB_PATIENTS
                WHERE IS_DELETED = 0
                  AND ((PHONE IS NOT NULL AND PHONE_ENC IS NULL)
                       OR (EMAIL IS NOT NULL AND EMAIL_ENC IS NULL))")).ToList();

            foreach (var row in rows)
            {
                var phoneEnc = _piiCryptoService.Encrypt(row.Phone);
                var emailEnc = _piiCryptoService.Encrypt(row.Email);

                await connection.ExecuteAsync(@"
                    UPDATE HGB_PATIENTS
                    SET PHONE_ENC = CASE WHEN PHONE_ENC IS NULL AND :PhoneEnc IS NOT NULL THEN TO_CLOB(:PhoneEnc) ELSE PHONE_ENC END,
                        PHONE_HASH = COALESCE(PHONE_HASH, :PhoneHash),
                        EMAIL_ENC = CASE WHEN EMAIL_ENC IS NULL AND :EmailEnc IS NOT NULL THEN TO_CLOB(:EmailEnc) ELSE EMAIL_ENC END,
                        EMAIL_HASH = COALESCE(EMAIL_HASH, :EmailHash),
                        PHONE = CASE WHEN :HasPhone = 1 THEN NULL ELSE PHONE END,
                        EMAIL = CASE WHEN :HasEmail = 1 THEN NULL ELSE EMAIL END,
                        UPDATED_AT = SYSTIMESTAMP
                    WHERE PATIENT_ID = :PatientId",
                    new
                    {
                        row.PatientId,
                        PhoneEnc = phoneEnc,
                        PhoneHash = _piiCryptoService.HashForLookup(row.Phone),
                        EmailEnc = emailEnc,
                        EmailHash = _piiCryptoService.HashForLookup(row.Email),
                        HasPhone = string.IsNullOrWhiteSpace(phoneEnc) ? 0 : 1,
                        HasEmail = string.IsNullOrWhiteSpace(emailEnc) ? 0 : 1
                    });
            }

            if (rows.Count > 0)
                _logger.LogInformation("PII migration encrypted {Count} patient rows", rows.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PII migration skipped");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private sealed class PatientPiiRow
    {
        public int PatientId { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
    }
}
