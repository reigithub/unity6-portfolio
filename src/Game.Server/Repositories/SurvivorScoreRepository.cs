using System.Text;
using Dapper;
using Game.Server.Database;
using Game.Server.Tables;
using Game.Server.Repositories.Interfaces;

namespace Game.Server.Repositories;

public class SurvivorScoreRepository : ISurvivorScoreRepository
{
    private readonly IDbSession _dbSession;

    public SurvivorScoreRepository(IDbSession dbSession)
    {
        _dbSession = dbSession;
    }

    public async Task<SurvivorScore> AddAsync(SurvivorScore score)
    {
        // UPSERT: 新規INSERT、または既存行よりスコアが上回る場合のみUPDATE
        score.Id = await _dbSession.Connection.ExecuteScalarAsync<long>(
            @"WITH upserted AS (
                  INSERT INTO ""Ranking"".""SurvivorScore"" (""UserId"", ""StageId"", ""Score"", ""ClearTime"", ""WaveReached"", ""EnemiesDefeated"", ""RecordedAt"")
                  VALUES (@UserId, @StageId, @Score, @ClearTime, @WaveReached, @EnemiesDefeated, @RecordedAt)
                  ON CONFLICT (""UserId"", ""StageId"") DO UPDATE SET
                      ""Score"" = EXCLUDED.""Score"",
                      ""ClearTime"" = EXCLUDED.""ClearTime"",
                      ""WaveReached"" = EXCLUDED.""WaveReached"",
                      ""EnemiesDefeated"" = EXCLUDED.""EnemiesDefeated"",
                      ""RecordedAt"" = EXCLUDED.""RecordedAt""
                  WHERE EXCLUDED.""Score"" > ""SurvivorScore"".""Score""
                     OR (EXCLUDED.""Score"" = ""SurvivorScore"".""Score""
                         AND EXCLUDED.""ClearTime"" < ""SurvivorScore"".""ClearTime"")
                  RETURNING ""Id""
              )
              SELECT COALESCE(
                  (SELECT ""Id"" FROM upserted),
                  (SELECT ""Id"" FROM ""Ranking"".""SurvivorScore""
                   WHERE ""UserId"" = @UserId AND ""StageId"" = @StageId)
              )",
            score,
            transaction: _dbSession.Transaction);

        return score;
    }

    public async Task<List<SurvivorScore>> GetUserScoresAsync(
        Guid userId, int? stageId, int limit)
    {
        var sb = new StringBuilder(
            @"SELECT ""Id"", ""UserId"", ""StageId"", ""Score"", ""ClearTime"", ""WaveReached"", ""EnemiesDefeated"", ""RecordedAt"",
                     ""CreatedAt"", ""UpdatedAt""
              FROM ""Ranking"".""SurvivorScore"" WHERE ""UserId"" = @UserId");

        var parameters = new DynamicParameters();
        parameters.Add("UserId", userId);

        if (stageId.HasValue)
        {
            sb.Append(@" AND ""StageId"" = @StageId");
            parameters.Add("StageId", stageId.Value);
        }

        sb.Append(@" ORDER BY ""RecordedAt"" DESC LIMIT @Limit");
        parameters.Add("Limit", limit);

        var results = await _dbSession.Connection.QueryAsync<SurvivorScore>(
            sb.ToString(),
            parameters,
            transaction: _dbSession.Transaction);
        return results.AsList();
    }
}
