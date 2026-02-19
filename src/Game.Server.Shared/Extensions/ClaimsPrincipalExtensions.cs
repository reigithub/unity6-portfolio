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
}
