using System.ComponentModel.DataAnnotations;

namespace Game.Server.Dto.Requests;

public class SubmitSurvivorScoreRequest
{
    [Required]
    public int StageId { get; set; }

    [Required]
    public int Score { get; set; }

    public float ClearTime { get; set; }

    public int WaveReached { get; set; }

    public int EnemiesDefeated { get; set; }
}
