namespace Game.Server.Dto.Responses;

public class LoginResponse
{
    public string UserId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Token { get; set; } = string.Empty;

    public bool IsNewUser { get; set; }
}
