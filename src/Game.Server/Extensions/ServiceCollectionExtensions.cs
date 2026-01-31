using System.Text;
using Game.Server.Configuration;
using Game.Server.Data;
using Game.Server.Repositories;
using Game.Server.Repositories.Interfaces;
using Game.Server.Services;
using Game.Server.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace Game.Server.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDatabase(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
        {
            var config = serviceProvider.GetRequiredService<IConfiguration>();
            string provider = config.GetValue<string>("Database:Provider") ?? "PostgreSQL";
            string? connectionString = config.GetConnectionString("Default");

            switch (provider)
            {
                case "PostgreSQL":
                    options.UseNpgsql(connectionString, npgsql =>
                    {
                        npgsql.EnableRetryOnFailure(
                            maxRetryCount: 3,
                            maxRetryDelay: TimeSpan.FromSeconds(5),
                            errorCodesToAdd: null);
                    });
                    break;

                case "SQLite":
                    options.UseSqlite(connectionString ?? "Data Source=gameserver.db");
                    break;

                case "InMemory":
                    options.UseInMemoryDatabase(connectionString ?? "GameServer");
                    break;

                default:
                    throw new InvalidOperationException(
                        $"Unsupported database provider: {provider}");
            }

            var env = serviceProvider.GetRequiredService<IWebHostEnvironment>();
            if (env.IsDevelopment())
            {
                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();
            }
        });

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
            });

        services.AddAuthorization();
        return services;
    }

    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services)
    {
        // Services
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IRankingService, RankingService>();
        services.AddScoped<IScoreService, ScoreService>();
        services.AddSingleton<IMasterDataService, MasterDataService>();

        // Repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRankingRepository, RankingRepository>();
        services.AddScoped<IScoreRepository, ScoreRepository>();

        return services;
    }
}
