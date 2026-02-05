using System.Security.Cryptography;

namespace Game.Server.Extensions;

public static class UserIdGenerator
{
    private const string Chars = "0123456789";
    private const int Length = 12; // 10^12 = 1,000,000,000,000

    public static string Generate()
    {
        return RandomNumberGenerator.GetString(Chars, Length);
    }

    /// <summary>
    /// Formats a UserId as "0000 0000 0000" for UI display.
    /// </summary>
    public static string FormatForDisplay(string userId)
    {
        return $"{userId[..4]} {userId[4..8]} {userId[8..]}";
    }
}
