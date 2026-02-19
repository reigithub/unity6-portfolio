using System.Text;
using Game.Realtime.Configuration;
using Game.Realtime.Extensions;
using Game.Realtime.Filters;
using MagicOnion.Server;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;

namespace Game.Realtime;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Kestrel: HTTP/1.1 + HTTP/2 on port 5001
        // Http1AndHttp2 allows gRPC (h2c) and plain HTTP health checks
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.ListenAnyIP(5001, listenOptions =>
            {
                listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
            });
        });

        // Valkey/Redis connection
        var valkeyConnectionString = builder.Configuration.GetConnectionString("Valkey")
            ?? "localhost:6379,abortConnect=false";
        builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<ConnectionMultiplexer>>();
            try
            {
                var multiplexer = ConnectionMultiplexer.Connect(valkeyConnectionString);
                logger.LogInformation("Connected to Valkey/Redis for Realtime server");
                return multiplexer;
            }
            catch (RedisConnectionException ex)
            {
                logger.LogWarning(ex, "Failed to connect to Valkey/Redis. Retrying with options...");
                return ConnectionMultiplexer.Connect(ConfigurationOptions.Parse(valkeyConnectionString));
            }
        });

        // gRPC + MagicOnion with Redis backplane
        builder.Services.AddGrpc();
        builder.Services.AddMagicOnion(options =>
        {
            options.GlobalFilters.Add<JwtAuthenticationFilter>();
            options.GlobalFilters.Add<ValidationExceptionFilter>();
            options.GlobalStreamingHubFilters.Add<HubValidationExceptionFilter>();
            options.EnableStreamingHubHeartbeat = true;
            options.StreamingHubHeartbeatInterval = TimeSpan.FromSeconds(30);
            options.StreamingHubHeartbeatTimeout = TimeSpan.FromSeconds(10);
        })
        .UseRedisGroup(options =>
        {
            options.ConnectionString = valkeyConnectionString;
        });

        // JWT Authentication
        builder.Services.AddOptions<JwtValidationSettings>()
            .Bind(builder.Configuration.GetSection("Jwt"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtValidationSettings>()
            ?? throw new InvalidOperationException(
                "Jwt configuration section is missing. Ensure 'Jwt' is configured in appsettings.");

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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

        builder.Services.AddAuthorization();

        // Realtime application services
        builder.Services.AddRealtimeServices(builder.Configuration);

        // Graceful shutdown timeout
        builder.Services.Configure<HostOptions>(options =>
        {
            options.ShutdownTimeout = TimeSpan.FromSeconds(
                builder.Configuration.GetValue("Hosting:ShutdownTimeoutSeconds", 60));
        });

        var app = builder.Build();

        // ASP.NET Core authentication / authorization middleware
        app.UseAuthentication();
        app.UseAuthorization();

        // Map MagicOnion hubs & health check
        app.MapMagicOnionService();
        app.MapRealtimeEndpoints();

        app.Run();
    }
}
