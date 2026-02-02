using System.Data;
using Dapper;
using FluentMigrator.Runner;
using Game.Server.Database.Migrations;
using Microsoft.Extensions.DependencyInjection;
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
            @"TRUNCATE ""User"".""UserScore"", ""User"".""UserInfo"" RESTART IDENTITY CASCADE");
    }

    private void RunMigrations()
    {
        var services = new ServiceCollection()
            .AddFluentMigratorCore()
            .ConfigureRunner(runner =>
            {
                runner.AddPostgres();
                runner.WithGlobalConnectionString(ConnectionString);
                runner.ScanIn(typeof(M0001_CreateMasterSchema).Assembly).For.Migrations();
            })
            .AddLogging(lb => lb.AddFluentMigratorConsole())
            .BuildServiceProvider(false);

        using var scope = services.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
        runner.MigrateUp();
    }
}
