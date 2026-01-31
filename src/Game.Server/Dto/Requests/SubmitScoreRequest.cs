using System.ComponentModel.DataAnnotations;

namespace Game.Server.Dto.Requests;

public class SubmitScoreRequest
{
    [Required]
    public int StageId { get; set; }

    [Required]
    public int Score { get; set; }

    public float ClearTime { get; set; }

    [Required]
    public string GameMode { get; set; } = string.Empty;

    public int WaveReached { get; set; }

    public int EnemiesDefeated { get; set; }
}
