namespace Game.Server.Dto.Responses;

public class AccountLinkResponse
{
    public string UserId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Token { get; set; } = string.Empty;

    public string AuthType { get; set; } = string.Empty;

    public string? Email { get; set; }
}
