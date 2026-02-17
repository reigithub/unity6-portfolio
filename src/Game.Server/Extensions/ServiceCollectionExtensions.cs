using System.Text;
using Game.Server.Configuration;
using Game.Server.Database;
using Game.Server.Repositories.Dapper;
using Game.Server.Repositories.Interfaces;
using Game.Server.Services;
using Game.Server.Services.Chat;
using Game.Server.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;

namespace Game.Server.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDatabase(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        services.AddSingleton<IDbConnectionFactory, DbConnectionFactory>();
        return services;
    }

    public static IServiceCollection AddValkey(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Valkey") ?? "localhost:6379,abortConnect=false";

        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<ConnectionMultiplexer>>();
            try
            {
                var multiplexer = ConnectionMultiplexer.Connect(connectionString);
                logger.LogInformation("Connected to Valkey/Redis");
                return multiplexer;
            }
            catch (RedisConnectionException ex)
            {
                logger.LogWarning(ex, "Failed to connect to Valkey/Redis. Cache will be unavailable.");
                // 接続失敗時も起動を継続するためにnullではなく接続を試みたインスタンスを返す
                // キャッシュサービス側で接続エラーをハンドリング
                return ConnectionMultiplexer.Connect(ConfigurationOptions.Parse(connectionString));
            }
        });

        services.AddScoped<ISurvivorRankingCacheService, ValkeySurvivorRankingCacheService>();

        return services;
    }

    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwtSettings = configuration.GetSection("Jwt").Get<JwtSettings>()
            ?? new JwtSettings { Secret = "development-secret-key-min-32-chars!" };

        services.Configure<JwtSettings>(configuration.GetSection("Jwt"));

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

                // SignalR WebSocket の access_token クエリ文字列対応
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        if (!string.IsNullOrEmpty(accessToken)
                            && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    },
                };
            });

        services.AddAuthorization();
        return services;
    }

    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Request Signing
        services.Configure<RequestSigningSettings>(configuration.GetSection("RequestSigning"));

        // Auth & Resend
        services.Configure<AuthSettings>(configuration.GetSection("Auth"));
        services.Configure<ResendSettings>(configuration.GetSection("Resend"));

        // MasterData
        services.Configure<MasterDataSettings>(configuration.GetSection("MasterData"));
        services.AddSingleton<IMasterDataService, MasterDataService>();

        // Resend client
        services.AddHttpClient<Resend.ResendClient>();
        services.Configure<Resend.ResendClientOptions>(o =>
        {
            o.ApiToken = configuration.GetSection("Resend")["ApiKey"] ?? string.Empty;
        });
        services.AddTransient<Resend.IResend, Resend.ResendClient>();

        // Email
        services.AddScoped<IEmailService, ResendEmailService>();

        // Services
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IRankingService, RankingService>();
        services.AddScoped<ISurvivorScoreValidationService, SurvivorScoreValidationService>();
        services.AddScoped<ISurvivorScoreService, SurvivorScoreService>();

        // Repositories
        services.AddScoped<IAuthRepository, DapperAuthRepository>();
        services.AddScoped<IUserRepository, DapperUserRepository>();
        services.AddScoped<IRankingRepository, DapperRankingRepository>();
        services.AddScoped<ISurvivorScoreRepository, DapperSurvivorScoreRepository>();

        return services;
    }

    public static IServiceCollection AddChatServices(this IServiceCollection services)
    {
        services.AddSingleton<IChatRoomDataService, ChatRoomDataService>();
        services.AddSingleton<IChatMessageService, ChatMessageService>();
        services.AddSingleton<ChatPermissionValidator>();
        return services;
    }
}
