using Dapper;
using Game.Server.Database;
using Game.Server.Database.Migrations;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Game.Server.Tests.Fixtures;

public class PostgresContainerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:17")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        RunMigrations();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    public NpgsqlConnection CreateConnection()
    {
        var connection = new NpgsqlConnection(ConnectionString);
        connection.Open();
        return connection;
    }

    public async Task ResetUserDataAsync()
    {
        using var connection = CreateConnection();
        await connection.ExecuteAsync(
            @"TRUNCATE ""User"".""UserExternalIdentity"", ""User"".""UserScore"", ""User"".""UserInfo"" RESTART IDENTITY CASCADE");
    }

    private void RunMigrations()
    {
        foreach (var schema in MigrationSchema.All)
        {
            MigrationRunnerFactory.MigrateUp(ConnectionString, schema);
        }
    }
}
