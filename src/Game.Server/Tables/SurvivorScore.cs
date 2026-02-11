namespace Game.Server.Tables;

public class SurvivorScore
{
    public long Id { get; set; }

    public Guid UserId { get; set; }

    public int StageId { get; set; }

    public int Score { get; set; }

    public float ClearTime { get; set; }

    public int WaveReached { get; set; }

    public int EnemiesDefeated { get; set; }

    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    // Navigation (populated by Dapper multi-mapping)
    public UserInfo? User { get; set; }
}
