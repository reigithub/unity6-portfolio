using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Game.Server.Shared.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static string? GetUserId(this ClaimsPrincipal? principal)
    {
        return principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }

    /// <summary>
    /// 認証フィルター通過後に使用する。userId が取得できない場合は InvalidOperationException をスローする。
    /// </summary>
    public static string GetRequiredUserId(this ClaimsPrincipal principal)
    {
        return principal.GetUserId()
            ?? throw new InvalidOperationException("UserId claim not found. Ensure authentication filter has executed.");
    }
}
