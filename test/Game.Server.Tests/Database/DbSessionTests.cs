using Dapper;
using Game.Server.Database;
using Game.Server.Tests.Fixtures;

namespace Game.Server.Tests.Database;

[Collection("Database")]
public class DbSessionTests : IAsyncLifetime
{
    private readonly PostgresContainerFixture _postgres;
    private IDbConnectionFactory _connectionFactory = null!;

    public DbSessionTests(PostgresContainerFixture postgres)
    {
        _postgres = postgres;
    }

    public async Task InitializeAsync()
    {
        await _postgres.ResetUserDataAsync();
        _connectionFactory = TestDataFixture.CreateConnectionFactory(_postgres.ConnectionString);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static readonly Guid TestUserId = Guid.Parse("00000000-0000-0000-0000-999999999999");

    private const string InsertUserSql =
        @"INSERT INTO ""User"".""UserInfo""
          (""Id"", ""UserId"", ""UserName"", ""Level"", ""AuthType"", ""RegisteredAt"", ""LastLoginAt"",
           ""FailedLoginAttempts"", ""IsEmailVerified"")
          VALUES (@Id, '999999999999', 'RollbackTest', 1, 0, NOW(), NOW(), 0, false)";

    private const string CountUserSql =
        @"SELECT COUNT(*) FROM ""User"".""UserInfo"" WHERE ""Id"" = @Id";

    [Fact]
    public void Dispose_WithUncommittedTransaction_RollsBackChanges()
    {
        // Arrange & Act: Begin transaction, insert, then Dispose without committing
        var session = TestDataFixture.CreateDbSession(_connectionFactory);
        session.BeginScope();
        session.Connection.Execute(InsertUserSql, new { Id = TestUserId }, session.Transaction);
        session.Dispose();

        // Assert: Row should NOT exist (rolled back)
        using var verifyConnection = _postgres.CreateConnection();
        var count = verifyConnection.ExecuteScalar<int>(CountUserSql, new { Id = TestUserId });
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task DisposeAsync_WithUncommittedTransaction_RollsBackChanges()
    {
        // Arrange & Act: Begin transaction, insert, then DisposeAsync without committing
        var session = TestDataFixture.CreateDbSession(_connectionFactory);
        session.BeginScope();
        await session.Connection.ExecuteAsync(InsertUserSql, new { Id = TestUserId }, session.Transaction);
        await session.DisposeAsync();

        // Assert: Row should NOT exist (rolled back)
        using var verifyConnection = _postgres.CreateConnection();
        var count = await verifyConnection.ExecuteScalarAsync<int>(CountUserSql, new { Id = TestUserId });
        Assert.Equal(0, count);
    }

    [Fact]
    public void DbTransactionScope_WithoutCommit_RollsBackOnDispose()
    {
        // Arrange & Act: Use DbTransactionScope pattern, don't call Commit
        using var session = TestDataFixture.CreateDbSession(_connectionFactory);
        using (var tx = session.BeginScope())
        {
            session.Connection.Execute(InsertUserSql, new { Id = TestUserId }, session.Transaction);
            // tx.Commit() intentionally omitted
        }

        // Assert: Row should NOT exist (rolled back by scope Dispose)
        using var verifyConnection = _postgres.CreateConnection();
        var count = verifyConnection.ExecuteScalar<int>(CountUserSql, new { Id = TestUserId });
        Assert.Equal(0, count);
    }

    [Fact]
    public void DbTransactionScope_WithCommit_PersistsChanges()
    {
        // Arrange & Act: Use DbTransactionScope pattern, call Commit
        using var session = TestDataFixture.CreateDbSession(_connectionFactory);
        using (var tx = session.BeginScope())
        {
            session.Connection.Execute(InsertUserSql, new { Id = TestUserId }, session.Transaction);
            tx.Commit();
        }

        // Assert: Row SHOULD exist (committed)
        using var verifyConnection = _postgres.CreateConnection();
        var count = verifyConnection.ExecuteScalar<int>(CountUserSql, new { Id = TestUserId });
        Assert.Equal(1, count);
    }
}
