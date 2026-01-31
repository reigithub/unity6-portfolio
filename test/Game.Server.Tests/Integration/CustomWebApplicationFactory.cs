using System.Data;
using Dapper;
using FluentMigrator.Runner;
using Game.Server.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace Game.Server.Tests.Integration;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private SqliteConnection? _keepAliveConnection;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _keepAliveConnection = new SqliteConnection("Data Source=:memory:");
        _keepAliveConnection.Open();

        // Create schema on the shared in-memory connection
        _keepAliveConnection.Execute(
            @"CREATE TABLE IF NOT EXISTS ""Users"" (
                ""Id"" TEXT PRIMARY KEY,
                ""DisplayName"" TEXT NOT NULL,
                ""PasswordHash"" TEXT NOT NULL,
                ""Level"" INTEGER NOT NULL DEFAULT 1,
                ""CreatedAt"" TEXT NOT NULL,
                ""LastLoginAt"" TEXT NOT NULL
              );
              CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Users_DisplayName"" ON ""Users"" (""DisplayName"");

              CREATE TABLE IF NOT EXISTS ""Scores"" (
                ""Id"" INTEGER PRIMARY KEY AUTOINCREMENT,
                ""UserId"" TEXT NOT NULL,
                ""GameMode"" TEXT NOT NULL,
                ""StageId"" INTEGER NOT NULL,
                ""Score"" INTEGER NOT NULL,
                ""ClearTime"" REAL NOT NULL,
                ""WaveReached"" INTEGER NOT NULL,
                ""EnemiesDefeated"" INTEGER NOT NULL,
                ""RecordedAt"" TEXT NOT NULL,
                FOREIGN KEY (""UserId"") REFERENCES ""Users"" (""Id"") ON DELETE CASCADE
              );
              CREATE INDEX IF NOT EXISTS ""IX_Scores_GameMode_StageId_Score"" ON ""Scores"" (""GameMode"", ""StageId"", ""Score"" DESC);
              CREATE INDEX IF NOT EXISTS ""IX_Scores_UserId_GameMode_StageId"" ON ""Scores"" (""UserId"", ""GameMode"", ""StageId"");");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = "SQLite",
                ["ConnectionStrings:Default"] = "Data Source=:memory:",
            });
        });

        builder.ConfigureServices(services =>
        {
            // Replace IDbConnectionFactory with one that reuses the
            // shared in-memory connection.
            services.RemoveAll<IDbConnectionFactory>();
            services.AddSingleton<IDbConnectionFactory>(
                new TestDbConnectionFactory(_keepAliveConnection));

            // Replace IMigrationRunner with a no-op so that Program.cs
            // MigrateUp() doesn't try to connect to PostgreSQL.
            // Schema is already created above.
            var mockRunner = new Mock<IMigrationRunner>();
            services.RemoveAll<IMigrationRunner>();
            services.AddSingleton(mockRunner.Object);
        });

        builder.UseEnvironment("Development");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _keepAliveConnection?.Dispose();
        }
    }

    private sealed class NonDisposingConnection : IDbConnection
    {
        private readonly IDbConnection _inner;

        public NonDisposingConnection(IDbConnection inner) => _inner = inner;

        public string ConnectionString
        {
            get => _inner.ConnectionString;
#pragma warning disable CS8767
            set => _inner.ConnectionString = value;
#pragma warning restore CS8767
        }

        public int ConnectionTimeout => _inner.ConnectionTimeout;

        public string Database => _inner.Database;

        public ConnectionState State => _inner.State;

        public IDbTransaction BeginTransaction() => _inner.BeginTransaction();

        public IDbTransaction BeginTransaction(IsolationLevel il) => _inner.BeginTransaction(il);

        public void ChangeDatabase(string databaseName) => _inner.ChangeDatabase(databaseName);

        public void Close()
        {
        }

        public IDbCommand CreateCommand() => _inner.CreateCommand();

        public void Open()
        {
            if (_inner.State != ConnectionState.Open)
            {
                _inner.Open();
            }
        }

        public void Dispose()
        {
        }
    }

    private sealed class TestDbConnectionFactory : IDbConnectionFactory
    {
        private readonly IDbConnection _keepAlive;

        public TestDbConnectionFactory(IDbConnection keepAlive)
        {
            _keepAlive = keepAlive;
        }

        public IDbConnection CreateConnection()
        {
            return new NonDisposingConnection(_keepAlive);
        }
    }
}
