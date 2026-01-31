using Game.Server.Data;
using Game.Server.Entities;
using Game.Server.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Game.Server.Repositories;

public class ScoreRepository : IScoreRepository
{
    private readonly AppDbContext _dbContext;

    public ScoreRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ScoreEntity> AddAsync(ScoreEntity score)
    {
        _dbContext.Scores.Add(score);
        await _dbContext.SaveChangesAsync();
        return score;
    }

    public async Task<List<ScoreEntity>> GetUserScoresAsync(
        string userId, string? gameMode, int? stageId, int limit)
    {
        IQueryable<ScoreEntity> query = _dbContext.Scores
            .Where(s => s.UserId == userId);

        if (!string.IsNullOrEmpty(gameMode))
        {
            query = query.Where(s => s.GameMode == gameMode);
        }

        if (stageId.HasValue)
        {
            query = query.Where(s => s.StageId == stageId.Value);
        }

        return await query
            .OrderByDescending(s => s.RecordedAt)
            .Take(limit)
            .ToListAsync();
    }
}
