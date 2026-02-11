using System.Text;
using Dapper;
using Game.Server.Database;
using Game.Server.Tables;
using Game.Server.Repositories.Interfaces;

namespace Game.Server.Repositories.Dapper;

public class DapperSurvivorScoreRepository : ISurvivorScoreRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public DapperSurvivorScoreRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<SurvivorScore> AddAsync(SurvivorScore score)
    {
        using var connection = _connectionFactory.CreateConnection();

        score.Id = await connection.ExecuteScalarAsync<long>(
            @"INSERT INTO ""Ranking"".""SurvivorScore"" (""UserId"", ""StageId"", ""Score"", ""ClearTime"", ""WaveReached"", ""EnemiesDefeated"", ""RecordedAt"")
              VALUES (@UserId, @StageId, @Score, @ClearTime, @WaveReached, @EnemiesDefeated, @RecordedAt)
              RETURNING ""Id""",
            score);

        return score;
    }

    public async Task<List<SurvivorScore>> GetUserScoresAsync(
        Guid userId, int? stageId, int limit)
    {
        using var connection = _connectionFactory.CreateConnection();

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

        var results = await connection.QueryAsync<SurvivorScore>(sb.ToString(), parameters);
        return results.AsList();
    }
}
