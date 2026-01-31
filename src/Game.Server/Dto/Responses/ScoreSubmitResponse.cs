namespace Game.Server.Dto.Responses;

public class ScoreSubmitResponse
{
    public long ScoreId { get; set; }

    public bool IsNewBest { get; set; }

    public int CurrentRank { get; set; }
}
