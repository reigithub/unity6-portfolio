using MessagePack;

namespace Game.Library.Shared.Realtime.Dto
{
    [MessagePackObject]
    public class CreateLobbyRequest
    {
        [Key(0)]
        public string LobbyName { get; set; } = string.Empty;

        [Key(1)]
        public string GameMode { get; set; } = string.Empty;

        [Key(2)]
        public int MaxPlayers { get; set; } = 4;

        [Key(3)]
        public bool IsPublic { get; set; } = true;
    }
}
