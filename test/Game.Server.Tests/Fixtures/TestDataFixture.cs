using Game.Server.Configuration;
using Game.Server.Data;
using Game.Server.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Game.Server.Tests.Fixtures;

public static class TestDataFixture
{
    public static readonly JwtSettings TestJwtSettings = new()
    {
        Secret = "test-secret-key-must-be-at-least-32-characters-long!",
        Issuer = "Game.Server",
        Audience = "Game.Client",
        ExpirationMinutes = 60,
        RefreshExpirationDays = 30,
    };

    public static IOptions<JwtSettings> GetJwtOptions()
    {
        return Options.Create(TestJwtSettings);
    }

    public static AppDbContext CreateInMemoryContext(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    public static async Task<AppDbContext> CreateSeededContextAsync(string? dbName = null)
    {
        var context = CreateInMemoryContext(dbName);

        var users = new[]
        {
            new UserEntity
            {
                Id = "user-1",
                DisplayName = "Player1",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password1!"),
                Level = 5,
            },
            new UserEntity
            {
                Id = "user-2",
                DisplayName = "Player2",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password2!"),
                Level = 3,
            },
            new UserEntity
            {
                Id = "user-3",
                DisplayName = "Player3",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password3!"),
                Level = 1,
            },
        };

        context.Users.AddRange(users);

        var scores = new[]
        {
            new ScoreEntity { UserId = "user-1", GameMode = "Survivor", StageId = 1, Score = 5000, ClearTime = 120f, WaveReached = 10, EnemiesDefeated = 50 },
            new ScoreEntity { UserId = "user-2", GameMode = "Survivor", StageId = 1, Score = 8000, ClearTime = 90f, WaveReached = 15, EnemiesDefeated = 80 },
            new ScoreEntity { UserId = "user-3", GameMode = "Survivor", StageId = 1, Score = 3000, ClearTime = 60f, WaveReached = 5, EnemiesDefeated = 20 },
            new ScoreEntity { UserId = "user-1", GameMode = "ScoreTimeAttack", StageId = 1, Score = 12000, ClearTime = 45f, EnemiesDefeated = 100 },
        };

        context.Scores.AddRange(scores);
        await context.SaveChangesAsync();

        return context;
    }
}
