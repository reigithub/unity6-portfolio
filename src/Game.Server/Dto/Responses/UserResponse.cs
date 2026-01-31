namespace Game.Server.Dto.Responses;

public class UserResponse
{
    public string UserId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public int Level { get; set; }

    public long CreatedAt { get; set; }
}
