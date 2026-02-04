using Dapper;
using Game.Server.Database;
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
                     u.""Id"", u.""UserId"", u.""DisplayName"", u.""Level"", u.""CreatedAt"", u.""LastLoginAt""
              FROM ""User"".""UserScore"" s
              INNER JOIN ""User"".""UserInfo"" u ON s.""UserId"" = u.""Id""
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
        string gameMode, int stageId, Guid userId)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql =
            @"SELECT s.""Id"", s.""UserId"", s.""GameMode"", s.""StageId"", s.""Score"",
                     s.""ClearTime"", s.""WaveReached"", s.""EnemiesDefeated"", s.""RecordedAt"",
                     u.""Id"", u.""UserId"", u.""DisplayName"", u.""Level"", u.""CreatedAt"", u.""LastLoginAt""
              FROM ""User"".""UserScore"" s
              INNER JOIN ""User"".""UserInfo"" u ON s.""UserId"" = u.""Id""
              WHERE s.""UserId"" = @UserId AND s.""GameMode"" = @GameMode AND s.""StageId"" = @StageId
              ORDER BY s.""Score"" DESC, s.""ClearTime"" ASC
              LIMIT 1";

        var results = await connection.QueryAsync<UserScore, UserInfo, UserScore>(
            sql,
            (score, user) =>
            {
                score.User = user;
                return score;
            },
            new { UserId = userId, GameMode = gameMode, StageId = stageId },
            splitOn: "Id");

        return results.FirstOrDefault();
    }

    public async Task<int> GetUserRankAsync(
        string gameMode, int stageId, Guid userId)
    {
        var userBest = await GetUserBestScoreAsync(gameMode, stageId, userId);
        if (userBest == null)
        {
            return 0;
        }

        using var connection = _connectionFactory.CreateConnection();

        const string sql =
            @"SELECT COUNT(DISTINCT ""UserId"")
              FROM ""User"".""UserScore""
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
