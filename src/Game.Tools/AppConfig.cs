using Microsoft.Extensions.Configuration;

namespace Game.Tools;

/// <summary>
/// Provides application configuration loaded from appsettings.json.
/// </summary>
public static class AppConfig
{
    private static IConfiguration? _configuration;

    private static IConfiguration Configuration => _configuration ??= new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json", optional: true)
        .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"}.json", optional: true)
        .Build();

    /// <summary>
    /// Resolve connection string: use the explicit value if provided, otherwise fall back to appsettings.json.
    /// </summary>
    public static string ResolveConnectionString(string? connectionString)
    {
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            return connectionString;
        }

        return Configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "Connection string not provided and ConnectionStrings:Default is not configured in appsettings.json.");
    }
}
