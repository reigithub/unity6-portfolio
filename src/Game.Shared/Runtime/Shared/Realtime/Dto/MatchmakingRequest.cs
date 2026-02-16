using MessagePack;

namespace Game.Library.Shared.Realtime.Dto
{
    [MessagePackObject]
    public class MatchmakingRequest
    {
        [Key(0)]
        public string GameMode { get; set; } = string.Empty;
    }
}
