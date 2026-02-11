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

    public async Task<List<SurvivorScore>> GetTopScoresAsync(
        int stageId, int limit, int offset)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql =
            @"SELECT s.""Id"", s.""UserId"", s.""StageId"", s.""Score"",
                     s.""ClearTime"", s.""WaveReached"", s.""EnemiesDefeated"", s.""RecordedAt"",
                     s.""CreatedAt"", s.""UpdatedAt"",
                     u.""Id"", u.""UserId"", u.""UserName"", u.""Level"", u.""RegisteredAt"", u.""LastLoginAt"",
                     u.""CreatedAt"", u.""UpdatedAt""
              FROM ""Ranking"".""SurvivorScore"" s
              INNER JOIN ""User"".""UserInfo"" u ON s.""UserId"" = u.""Id""
              WHERE s.""StageId"" = @StageId
              ORDER BY s.""Score"" DESC, s.""ClearTime"" ASC
              LIMIT @Limit OFFSET @Offset";

        var results = await connection.QueryAsync<SurvivorScore, UserInfo, SurvivorScore>(
            sql,
            (score, user) =>
            {
                score.User = user;
                return score;
            },
            new { StageId = stageId, Limit = limit, Offset = offset },
            splitOn: "Id");

        return results.AsList();
    }

    public async Task<SurvivorScore?> GetUserBestScoreAsync(
        int stageId, Guid userId)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql =
            @"SELECT s.""Id"", s.""UserId"", s.""StageId"", s.""Score"",
                     s.""ClearTime"", s.""WaveReached"", s.""EnemiesDefeated"", s.""RecordedAt"",
                     s.""CreatedAt"", s.""UpdatedAt"",
                     u.""Id"", u.""UserId"", u.""UserName"", u.""Level"", u.""RegisteredAt"", u.""LastLoginAt"",
                     u.""CreatedAt"", u.""UpdatedAt""
              FROM ""Ranking"".""SurvivorScore"" s
              INNER JOIN ""User"".""UserInfo"" u ON s.""UserId"" = u.""Id""
              WHERE s.""UserId"" = @UserId AND s.""StageId"" = @StageId
              ORDER BY s.""Score"" DESC, s.""ClearTime"" ASC
              LIMIT 1";

        var results = await connection.QueryAsync<SurvivorScore, UserInfo, SurvivorScore>(
            sql,
            (score, user) =>
            {
                score.User = user;
                return score;
            },
            new { UserId = userId, StageId = stageId },
            splitOn: "Id");

        return results.FirstOrDefault();
    }

    public async Task<int> GetUserRankAsync(
        int stageId, Guid userId)
    {
        var userBest = await GetUserBestScoreAsync(stageId, userId);
        if (userBest == null)
        {
            return 0;
        }

        using var connection = _connectionFactory.CreateConnection();

        const string sql =
            @"SELECT COUNT(DISTINCT ""UserId"")
              FROM ""Ranking"".""SurvivorScore""
              WHERE ""StageId"" = @StageId
                AND (""Score"" > @Score
                     OR (""Score"" = @Score AND ""ClearTime"" < @ClearTime))";

        int higherCount = await connection.ExecuteScalarAsync<int>(
            sql,
            new
            {
                StageId = stageId,
                Score = userBest.Score,
                ClearTime = userBest.ClearTime,
            });

        return higherCount + 1;
    }
}
