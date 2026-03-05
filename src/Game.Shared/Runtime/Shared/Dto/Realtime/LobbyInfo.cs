using MessagePack;

namespace Game.Library.Shared.Dto
{
    [MessagePackObject]
    public class LobbyInfo
    {
        [Key(0)]
        public string LobbyId { get; set; } = string.Empty;

        [Key(1)]
        public string LobbyName { get; set; } = string.Empty;

        [Key(2)]
        public string HostUserId { get; set; } = string.Empty;

        [Key(3)]
        public string GameMode { get; set; } = string.Empty;

        [Key(4)]
        public int CurrentPlayers { get; set; }

        [Key(5)]
        public int MaxPlayers { get; set; }

        [Key(6)]
        public bool IsPublic { get; set; }

        [Key(7)]
        public int StageId { get; set; }
    }
}
