using Dapper;
using Game.Server.Data;
using Game.Server.Tables;
using Game.Server.Repositories.Interfaces;

namespace Game.Server.Repositories.Dapper;

public class DapperRankingRepository : IRankingRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public DapperRankingRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<List<UserScore>> GetTopScoresAsync(
        string gameMode, int stageId, int limit, int offset)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql =
            @"SELECT s.""Id"", s.""UserId"", s.""GameMode"", s.""StageId"", s.""Score"",
                     s.""ClearTime"", s.""WaveReached"", s.""EnemiesDefeated"", s.""RecordedAt"",
                     u.""Id"", u.""DisplayName"", u.""PasswordHash"", u.""Level"", u.""CreatedAt"", u.""LastLoginAt""
              FROM ""Scores"" s
              INNER JOIN ""Users"" u ON s.""UserId"" = u.""Id""
              WHERE s.""GameMode"" = @GameMode AND s.""StageId"" = @StageId
              ORDER BY s.""Score"" DESC, s.""ClearTime"" ASC
              LIMIT @Limit OFFSET @Offset";

        var results = await connection.QueryAsync<UserScore, UserInfo, UserScore>(
            sql,
            (score, user) =>
            {
                score.User = user;
                return score;
            },
            new { GameMode = gameMode, StageId = stageId, Limit = limit, Offset = offset },
            splitOn: "Id");

        return results.AsList();
    }

    public async Task<UserScore?> GetUserBestScoreAsync(
        string gameMode, int stageId, string userId)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql =
            @"SELECT ""Id"", ""UserId"", ""GameMode"", ""StageId"", ""Score"",
                     ""ClearTime"", ""WaveReached"", ""EnemiesDefeated"", ""RecordedAt""
              FROM ""Scores""
              WHERE ""UserId"" = @UserId AND ""GameMode"" = @GameMode AND ""StageId"" = @StageId
              ORDER BY ""Score"" DESC, ""ClearTime"" ASC
              LIMIT 1";

        return await connection.QueryFirstOrDefaultAsync<UserScore>(
            sql,
            new { UserId = userId, GameMode = gameMode, StageId = stageId });
    }

    public async Task<int> GetUserRankAsync(
        string gameMode, int stageId, string userId)
    {
        var userBest = await GetUserBestScoreAsync(gameMode, stageId, userId);
        if (userBest == null)
        {
            return 0;
        }

        using var connection = _connectionFactory.CreateConnection();

        const string sql =
            @"SELECT COUNT(DISTINCT ""UserId"")
              FROM ""Scores""
              WHERE ""GameMode"" = @GameMode AND ""StageId"" = @StageId
                AND (""Score"" > @Score
                     OR (""Score"" = @Score AND ""ClearTime"" < @ClearTime))";

        int higherCount = await connection.ExecuteScalarAsync<int>(
            sql,
            new
            {
                GameMode = gameMode,
                StageId = stageId,
                Score = userBest.Score,
                ClearTime = userBest.ClearTime,
            });

        return higherCount + 1;
    }
}
