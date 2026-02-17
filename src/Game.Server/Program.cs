using Game.Server.Database;
using Game.Server.Extensions;
using Game.Server.Infrastructure;
using Game.Server.Middleware;
using Scalar.AspNetCore;

namespace Game.Server;

public partial class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

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
        builder.Services.AddSignalR()
            .AddMessagePackProtocol();

        // Chat サービス登録
        builder.Services.AddChatServices();

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

        app.UseMiddleware<RequestLoggingMiddleware>();
        app.UseMiddleware<ExceptionHandlingMiddleware>();

        app.UseHttpsRedirection();
        app.UseCors();
        app.UseResponseCaching();
        app.UseAuthentication();
        app.UseMiddleware<RequestSigningMiddleware>();
        app.UseAuthorization();

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
}
