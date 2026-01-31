using System.Data;
using Dapper;
using Game.Server.Configuration;
using Game.Server.Data;
using Game.Server.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace Game.Server.Tests.Fixtures;

public static class TestDataFixture
{
    public static readonly JwtSettings TestJwtSettings = new()
    {
        Secret = "test-secret-key-must-be-at-least-32-characters-long!",
        Issuer = "Game.Server",
        Audience = "Game.Client",
        ExpirationMinutes = 60,
        RefreshExpirationDays = 30,
    };

    public static IOptions<JwtSettings> GetJwtOptions()
    {
        return Options.Create(TestJwtSettings);
    }

    public static SqliteConnection CreateSqliteConnection()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        CreateSchema(connection);
        return connection;
    }

    public static IDbConnectionFactory CreateConnectionFactory(SqliteConnection keepAlive)
    {
        return new TestDbConnectionFactory(keepAlive);
    }

    public static async Task<SqliteConnection> CreateSeededConnectionAsync()
    {
        var connection = CreateSqliteConnection();

        var users = new[]
        {
            new UserEntity
            {
                Id = "user-1",
                DisplayName = "Player1",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password1!"),
                Level = 5,
            },
            new UserEntity
            {
                Id = "user-2",
                DisplayName = "Player2",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password2!"),
                Level = 3,
            },
            new UserEntity
            {
                Id = "user-3",
                DisplayName = "Player3",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password3!"),
                Level = 1,
            },
        };

        foreach (var user in users)
        {
            await connection.ExecuteAsync(
                @"INSERT INTO ""Users"" (""Id"", ""DisplayName"", ""PasswordHash"", ""Level"", ""CreatedAt"", ""LastLoginAt"")
                  VALUES (@Id, @DisplayName, @PasswordHash, @Level, @CreatedAt, @LastLoginAt)",
                user);
        }

        var scores = new[]
        {
            new ScoreEntity { UserId = "user-1", GameMode = "Survivor", StageId = 1, Score = 5000, ClearTime = 120f, WaveReached = 10, EnemiesDefeated = 50 },
            new ScoreEntity { UserId = "user-2", GameMode = "Survivor", StageId = 1, Score = 8000, ClearTime = 90f, WaveReached = 15, EnemiesDefeated = 80 },
            new ScoreEntity { UserId = "user-3", GameMode = "Survivor", StageId = 1, Score = 3000, ClearTime = 60f, WaveReached = 5, EnemiesDefeated = 20 },
            new ScoreEntity { UserId = "user-1", GameMode = "ScoreTimeAttack", StageId = 1, Score = 12000, ClearTime = 45f, EnemiesDefeated = 100 },
        };

        foreach (var score in scores)
        {
            await connection.ExecuteAsync(
                @"INSERT INTO ""Scores"" (""UserId"", ""GameMode"", ""StageId"", ""Score"", ""ClearTime"", ""WaveReached"", ""EnemiesDefeated"", ""RecordedAt"")
                  VALUES (@UserId, @GameMode, @StageId, @Score, @ClearTime, @WaveReached, @EnemiesDefeated, @RecordedAt)",
                score);
        }

        return connection;
    }

    private static void CreateSchema(IDbConnection connection)
    {
        connection.Execute(
            @"CREATE TABLE ""Users"" (
                ""Id"" TEXT PRIMARY KEY,
                ""DisplayName"" TEXT NOT NULL,
                ""PasswordHash"" TEXT NOT NULL,
                ""Level"" INTEGER NOT NULL DEFAULT 1,
                ""CreatedAt"" TEXT NOT NULL,
                ""LastLoginAt"" TEXT NOT NULL
              );
              CREATE UNIQUE INDEX ""IX_Users_DisplayName"" ON ""Users"" (""DisplayName"");

              CREATE TABLE ""Scores"" (
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
              CREATE INDEX ""IX_Scores_GameMode_StageId_Score"" ON ""Scores"" (""GameMode"", ""StageId"", ""Score"" DESC);
              CREATE INDEX ""IX_Scores_UserId_GameMode_StageId"" ON ""Scores"" (""UserId"", ""GameMode"", ""StageId"");");
    }

    /// <summary>
    /// A connection wrapper that suppresses Dispose/Close so that the
    /// underlying SQLite in-memory database is not destroyed when
    /// Dapper repositories call "using var conn = ...".
    /// </summary>
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
            // no-op: keep the in-memory DB alive
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
            // no-op: the owner disposes the real connection
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
