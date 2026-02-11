using Microsoft.Extensions.Configuration;

namespace Game.Tools;

/// <summary>
/// Provides application configuration loaded from appsettings.json.
/// </summary>
public static class AppConfig
{
    private static readonly Dictionary<string, IConfiguration> _configurations = new();

    private static IConfiguration GetConfiguration(string environment)
    {
        if (!_configurations.TryGetValue(environment, out var config))
        {
            config = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile($"appsettings.{environment}.json", optional: true)
                .Build();
            _configurations[environment] = config;
        }
        return config;
    }

    /// <summary>
    /// Resolve environment alias to canonical name.
    /// </summary>
    /// <param name="env">Environment name or alias (case-insensitive).</param>
    /// <returns>Canonical environment name: "Production" or "Development".</returns>
    public static string ResolveEnvironment(string? env)
    {
        if (string.IsNullOrWhiteSpace(env))
        {
            return Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Development";
        }

        return env.ToLowerInvariant() switch
        {
            "prod" or "release" or "production" => "Production",
            "dev" or "develop" or "development" => "Development",
            _ => env // Allow custom environments
        };
    }

    /// <summary>
    /// Check if the resolved environment is production.
    /// </summary>
    public static bool IsProduction(string? env)
    {
        var resolved = ResolveEnvironment(env);
        return resolved.Equals("Production", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Resolve connection string: use the explicit value if provided, otherwise fall back to appsettings.json.
    /// </summary>
    /// <param name="connectionString">Explicit connection string (optional).</param>
    /// <param name="env">Environment name or alias (optional, defaults to DOTNET_ENVIRONMENT or Development).</param>
    public static string ResolveConnectionString(string? connectionString, string? env = null)
    {
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            return connectionString;
        }

        var environment = ResolveEnvironment(env);
        var configuration = GetConfiguration(environment);

        return configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                $"Connection string not provided and ConnectionStrings:Default is not configured in appsettings.{environment}.json.");
    }
}
