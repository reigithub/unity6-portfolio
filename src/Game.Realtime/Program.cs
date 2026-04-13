using System.Net.Security;
using Game.Realtime.Extensions;
using Game.Realtime.Filters;
using Game.Server.Shared.Configuration;
using Game.Server.Shared.Extensions;
using Google.Apis.Auth.OAuth2;
using MagicOnion.Server;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using StackExchange.Redis;

namespace Game.Realtime;

public class Program
{
    public static async Task Main(string[] args)
    {
        // .env → 環境変数（Docker 環境では no-op）
        EnvVarLoader.Load();

        // Bootstrap logger（ホスト構築前のエラーもキャプチャ）
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .CreateBootstrapLogger();

        try
        {
        var builder = WebApplication.CreateBuilder(args);

        // Serilog を MELプロバイダーとして登録
        builder.Services.AddSerilog((services, lc) => lc
            .ReadFrom.Configuration(builder.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext());

        // Kestrel: HTTP/2 only on port 5001
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.ListenAnyIP(5001, listenOptions =>
            {
                listenOptions.Protocols = HttpProtocols.Http2;
            });
        });

        // Valkey/Redis connection
        // MagicOnion の MapMagicOnionService() がルートマッピング時に IConnectionMultiplexer を即座に解決するため、
        // ValkeyConnectionInitializer（IHostedService）パターンではなく、builder.Build() 前に同期接続を確立する。
        var valkeyConnectionString = builder.Configuration.GetConnectionString("Valkey")
            ?? throw new InvalidOperationException("ConnectionStrings:Valkey is not configured.");
        var valkeyOptions = ConfigurationOptions.Parse(valkeyConnectionString);
        GoogleCredential? valkeyCredential = null;

        if (valkeyOptions.Ssl)
        {
            // GCP Memorystore IAM 認証: トークンを取得して接続
            valkeyCredential = await GoogleCredential.GetApplicationDefaultAsync();
            var token = await valkeyCredential.UnderlyingCredential.GetAccessTokenForRequestAsync();
            valkeyOptions.User = "default";
            valkeyOptions.Password = token;
            valkeyOptions.CertificateValidation += (_, _, _, errors) =>
                errors is SslPolicyErrors.None or SslPolicyErrors.RemoteCertificateChainErrors;
        }

        var valkeyMultiplexer = await ConnectionMultiplexer.ConnectAsync(valkeyOptions);
        Log.Information("Connected to Valkey/Redis{IamSuffix}", valkeyOptions.Ssl ? " with IAM authentication" : "");

        builder.Services.AddSingleton<IConnectionMultiplexer>(valkeyMultiplexer);

        // IAM トークンリフレッシュ（4分間隔、トークン有効期限は1時間）
        if (valkeyCredential != null)
        {
            var credential = valkeyCredential;
            builder.Services.AddHostedService(_ => new ValkeyTokenRefreshService(valkeyMultiplexer, credential));
        }

        // gRPC + MagicOnion with Redis backplane
        builder.Services.AddGrpc();
        builder.Services.AddMagicOnion(options =>
        {
            options.GlobalFilters.Add<JwtAuthenticationFilter>();
            options.GlobalFilters.Add<ValidationExceptionFilter>();
            options.GlobalStreamingHubFilters.Add<JwtAuthenticationHubFilter>();
            options.GlobalStreamingHubFilters.Add<ValidationExceptionHubFilter>();
            options.EnableStreamingHubHeartbeat = true;
            options.StreamingHubHeartbeatInterval = TimeSpan.FromSeconds(30);
            options.StreamingHubHeartbeatTimeout = TimeSpan.FromSeconds(10);
        })
        .UseRedisGroup(options =>
        {
            options.ConnectionMultiplexer = valkeyMultiplexer;
        }, registerAsDefault: true);

        // JWT Authentication
        builder.Services.AddJwtValidation(builder.Configuration);

        // Realtime application services
        builder.Services.AddRealtimeServices(builder.Configuration);

        // Graceful shutdown timeout
        builder.Services.Configure<HostOptions>(options =>
        {
            options.ShutdownTimeout = TimeSpan.FromSeconds(
                builder.Configuration.GetValue("Hosting:ShutdownTimeoutSeconds", 60));
        });

        // OpenTelemetry トレース・メトリクス（Development: Aspire Dashboard 検証）
        if (builder.Environment.IsDevelopment())
        {
            builder.Services.AddOpenTelemetry()
                .ConfigureResource(r => r.AddService("game-realtime"))
                .WithTracing(tracing => tracing
                    .AddAspNetCoreInstrumentation()
                    .AddOtlpExporter(o => o.Endpoint = new Uri("http://localhost:18889")))
                .WithMetrics(metrics => metrics
                    .AddAspNetCoreInstrumentation()
                    .AddOtlpExporter(o => o.Endpoint = new Uri("http://localhost:18889")));
        }

        var app = builder.Build();

        // ASP.NET Core authentication / authorization middleware
        app.UseAuthentication();
        app.UseAuthorization();

        // Map MagicOnion hubs & health check
        app.MapMagicOnionService();
        app.MapRealtimeEndpoints();

        app.Run();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }

    /// <summary>
    /// GCP Memorystore IAM トークンを定期的にリフレッシュする BackgroundService。
    /// ValkeyConnectionInitializer 相当の機能を Game.Realtime 用に分離。
    /// </summary>
    private sealed class ValkeyTokenRefreshService : BackgroundService
    {
        private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(4);

        private readonly IConnectionMultiplexer _multiplexer;
        private readonly GoogleCredential _credential;

        public ValkeyTokenRefreshService(IConnectionMultiplexer multiplexer, GoogleCredential credential)
        {
            _multiplexer = multiplexer;
            _credential = credential;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(RefreshInterval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    var newToken = await _credential.UnderlyingCredential.GetAccessTokenForRequestAsync(
                        cancellationToken: stoppingToken);
                    foreach (var server in _multiplexer.GetServers())
                    {
                        await server.ExecuteAsync("AUTH", "default", newToken);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to refresh Valkey IAM token");
                }
            }
        }
    }
}
