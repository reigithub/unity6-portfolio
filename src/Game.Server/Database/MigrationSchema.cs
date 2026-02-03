namespace Game.Server.Database;

public static class MigrationSchema
{
    public const string Master = "Master";
    public const string User = "User";

    /// <summary>適用順（Up では先頭から、Down では末尾から）。</summary>
    public static readonly string[] All = [Master, User];

    /// <summary>
    /// スキーマ名を解決する。空文字列の場合は全スキーマを返す。
    /// </summary>
    public static string[] ResolveSchemas(string schema)
    {
        if (string.IsNullOrEmpty(schema) || schema.Equals("all", StringComparison.OrdinalIgnoreCase))
            return All;

        var match = All
            .FirstOrDefault(s => s.Equals(schema, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException(
                $"Unknown schema '{schema}'. Valid schemas: all, {string.Join(", ", All)}");

        return [match];
    }
}
