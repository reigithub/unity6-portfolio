using Game.Server.Database;
using Game.Server.Services.Interfaces;
using Game.Server.Tests.Fixtures;
using Game.Server.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using StackExchange.Redis;

namespace Game.Server.Tests.Integration;

/// <summary>
/// 統合テスト共通のサービスモック登録。
/// DB 接続不要・外部サービス不要でアプリを起動できるようにする。
/// </summary>
internal static class TestServiceOverrides
{
    public static void Apply(IServiceCollection services)
    {
        // InMemory DB connection (ダミー — マイグレーション不要のテスト向け)
        services.RemoveAll<IDbConnectionFactory>();
        services.AddSingleton<IDbConnectionFactory>(
            new TestDbConnectionFactory("Host=localhost;Database=dummy"));

        // MasterData
        var mockMasterData = new Mock<IMasterDataService>();
        services.RemoveAll<IMasterDataService>();
        services.AddSingleton(mockMasterData.Object);

        // ScoreValidator
        var mockValidation = new Mock<ISurvivorScoreValidator>();
        services.RemoveAll<ISurvivorScoreValidator>();
        services.AddSingleton(mockValidation.Object);

        // Valkey/Redis
        var mockDb = new Mock<IDatabase>();
        mockDb.Setup(d => d.PingAsync(It.IsAny<CommandFlags>()))
            .ReturnsAsync(TimeSpan.FromMilliseconds(1));
        var mockRedis = new Mock<IConnectionMultiplexer>();
        mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(mockDb.Object);
        mockRedis.Setup(r => r.IsConnected).Returns(true);
        services.RemoveAll<IConnectionMultiplexer>();
        services.AddSingleton(mockRedis.Object);

        // Email
        var mockEmailService = new Mock<IEmailService>();
        mockEmailService
            .Setup(e => e.SendVerificationEmailAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);
        mockEmailService
            .Setup(e => e.SendPasswordResetEmailAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);
        services.RemoveAll<IEmailService>();
        services.AddSingleton(mockEmailService.Object);
    }
}
