namespace Game.Server.Dto.Responses;

public class RankingResponse
{
    public string GameMode { get; set; } = string.Empty;

    public int StageId { get; set; }

    public int TotalCount { get; set; }

    public List<RankingEntryResponse> Entries { get; set; } = new();
}

public class RankingEntryResponse
{
    public int Rank { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string UserName { get; set; } = string.Empty;

    public int Score { get; set; }

    public float ClearTime { get; set; }

    public long RecordedAt { get; set; }
}
