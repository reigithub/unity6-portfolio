using System.Text;
using Game.Realtime.Extensions;
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
            options.EnableStreamingHubHeartbeat = true;
            options.StreamingHubHeartbeatInterval = TimeSpan.FromSeconds(30);
            options.StreamingHubHeartbeatTimeout = TimeSpan.FromSeconds(10);
        })
        .UseRedisGroup(options =>
        {
            options.ConnectionString = valkeyConnectionString;
        });

        // JWT Authentication
        var jwtSecret = builder.Configuration["Jwt:Secret"]
            ?? "your-secret-key-must-be-at-least-32-characters-long";
        var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "Game.Server";
        var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "Game.Client";

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtIssuer,
                    ValidAudience = jwtAudience,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtSecret)),
                };
            });

        builder.Services.AddAuthorization();

        // Realtime application services
        builder.Services.AddRealtimeServices(builder.Configuration);

        var app = builder.Build();

        // Map MagicOnion hubs & health check
        app.MapMagicOnionService();
        app.MapRealtimeEndpoints();

        // Graceful shutdown
        var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
        lifetime.ApplicationStopping.Register(() =>
        {
            app.Logger.LogInformation("Realtime server shutting down gracefully...");
            Thread.Sleep(TimeSpan.FromSeconds(60));
        });

        app.Run();
    }
}
