using Dapper;
using HastaGeriBildirim.Data;
using Oracle.ManagedDataAccess.Client;

namespace HastaGeriBildirim.Repositories;

public class WebhookReplayRepository
{
    private readonly OracleConnectionFactory _connectionFactory;

    public WebhookReplayRepository(OracleConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<bool> TryRecordAsync(string signatureHash, string sourceSystem, DateTime receivedAt)
    {
        using var connection = _connectionFactory.CreateConnection();

        try
        {
            await connection.ExecuteAsync(@"
                INSERT INTO HGB_WEBHOOK_REPLAY (SIGNATURE_HASH, SOURCE_SYSTEM, RECEIVED_AT, EXPIRES_AT)
                VALUES (:SignatureHash, :SourceSystem, :ReceivedAt, :ExpiresAt)",
                new
                {
                    SignatureHash = signatureHash,
                    SourceSystem = sourceSystem,
                    ReceivedAt = receivedAt,
                    ExpiresAt = receivedAt.AddMinutes(10)
                });

            return true;
        }
        catch (OracleException ex) when (ex.Number == 1)
        {
            return false;
        }
    }

    public async Task DeleteExpiredAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync("DELETE FROM HGB_WEBHOOK_REPLAY WHERE EXPIRES_AT < SYSTIMESTAMP");
    }
}
