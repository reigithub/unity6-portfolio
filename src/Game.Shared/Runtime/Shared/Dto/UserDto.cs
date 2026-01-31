using MessagePack;

namespace Game.Library.Shared.Dto
{
    [MessagePackObject(true)]
    public class UserDto
    {
        public string UserId { get; set; }

        public string DisplayName { get; set; }

        public int Level { get; set; }

        public long CreatedAt { get; set; }
    }
}
