using System.Security.Cryptography;
using Spectre.Console;

namespace Game.Tools.Commands;

public class SecurityCommands
{
    /// <summary>
    /// Generate a cryptographically secure signing key for HMAC-SHA256 request signing.
    /// </summary>
    public void GenerateSigningKey(int bytes = 32)
    {
        var key = RandomNumberGenerator.GetBytes(bytes);
        var base64Key = Convert.ToBase64String(key);

        AnsiConsole.MarkupLine("[blue]Generated HMAC-SHA256 signing key:[/]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[green]{base64Key}[/]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Set this key in:[/]");
        AnsiConsole.MarkupLine("[dim]  Server: appsettings.*.json → RequestSigning.SecretKey[/]");
        AnsiConsole.MarkupLine("[dim]  Client: GameEnvironmentSettings → SigningSecretKey[/]");
    }
}
