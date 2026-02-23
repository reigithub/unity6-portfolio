using Game.Server.Configuration;
using Game.Server.Database;
using Game.Server.Health;
using Game.Server.Repositories;
using Game.Server.Repositories.Interfaces;
using Game.Server.Services;
using Game.Server.Services.Chat;
using Game.Server.Services.Interfaces;
using Game.Server.Shared.Extensions;
using Game.Server.Shared.Health;
using Game.Server.Validation;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Game.Server.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDatabase(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        services.AddSingleton<IDbConnectionFactory, DbConnectionFactory>();
        services.AddScoped<IDbSession, DbSession>();
        return services;
    }

    public static IServiceCollection AddValkey(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddValkeyConnection(configuration);

        // Ranking Cache Settings
        services.Configure<RankingCacheSettings>(configuration.GetSection("RankingCache"));

        services.AddScoped<ISurvivorRankingCacheService, SurvivorRankingCacheService>();

        return services;
    }

    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddJwtValidation(configuration, options =>
        {
            // Game.Server 固有: SignalR WebSocket の access_token クエリ文字列対応
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

        // Game.Server 固有: トークン発行用設定
        services.AddOptions<JwtSettings>()
            .Bind(configuration.GetSection("Jwt"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }

    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Request Signing
        services.AddOptions<RequestSigningSettings>()
            .Bind(configuration.GetSection("RequestSigning"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Auth & Resend
        services.Configure<AuthSettings>(configuration.GetSection("Auth"));
        services.Configure<ResendSettings>(configuration.GetSection("Resend"));

        // MasterData
        services.Configure<MasterDataSettings>(configuration.GetSection("MasterData"));
        services.AddSingleton<IMasterDataService, MasterDataService>();

        // Resend client
        services.AddHttpClient<Resend.ResendClient>()
            .ConfigureHttpClient(client => client.Timeout = TimeSpan.FromSeconds(30));
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
        services.AddScoped<ISurvivorValidator, SurvivorValidator>();
        services.AddScoped<ISurvivorScoreService, SurvivorScoreService>();

        // Repositories
        services.AddScoped<IAuthRepository, AuthRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRankingRepository, RankingRepository>();
        services.AddScoped<ISurvivorScoreRepository, SurvivorScoreRepository>();

        return services;
    }

    public static IServiceCollection AddAppHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<ValkeyHealthCheck>("valkey", tags: new[] { "ready" })
            .AddCheck<PostgresHealthCheck>("postgres", tags: new[] { "ready" });
        return services;
    }

    public static IServiceCollection AddChatServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Chat Settings
        services.Configure<ChatSettings>(configuration.GetSection("Chat"));

        // Distributed Lock Provider (レースコンディション防止)
        services.AddDistributedLock();

        services.AddSingleton<IChatRoomDataService, ChatRoomDataService>();
        services.AddSingleton<IChatMessageService, ChatMessageService>();
        services.AddSingleton<ChatPermissionValidator>();
        services.AddSingleton<IChatInputValidator, ChatInputValidator>();
        return services;
    }
}
