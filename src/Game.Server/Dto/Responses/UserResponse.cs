namespace Game.Server.Dto.Responses;

public class UserResponse
{
    public string UserId { get; set; } = string.Empty;

    public string UserName { get; set; } = string.Empty;

    public int Level { get; set; }

    public long RegisteredAt { get; set; }

    public string AuthType { get; set; } = string.Empty;

    public string? Email { get; set; }

    public bool HasTransferPassword { get; set; }
}
