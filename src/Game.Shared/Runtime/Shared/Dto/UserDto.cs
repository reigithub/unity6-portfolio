using MessagePack;

namespace Game.Library.Shared.Dto
{
    [MessagePackObject]
    public class UserDto
    {
        [Key(0)]
        public string UserId { get; set; }

        [Key(1)]
        public string UserName { get; set; }

        [Key(2)]
        public int Level { get; set; }

        [Key(3)]
        public long RegisteredAt { get; set; }
    }
}
