using MessagePack;

namespace Game.Library.Shared.Dto
{
    [MessagePackObject]
    public class MatchmakingResponse
    {
        [Key(0)]
        public bool Success { get; set; }

        [Key(1)]
        public string TicketId { get; set; } = string.Empty;

        [Key(2)]
        public int EstimatedWaitSeconds { get; set; }

        [Key(3)]
        public int PlayersInQueue { get; set; }

        [Key(4)]
        public string ErrorMessage { get; set; } = string.Empty;
    }
}
