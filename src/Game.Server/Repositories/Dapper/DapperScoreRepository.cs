using System.Data;
using System.Text;
using Dapper;
using Game.Server.Data;
using Game.Server.Entities;
using Game.Server.Repositories.Interfaces;
using Npgsql;

namespace Game.Server.Repositories.Dapper;

public class DapperScoreRepository : IScoreRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public DapperScoreRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<ScoreEntity> AddAsync(ScoreEntity score)
    {
        using var connection = _connectionFactory.CreateConnection();

        if (connection is NpgsqlConnection)
        {
            score.Id = await connection.ExecuteScalarAsync<long>(
                @"INSERT INTO ""Scores"" (""UserId"", ""GameMode"", ""StageId"", ""Score"", ""ClearTime"", ""WaveReached"", ""EnemiesDefeated"", ""RecordedAt"")
                  VALUES (@UserId, @GameMode, @StageId, @Score, @ClearTime, @WaveReached, @EnemiesDefeated, @RecordedAt)
                  RETURNING ""Id""",
                score);
        }
        else
        {
            await connection.ExecuteAsync(
                @"INSERT INTO ""Scores"" (""UserId"", ""GameMode"", ""StageId"", ""Score"", ""ClearTime"", ""WaveReached"", ""EnemiesDefeated"", ""RecordedAt"")
                  VALUES (@UserId, @GameMode, @StageId, @Score, @ClearTime, @WaveReached, @EnemiesDefeated, @RecordedAt)",
                score);
            score.Id = await connection.ExecuteScalarAsync<long>("SELECT last_insert_rowid()");
        }

        return score;
    }

    public async Task<List<ScoreEntity>> GetUserScoresAsync(
        string userId, string? gameMode, int? stageId, int limit)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sb = new StringBuilder(
            @"SELECT ""Id"", ""UserId"", ""GameMode"", ""StageId"", ""Score"", ""ClearTime"", ""WaveReached"", ""EnemiesDefeated"", ""RecordedAt""
              FROM ""Scores"" WHERE ""UserId"" = @UserId");

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

        var results = await connection.QueryAsync<ScoreEntity>(sb.ToString(), parameters);
        return results.AsList();
    }
}
