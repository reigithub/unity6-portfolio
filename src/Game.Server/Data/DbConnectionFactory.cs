using System.Data;
using Microsoft.Data.Sqlite;
using Npgsql;

namespace Game.Server.Data;

public class DbConnectionFactory : IDbConnectionFactory
{
    private readonly string _provider;
    private readonly string _connectionString;

    public DbConnectionFactory(IConfiguration configuration)
    {
        _provider = configuration.GetValue<string>("Database:Provider") ?? "PostgreSQL";
        _connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured.");
    }

    public IDbConnection CreateConnection()
    {
        IDbConnection connection = _provider switch
        {
            "PostgreSQL" => new NpgsqlConnection(_connectionString),
            "SQLite" => new SqliteConnection(_connectionString),
            _ => throw new InvalidOperationException($"Unsupported database provider: {_provider}"),
        };

        connection.Open();
        return connection;
    }
}
