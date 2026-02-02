using System.Text;

namespace Game.Tools.CodeGen;

/// <summary>
/// Converts between naming conventions used in proto (snake_case) and C# (PascalCase).
/// </summary>
public static class NameConverter
{
    /// <summary>
    /// Convert snake_case to PascalCase.
    /// Example: "weapon_id" → "WeaponId"
    /// </summary>
    public static string ToPascalCase(string snakeCase)
    {
        if (string.IsNullOrEmpty(snakeCase))
        {
            return snakeCase;
        }

        var sb = new StringBuilder();
        bool capitalizeNext = true;

        foreach (char c in snakeCase)
        {
            if (c == '_')
            {
                capitalizeNext = true;
                continue;
            }

            sb.Append(capitalizeNext ? char.ToUpperInvariant(c) : c);
            capitalizeNext = false;
        }

        return sb.ToString();
    }

    /// <summary>
    /// Convert PascalCase to snake_case.
    /// Example: "WeaponId" → "weapon_id"
    /// </summary>
    public static string ToSnakeCase(string pascalCase)
    {
        if (string.IsNullOrEmpty(pascalCase))
        {
            return pascalCase;
        }

        var sb = new StringBuilder();

        for (int i = 0; i < pascalCase.Length; i++)
        {
            char c = pascalCase[i];
            if (char.IsUpper(c))
            {
                if (i > 0)
                {
                    // Don't add underscore between consecutive uppercase letters
                    // unless followed by a lowercase letter (e.g., "BGM" stays "bgm", "BgmAsset" → "bgm_asset")
                    bool prevIsUpper = char.IsUpper(pascalCase[i - 1]);
                    bool nextIsLower = i + 1 < pascalCase.Length && char.IsLower(pascalCase[i + 1]);
                    if (!prevIsUpper || nextIsLower)
                    {
                        sb.Append('_');
                    }
                }

                sb.Append(char.ToLowerInvariant(c));
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }
}
