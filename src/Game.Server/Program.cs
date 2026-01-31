using FluentMigrator.Runner;
using Game.Server.Extensions;
using Game.Server.Middleware;

namespace Game.Server;

public partial class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Controllers
        builder.Services.AddControllers()
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

        // Application Services
        builder.Services.AddApplicationServices();

        // CORS
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.AllowAnyOrigin()
                      .AllowAnyMethod()
                      .AllowAnyHeader();
            });
        });

        // Response Caching
        builder.Services.AddResponseCaching();

        var app = builder.Build();

        // Middleware Pipeline
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.UseMiddleware<RequestLoggingMiddleware>();
        app.UseMiddleware<ExceptionHandlingMiddleware>();

        app.UseHttpsRedirection();
        app.UseCors();
        app.UseResponseCaching();
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();

        // FluentMigrator: auto-apply migrations in Development
        if (app.Environment.IsDevelopment())
        {
            using var scope = app.Services.CreateScope();
            var runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
            runner.MigrateUp();
        }

        app.Run();
    }
}
