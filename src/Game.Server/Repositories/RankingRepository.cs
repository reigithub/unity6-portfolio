using Dapper;
using Game.Server.Database;
using Game.Server.Tables;
using Game.Server.Repositories.Interfaces;

namespace Game.Server.Repositories;

public class RankingRepository : IRankingRepository
{
    private readonly IDbSession _dbSession;

    public RankingRepository(IDbSession dbSession)
    {
        _dbSession = dbSession;
    }

    public async Task<List<SurvivorScore>> GetTopScoresAsync(
        int stageId, int limit, int offset)
    {
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

        var results = await _dbSession.Connection.QueryAsync<SurvivorScore, UserInfo, SurvivorScore>(
            sql,
            (score, user) =>
            {
                score.User = user;
                return score;
            },
            new { StageId = stageId, Limit = limit, Offset = offset },
            splitOn: "Id",
            transaction: _dbSession.Transaction);

        return results.AsList();
    }

    public async Task<SurvivorScore?> GetUserBestScoreAsync(
        int stageId, Guid userId)
    {
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

        var results = await _dbSession.Connection.QueryAsync<SurvivorScore, UserInfo, SurvivorScore>(
            sql,
            (score, user) =>
            {
                score.User = user;
                return score;
            },
            new { UserId = userId, StageId = stageId },
            splitOn: "Id",
            transaction: _dbSession.Transaction);

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

        const string sql =
            @"SELECT COUNT(DISTINCT ""UserId"")
              FROM ""Ranking"".""SurvivorScore""
              WHERE ""StageId"" = @StageId
                AND (""Score"" > @Score
                     OR (""Score"" = @Score AND ""ClearTime"" < @ClearTime))";

        int higherCount = await _dbSession.Connection.ExecuteScalarAsync<int>(
            sql,
            new
            {
                StageId = stageId,
                Score = userBest.Score,
                ClearTime = userBest.ClearTime,
            },
            transaction: _dbSession.Transaction);

        return higherCount + 1;
    }
}
