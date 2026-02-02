using System.Text;
using FluentMigrator.Runner;
using Game.Server.Configuration;
using Game.Server.Database;
using Game.Server.Database.Migrations;
using Game.Server.Repositories.Dapper;
using Game.Server.Repositories.Interfaces;
using Game.Server.Services;
using Game.Server.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Game.Server.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDatabase(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        services.AddSingleton<IDbConnectionFactory, DbConnectionFactory>();

        string provider = configuration.GetValue<string>("Database:Provider") ?? "PostgreSQL";
        string connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured.");

        services.AddFluentMigratorCore()
            .ConfigureRunner(runner =>
            {
                runner.AddPostgres();
                runner.WithGlobalConnectionString(connectionString);
                runner.ScanIn(typeof(M0001_CreateMasterSchema).Assembly).For.Migrations();
            })
            .AddLogging(lb => lb.AddFluentMigratorConsole());

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
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // MasterData
        services.Configure<MasterDataSettings>(configuration.GetSection("MasterData"));
        services.AddSingleton<IMasterDataService, MasterDataService>();

        // Services
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IRankingService, RankingService>();
        services.AddScoped<IScoreService, ScoreService>();

        // Repositories
        services.AddScoped<IAuthRepository, DapperAuthRepository>();
        services.AddScoped<IUserRepository, DapperUserRepository>();
        services.AddScoped<IRankingRepository, DapperRankingRepository>();
        services.AddScoped<IScoreRepository, DapperScoreRepository>();

        return services;
    }
}
