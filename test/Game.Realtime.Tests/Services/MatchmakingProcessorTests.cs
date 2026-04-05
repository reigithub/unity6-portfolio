using Game.Library.Shared.Dto;
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
    private readonly Mock<IUnityServerAuthApiClient> _unityServerAuthApiMock;
    private readonly Mock<IConnectionMultiplexer> _redisMock;
    private readonly Mock<ISubscriber> _subscriberMock;
    private readonly Mock<ILogger<MatchmakingProcessor>> _loggerMock;
    private readonly MatchmakingConfiguration _config;
    private readonly UnityServerConfiguration _unityServerConfig;

    public MatchmakingProcessorTests()
    {
        _queueServiceMock = new Mock<IMatchmakingQueueService>();
        _unityServerAuthApiMock = new Mock<IUnityServerAuthApiClient>();
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

        _unityServerConfig = new UnityServerConfiguration
        {
            ServerAddress = "localhost",
            ServerPort = 7777,
        };
    }

    private MatchmakingProcessor CreateProcessor()
    {
        return new MatchmakingProcessor(
            _queueServiceMock.Object,
            _unityServerAuthApiMock.Object,
            _redisMock.Object,
            Options.Create(_config),
            Options.Create(_unityServerConfig),
            _loggerMock.Object);
    }

    [Fact]
    public async Task ProcessAsync_DoesNothing_WhenNoActiveStages()
    {
        // Arrange
        _queueServiceMock.Setup(x => x.GetActiveStageKeysAsync("survival"))
            .ReturnsAsync(Array.Empty<string>());

        var processor = CreateProcessor();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        // Act
        await processor.StartAsync(cts.Token);
        await Task.Delay(50);
        await processor.StopAsync(CancellationToken.None);

        // Assert: Game.Server API が呼ばれないことを確認
        _unityServerAuthApiMock.Verify(
            x => x.IssueTokenAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_CreatesMatch_WhenEnoughPlayersInStageQueue()
    {
        // Arrange
        _queueServiceMock.Setup(x => x.GetActiveStageKeysAsync("survival"))
            .ReturnsAsync(new[] { "1" });

        var queueCallCount = 0;
        _queueServiceMock.Setup(x => x.GetQueueCountAsync("survival", 1))
            .ReturnsAsync(() =>
            {
                queueCallCount++;
                return queueCallCount == 1 ? 2 : 0;
            });

        // 先頭プレイヤー取得
        _queueServiceMock.Setup(x => x.DequeueTopPlayersAsync("survival", 1, 1))
            .ReturnsAsync(new[] { "p1" });

        _queueServiceMock.Setup(x => x.GetPlayerMatchSizeAsync("p1"))
            .ReturnsAsync(2);
        _queueServiceMock.Setup(x => x.GetPlayerMatchSizeAsync("p2"))
            .ReturnsAsync(2);

        // 残りプレイヤー取得（matchSize=2 なので 1 人必要、バッチ 2 人取得）
        _queueServiceMock.Setup(x => x.DequeueTopPlayersAsync("survival", 1, 2))
            .ReturnsAsync(new[] { "p2" });

        _unityServerAuthApiMock.Setup(x => x.IssueTokenAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new UnityServerAuthResponse { Token = "token", SessionName = "session-001" });

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

        // Assert: 2人分のトークンが発行されたことを確認
        _unityServerAuthApiMock.Verify(
            x => x.IssueTokenAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task ProcessAsync_DoesNotMatch_DifferentMatchSizes()
    {
        // Arrange: stageId=1 キューに matchSize=2 と matchSize=3 のプレイヤーがいる
        _queueServiceMock.Setup(x => x.GetActiveStageKeysAsync("survival"))
            .ReturnsAsync(new[] { "1" });

        var queueCallCount = 0;
        _queueServiceMock.Setup(x => x.GetQueueCountAsync("survival", 1))
            .ReturnsAsync(() =>
            {
                queueCallCount++;
                return queueCallCount == 1 ? 2 : 0;
            });

        _queueServiceMock.Setup(x => x.DequeueTopPlayersAsync("survival", 1, 1))
            .ReturnsAsync(new[] { "p1" });

        _queueServiceMock.Setup(x => x.GetPlayerMatchSizeAsync("p1"))
            .ReturnsAsync(2);
        _queueServiceMock.Setup(x => x.GetPlayerMatchSizeAsync("p2"))
            .ReturnsAsync(3); // 異なる matchSize

        // 残りプレイヤー取得
        _queueServiceMock.Setup(x => x.DequeueTopPlayersAsync("survival", 1, 2))
            .ReturnsAsync(new[] { "p2" });

        // any キューは空
        _queueServiceMock.Setup(x => x.GetQueueCountAsync("survival", 0))
            .ReturnsAsync(0);
        _queueServiceMock.Setup(x => x.DequeueTopPlayersAsync("survival", 0, 2))
            .ReturnsAsync(Array.Empty<string>());

        var processor = CreateProcessor();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act
        await processor.StartAsync(cts.Token);
        await Task.Delay(3000);
        await processor.StopAsync(CancellationToken.None);

        // Assert: マッチ不成立（Game.Server API 未呼び出し）
        _unityServerAuthApiMock.Verify(
            x => x.IssueTokenAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);

        // Assert: プレイヤーが再エンキューされたことを確認
        _queueServiceMock.Verify(
            x => x.EnqueuePlayerAsync(It.IsAny<string>(), "survival", 1, It.IsAny<int>()),
            Times.AtLeast(1));
    }

    [Fact]
    public async Task ProcessAsync_CancellationToken_StopsProcessing()
    {
        // Arrange: キャンセルトークンを即座にキャンセル
        _queueServiceMock.Setup(x => x.GetActiveStageKeysAsync("survival"))
            .ReturnsAsync(Array.Empty<string>());

        var processor = CreateProcessor();
        using var cts = new CancellationTokenSource();

        // Act
        await processor.StartAsync(cts.Token);
        await cts.CancelAsync();
        await processor.StopAsync(CancellationToken.None);

        // Assert: 正常終了する（例外なし）
    }

    [Fact]
    public async Task ProcessAsync_SupplementsFromAnyQueue()
    {
        // Arrange: stageId=1 キューに1人、anyキューに1人 → matchSize=2 で成立
        _queueServiceMock.Setup(x => x.GetActiveStageKeysAsync("survival"))
            .ReturnsAsync(new[] { "1" });

        var queueCallCount = 0;
        _queueServiceMock.Setup(x => x.GetQueueCountAsync("survival", 1))
            .ReturnsAsync(() =>
            {
                queueCallCount++;
                return queueCallCount == 1 ? 1 : 0;
            });

        _queueServiceMock.Setup(x => x.DequeueTopPlayersAsync("survival", 1, 1))
            .ReturnsAsync(new[] { "p1" });

        _queueServiceMock.Setup(x => x.GetPlayerMatchSizeAsync("p1"))
            .ReturnsAsync(2);
        _queueServiceMock.Setup(x => x.GetPlayerMatchSizeAsync("p_any"))
            .ReturnsAsync(2);

        // stageキューから追加取得（空）
        _queueServiceMock.Setup(x => x.DequeueTopPlayersAsync("survival", 1, 2))
            .ReturnsAsync(Array.Empty<string>());

        // anyキュー（stageId=0）から補填
        _queueServiceMock.Setup(x => x.GetQueueCountAsync("survival", 0))
            .ReturnsAsync(1);
        _queueServiceMock.Setup(x => x.DequeueTopPlayersAsync("survival", 0, 2))
            .ReturnsAsync(new[] { "p_any" });

        _unityServerAuthApiMock.Setup(x => x.IssueTokenAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new UnityServerAuthResponse { Token = "token", SessionName = "session-002" });

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

        // Assert: 2人分のトークンが発行（stageキュー1人 + anyキュー1人）
        _unityServerAuthApiMock.Verify(
            x => x.IssueTokenAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Exactly(2));
    }
}
