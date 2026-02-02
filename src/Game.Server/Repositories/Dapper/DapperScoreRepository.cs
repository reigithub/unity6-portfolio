using System.Data;
using System.Text;
using Dapper;
using Game.Server.Database;
using Game.Server.Tables;
using Game.Server.Repositories.Interfaces;

namespace Game.Server.Repositories.Dapper;

public class DapperScoreRepository : IScoreRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public DapperScoreRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<UserScore> AddAsync(UserScore score)
    {
        using var connection = _connectionFactory.CreateConnection();

        score.Id = await connection.ExecuteScalarAsync<long>(
            @"INSERT INTO ""User"".""UserScore"" (""UserId"", ""GameMode"", ""StageId"", ""Score"", ""ClearTime"", ""WaveReached"", ""EnemiesDefeated"", ""RecordedAt"")
              VALUES (@UserId, @GameMode, @StageId, @Score, @ClearTime, @WaveReached, @EnemiesDefeated, @RecordedAt)
              RETURNING ""Id""",
            score);

        return score;
    }

    public async Task<List<UserScore>> GetUserScoresAsync(
        string userId, string? gameMode, int? stageId, int limit)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sb = new StringBuilder(
            @"SELECT ""Id"", ""UserId"", ""GameMode"", ""StageId"", ""Score"", ""ClearTime"", ""WaveReached"", ""EnemiesDefeated"", ""RecordedAt""
              FROM ""User"".""UserScore"" WHERE ""UserId"" = @UserId");

        var parameters = new DynamicParameters();
        parameters.Add("UserId", userId);

        if (!string.IsNullOrEmpty(gameMode))
        {
            sb.Append(@" AND ""GameMode"" = @GameMode");
            parameters.Add("GameMode", gameMode);
        }

        if (stageId.HasValue)
        {
            sb.Append(@" AND ""StageId"" = @StageId");
            parameters.Add("StageId", stageId.Value);
        }

        sb.Append(@" ORDER BY ""RecordedAt"" DESC LIMIT @Limit");
        parameters.Add("Limit", limit);

        var results = await connection.QueryAsync<UserScore>(sb.ToString(), parameters);
        return results.AsList();
    }
}
