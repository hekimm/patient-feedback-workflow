using Oracle.ManagedDataAccess.Client;
using System.Data;

namespace HastaGeriBildirim.Data;

public class OracleConnectionFactory
{
    private readonly string _connectionString;

    public OracleConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("OracleDb")
            ?? throw new InvalidOperationException("Oracle connection string not found");
    }

    public IDbConnection CreateConnection()
    {
        return new OracleConnection(_connectionString);
    }
}
