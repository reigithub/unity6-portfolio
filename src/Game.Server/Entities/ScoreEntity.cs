namespace Game.Server.Entities;

public class ScoreEntity
{
    public long Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string GameMode { get; set; } = string.Empty;

    public int StageId { get; set; }

    public int Score { get; set; }

    public float ClearTime { get; set; }

    public int WaveReached { get; set; }

    public int EnemiesDefeated { get; set; }

    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public UserEntity User { get; set; } = null!;
}
