using System.ComponentModel.DataAnnotations;
using MessagePack;
using Key = MessagePack.KeyAttribute;

namespace Game.Library.Shared.Dto
{
    [MessagePackObject]
    public class CreateLobbyRequest
    {
        [Key(0)]
        [Required]
        [StringLength(50, MinimumLength = 1)]
        public string LobbyName { get; set; } = string.Empty;

        [Key(1)]
        [Required]
        [StringLength(30, MinimumLength = 1)]
        public string GameMode { get; set; } = string.Empty;

        [Key(2)]
        [Range(2, 16)]
        public int MaxPlayers { get; set; } = 4;

        [Key(3)]
        public bool IsPublic { get; set; } = true;

        [Key(4)]
        [Required]
        [StringLength(50, MinimumLength = 1)]
        public string PlayerName { get; set; } = string.Empty;
    }
}
