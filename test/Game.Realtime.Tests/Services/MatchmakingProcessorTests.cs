using Game.Realtime.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;

namespace Game.Realtime.Tests.Services;

/// <summary>
/// MatchmakingProcessor のテスト
/// </summary>
public class MatchmakingProcessorTests
{
    private readonly Mock<IMatchmakingQueueService> _queueServiceMock;
    private readonly Mock<IMatchSessionTokenService> _tokenServiceMock;
    private readonly Mock<IConnectionMultiplexer> _redisMock;
    private readonly Mock<ISubscriber> _subscriberMock;
    private readonly Mock<ILogger<MatchmakingProcessor>> _loggerMock;
    private readonly MatchmakingConfiguration _config;

    public MatchmakingProcessorTests()
    {
        _queueServiceMock = new Mock<IMatchmakingQueueService>();
        _tokenServiceMock = new Mock<IMatchSessionTokenService>();
        _redisMock = new Mock<IConnectionMultiplexer>();
        _subscriberMock = new Mock<ISubscriber>();
        _loggerMock = new Mock<ILogger<MatchmakingProcessor>>();

        _redisMock.Setup(x => x.GetSubscriber(It.IsAny<object>()))
            .Returns(_subscriberMock.Object);

        _config = new MatchmakingConfiguration
        {
            GameModes = new Dictionary<string, GameModeConfig>
            {
                ["survival"] = new GameModeConfig { MatchSize = 4 },
            },
        };
    }

    private MatchmakingProcessor CreateProcessor()
    {
        return new MatchmakingProcessor(
            _queueServiceMock.Object,
            _tokenServiceMock.Object,
            _redisMock.Object,
            Options.Create(_config),
            _loggerMock.Object);
    }

    [Fact]
    public async Task ProcessAsync_DoesNothing_WhenQueueBelowMatchSize()
    {
        // Arrange
        _queueServiceMock.Setup(x => x.GetQueueCountAsync("survival"))
            .ReturnsAsync(3);

        var processor = CreateProcessor();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        // Act
        await processor.StartAsync(cts.Token);
        await Task.Delay(50);
        await processor.StopAsync(CancellationToken.None);

        // Assert: マッチが作成されないことを確認
        _queueServiceMock.Verify(
            x => x.DequeueTopPlayersAsync(It.IsAny<string>(), It.IsAny<int>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_CreatesMatch_WhenEnoughPlayers()
    {
        // Arrange
        var callCount = 0;
        _queueServiceMock.Setup(x => x.GetQueueCountAsync("survival"))
            .ReturnsAsync(() =>
            {
                callCount++;
                // 最初の呼び出しは 4 を返し、2回目以降は 0 を返す
                return callCount == 1 ? 4 : 0;
            });

        _queueServiceMock.Setup(x => x.DequeueTopPlayersAsync("survival", 4))
            .ReturnsAsync(new[] { "p1", "p2", "p3", "p4" });

        _tokenServiceMock.Setup(x => x.IssueTokenAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync("token");

        _subscriberMock.Setup(x => x.PublishAsync(
                It.IsAny<RedisChannel>(),
                It.IsAny<RedisValue>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(1);

        var processor = CreateProcessor();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act
        await processor.StartAsync(cts.Token);
        await Task.Delay(3000);
        await processor.StopAsync(CancellationToken.None);

        // Assert: 4人分のトークンが発行されたことを確認
        _tokenServiceMock.Verify(
            x => x.IssueTokenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>()),
            Times.Exactly(4));

        // Assert: 4人分の通知が発行されたことを確認
        _subscriberMock.Verify(
            x => x.PublishAsync(
                It.IsAny<RedisChannel>(),
                It.IsAny<RedisValue>(),
                It.IsAny<CommandFlags>()),
            Times.Exactly(4));
    }

    [Fact]
    public async Task ProcessAsync_ReenqueuesPlayers_WhenNotEnoughDequeued()
    {
        // Arrange
        var callCount = 0;
        _queueServiceMock.Setup(x => x.GetQueueCountAsync("survival"))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1 ? 4 : 0;
            });

        _queueServiceMock.Setup(x => x.DequeueTopPlayersAsync("survival", 4))
            .ReturnsAsync(new[] { "p1", "p2" }); // 4人のうち2人しか取得できなかった

        var processor = CreateProcessor();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act
        await processor.StartAsync(cts.Token);
        await Task.Delay(3000);
        await processor.StopAsync(CancellationToken.None);

        // Assert: 2人が再エンキューされたことを確認
        _queueServiceMock.Verify(
            x => x.EnqueuePlayerAsync(It.IsAny<string>(), "survival"),
            Times.Exactly(2));

        // Assert: トークンが発行されていないことを確認
        _tokenServiceMock.Verify(
            x => x.IssueTokenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>()),
            Times.Never);
    }
}
