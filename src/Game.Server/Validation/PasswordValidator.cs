using System.Text.RegularExpressions;

namespace Game.Server.Validation;

public static partial class PasswordValidator
{
    public static (bool IsValid, string? ErrorMessage) Validate(string password)
    {
        if (password.Length < 8)
        {
            return (false, "Password must be at least 8 characters long");
        }

        if (!UppercaseRegex().IsMatch(password))
        {
            return (false, "Password must contain at least one uppercase letter");
        }

        if (!LowercaseRegex().IsMatch(password))
        {
            return (false, "Password must contain at least one lowercase letter");
        }

        if (!DigitRegex().IsMatch(password))
        {
            return (false, "Password must contain at least one digit");
        }

        if (!SpecialCharRegex().IsMatch(password))
        {
            return (false, "Password must contain at least one special character");
        }

        return (true, null);
    }

    [GeneratedRegex("[A-Z]")]
    private static partial Regex UppercaseRegex();

    [GeneratedRegex("[a-z]")]
    private static partial Regex LowercaseRegex();

    [GeneratedRegex("[0-9]")]
    private static partial Regex DigitRegex();

    [GeneratedRegex(@"[^a-zA-Z0-9]")]
    private static partial Regex SpecialCharRegex();
}
