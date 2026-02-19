using System.Text;
using Game.Server.Shared.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Game.Server.Shared.Extensions;

/// <summary>
/// JWT Bearer 認証の共通登録拡張メソッド
/// </summary>
public static class JwtServiceExtensions
{
    /// <summary>
    /// JWT Bearer 認証 + Authorization を登録。
    /// Game.Server は configureJwtBearer で SignalR WebSocket イベントを追加可能。
    /// </summary>
    public static IServiceCollection AddJwtValidation(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<JwtBearerOptions>? configureJwtBearer = null)
    {
        services.AddOptions<JwtValidationSettings>()
            .Bind(configuration.GetSection("Jwt"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var jwtSettings = configuration.GetSection("Jwt").Get<JwtValidationSettings>()
            ?? throw new InvalidOperationException(
                "Jwt configuration section is missing. Ensure 'Jwt' is configured in appsettings.");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidAudience = jwtSettings.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtSettings.Secret)),
                };

                configureJwtBearer?.Invoke(options);
            });

        services.AddAuthorization();
        return services;
    }
}
