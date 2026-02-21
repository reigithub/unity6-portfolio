using Game.Realtime.Extensions;
using Game.Realtime.Filters;
using Game.Server.Shared.Extensions;
using MagicOnion.Server;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

namespace Game.Realtime;

public class Program
{
    public static async Task Main(string[] args)
    {
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
        builder.Services.AddValkeyConnection(builder.Configuration);
        var valkeyConnectionString = builder.Configuration.GetConnectionString("Valkey")
            ?? "localhost:6379,abortConnect=false";

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
            options.ConnectionString = valkeyConnectionString;
        });

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
}
