using Game.Server.Database;
using Game.Server.Extensions;
using Game.Server.Filters;
using Game.Server.Shared.Configuration;
using Game.Server.Shared.Health;
using Game.Server.Infrastructure;
using Game.Server.Middleware;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.SignalR;
using Scalar.AspNetCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

namespace Game.Server;

public partial class Program
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

        // Serilog を MEL プロバイダーとして登録
        builder.Services.AddSerilog((services, lc) => lc
            .ReadFrom.Configuration(builder.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext());

        // Controllers (MessagePack primary, JSON fallback)
        builder.Services.AddControllers(options =>
            {
                options.InputFormatters.Insert(0, new MessagePackInputFormatter());
                options.OutputFormatters.Insert(0, new MessagePackOutputFormatter());
            })
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNamingPolicy =
                    System.Text.Json.JsonNamingPolicy.CamelCase;
            });

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddOpenApi();

        // Database
        builder.Services.AddDatabase(builder.Configuration, builder.Environment);

        // Authentication
        builder.Services.AddJwtAuthentication(builder.Configuration);

        // Valkey/Redis Cache
        builder.Services.AddValkey(builder.Configuration);

        // Application Services
        builder.Services.AddApplicationServices(builder.Configuration);

        // SignalR + MessagePack プロトコル
        builder.Services.AddSignalR(options =>
        {
            options.AddFilter<ChatValidationFilter>();
        })
            .AddMessagePackProtocol();

        // Chat サービス登録
        builder.Services.AddChatServices(builder.Configuration);

        // Health Checks
        builder.Services.AddAppHealthChecks(builder.Configuration);

        // CORS（SignalR は AllowCredentials が必要）
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.SetIsOriginAllowed(_ => true)
                      .AllowAnyMethod()
                      .AllowAnyHeader()
                      .AllowCredentials();
            });
        });

        // Response Caching
        builder.Services.AddResponseCaching();

        // OpenTelemetry トレース・メトリクス（Development: Aspire Dashboard 検証）
        if (builder.Environment.IsDevelopment())
        {
            builder.Services.AddOpenTelemetry()
                .ConfigureResource(r => r.AddService("game-server"))
                .WithTracing(tracing => tracing
                    .AddAspNetCoreInstrumentation()
                    .AddOtlpExporter(o => o.Endpoint = new Uri("http://localhost:18889")))
                .WithMetrics(metrics => metrics
                    .AddAspNetCoreInstrumentation()
                    .AddOtlpExporter(o => o.Endpoint = new Uri("http://localhost:18889")));
        }

        var app = builder.Build();

        // Middleware Pipeline
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.MapScalarApiReference(options =>
            {
                options.WithTitle("Game Server API")
                       .WithTheme(ScalarTheme.DeepSpace)
                       .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
            });
        }

        app.UseSerilogRequestLogging();
        app.UseMiddleware<ExceptionHandlingMiddleware>();

        app.UseHttpsRedirection();
        app.UseCors();
        app.UseResponseCaching();
        app.UseRouting();
        app.UseAuthentication();
        app.UseMiddleware<RequestSigningMiddleware>();
        app.UseAuthorization();

        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = HealthCheckResponseWriter.WriteAsync,
        });
        app.MapControllers();
        app.MapHub<Game.Server.Hubs.ChatHub>("/hubs/chat");

        // FluentMigrator: auto-apply migrations in Development
        if (app.Environment.IsDevelopment())
        {
            var connectionString = app.Configuration.GetConnectionString("Default")
                ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured.");

            foreach (var schema in MigrationSchema.All)
            {
                MigrationRunnerFactory.MigrateUp(connectionString, schema);
            }
        }

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
