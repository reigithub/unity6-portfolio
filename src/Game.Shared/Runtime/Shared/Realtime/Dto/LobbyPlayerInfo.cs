using MessagePack;

namespace Game.Library.Shared.Realtime.Dto
{
    [MessagePackObject]
    public class LobbyPlayerInfo
    {
        [Key(0)]
        public string UserId { get; set; } = string.Empty;

        [Key(1)]
        public string PlayerName { get; set; } = string.Empty;

        [Key(2)]
        public bool IsReady { get; set; }

        [Key(3)]
        public bool IsHost { get; set; }
    }
}
