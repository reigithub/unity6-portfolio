using MessagePack;

namespace Game.Library.Shared.Realtime.Dto
{
    [MessagePackObject]
    public class CreateLobbyResponse
    {
        [Key(0)]
        public bool Success { get; set; }

        [Key(1)]
        public string LobbyId { get; set; } = string.Empty;

        [Key(2)]
        public string ErrorMessage { get; set; } = string.Empty;
    }
}
