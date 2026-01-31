using Game.Server.Data;
using Game.Server.Entities;
using Game.Server.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Game.Server.Repositories;

public class RankingRepository : IRankingRepository
{
    private readonly AppDbContext _dbContext;

    public RankingRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<ScoreEntity>> GetTopScoresAsync(
        string gameMode, int stageId, int limit, int offset)
    {
        return await _dbContext.Scores
            .Include(s => s.User)
            .Where(s => s.GameMode == gameMode && s.StageId == stageId)
            .OrderByDescending(s => s.Score)
            .ThenBy(s => s.ClearTime)
            .Skip(offset)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<ScoreEntity?> GetUserBestScoreAsync(
        string gameMode, int stageId, string userId)
    {
        return await _dbContext.Scores
            .Where(s => s.UserId == userId && s.GameMode == gameMode && s.StageId == stageId)
            .OrderByDescending(s => s.Score)
            .ThenBy(s => s.ClearTime)
            .FirstOrDefaultAsync();
    }

    public async Task<int> GetUserRankAsync(
        string gameMode, int stageId, string userId)
    {
        var userBest = await GetUserBestScoreAsync(gameMode, stageId, userId);
        if (userBest == null)
        {
            return 0;
        }

        int rank = await _dbContext.Scores
            .Where(s => s.GameMode == gameMode && s.StageId == stageId)
            .Where(s => s.Score > userBest.Score
                || (s.Score == userBest.Score && s.ClearTime < userBest.ClearTime))
            .Select(s => s.UserId)
            .Distinct()
            .CountAsync();

        return rank + 1;
    }
}
