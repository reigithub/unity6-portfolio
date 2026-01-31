using Game.Server.Data;
using Game.Server.Extensions;
using Game.Server.Middleware;
using Microsoft.EntityFrameworkCore;

namespace Game.Server;

public partial class Program
{
    public static async Task Main(string[] args)
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

        // DB Migration (auto-apply in Development, skip for InMemory)
        if (app.Environment.IsDevelopment())
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            if (db.Database.IsRelational())
            {
                await db.Database.MigrateAsync();
            }
            else
            {
                await db.Database.EnsureCreatedAsync();
            }
        }

        app.Run();
    }
}
