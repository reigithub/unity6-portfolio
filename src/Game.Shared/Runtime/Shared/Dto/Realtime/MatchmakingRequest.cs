using MessagePack;

namespace Game.Library.Shared.Dto
{
    [MessagePackObject]
    public class MatchmakingRequest
    {
        [Key(0)]
        public string GameMode { get; set; } = string.Empty;
    }
}
