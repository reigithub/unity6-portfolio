using System.Security.Cryptography;

namespace Game.Server.Extensions;

public static class UserIdGenerator
{
    private const string Chars = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const int Length = 10; // 62^10 ≈ 8.4 × 10^17

    public static string Generate()
    {
        return RandomNumberGenerator.GetString(Chars, Length);
    }
}
