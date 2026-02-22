using Game.Library.Shared.Dto;
using Game.Server.Tables;
using Game.Server.Repositories.Interfaces;
using Game.Server.Services;
using Game.Server.Services.Interfaces;
using Game.Server.Tests.Fixtures;
using Medallion.Threading;
using Microsoft.Extensions.Logging;
using Moq;

namespace Game.Server.Tests.Services;

public class RankingServiceTests
{
    private readonly Mock<IRankingRepository> _mockRepo;
    private readonly Mock<ISurvivorRankingCacheService> _mockCacheService;
    private readonly Mock<IDistributedLockProvider> _mockLockProvider;
    private readonly Mock<ILogger<RankingService>> _mockLogger;
    private readonly RankingService _service;

    public RankingServiceTests()
    {
        _mockRepo = new Mock<IRankingRepository>();
        _mockCacheService = new Mock<ISurvivorRankingCacheService>();
        _mockLockProvider = new Mock<IDistributedLockProvider>();
        _mockLogger = new Mock<ILogger<RankingService>>();

        // ロックは常に成功（テスト環境ではレースコンディションなし）
        var lockMock = new Mock<IDistributedLock>();
        lockMock.Setup(x => x.AcquireAsync(It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<IDistributedSynchronizationHandle>());
        _mockLockProvider.Setup(x => x.CreateLock(It.IsAny<string>()))
            .Returns(lockMock.Object);

        _service = new RankingService(
            _mockRepo.Object,
            _mockCacheService.Object,
            _mockLockProvider.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task GetRankingAsync_ReturnsOrderedByScore()
    {
        // Arrange
        var scores = new List<SurvivorScore>
        {
            new() { UserId = TestDataFixture.User2Id, Score = 200, ClearTime = 90f, User = new() { UserId = "pub2", UserName = "B" } },
            new() { UserId = TestDataFixture.User1Id, Score = 100, ClearTime = 120f, User = new() { UserId = "pub1", UserName = "A" } },
        };
        _mockRepo.Setup(r => r.GetTopScoresAsync(1, 100, 0))
            .ReturnsAsync(scores);

        // Act
        var result = await _service.GetRankingAsync(1, 100, 0);

        // Assert
        Assert.Equal(2, result.Entries.Count);
        Assert.Equal("B", result.Entries[0].UserName);
        Assert.Equal(1, result.Entries[0].Rank);
        Assert.Equal("A", result.Entries[1].UserName);
        Assert.Equal(2, result.Entries[1].Rank);
    }

    [Fact]
    public async Task GetRankingAsync_EmptyResults_ReturnsEmptyList()
    {
        // Arrange
        _mockRepo.Setup(r => r.GetTopScoresAsync(99, 100, 0))
            .ReturnsAsync(new List<SurvivorScore>());

        // Act
        var result = await _service.GetRankingAsync(99, 100, 0);

        // Assert
        Assert.Empty(result.Entries);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task GetUserRankAsync_ExistingUser_ReturnsCorrectRank()
    {
        // Arrange
        var userId = TestDataFixture.User1Id;
        var bestScore = new SurvivorScore
        {
            UserId = userId,
            Score = 5000,
            ClearTime = 120f,
            User = new() { UserId = "000000000001", UserName = "Player1" },
        };
        _mockRepo.Setup(r => r.GetUserBestScoreAsync(1, userId))
            .ReturnsAsync(bestScore);
        _mockRepo.Setup(r => r.GetUserRankAsync(1, userId))
            .ReturnsAsync(2);

        // Act
        var result = await _service.GetUserRankAsync(1, userId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Rank);
        Assert.Equal("Player1", result.UserName);
        Assert.Equal(5000, result.Score);
    }

    [Fact]
    public async Task GetUserRankAsync_NonExistentUser_ReturnsNull()
    {
        // Arrange
        var noUserId = Guid.Empty;
        _mockRepo.Setup(r => r.GetUserBestScoreAsync(1, noUserId))
            .ReturnsAsync((SurvivorScore?)null);

        // Act
        var result = await _service.GetUserRankAsync(1, noUserId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetRankingAsync_CacheMiss_AcquiresLockAndRechecksCache()
    {
        // Arrange: 最初のキャッシュチェックはmiss、ロック取得後の2回目はhit
        var cachedEntries = new List<RankingEntryDto>
        {
            new() { Rank = 1, UserId = "pub1", UserName = "A", Score = 100 },
        };

        var callCount = 0;
        _mockCacheService.Setup(c => c.GetRankingAsync(1, 100, 0))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1 ? null : cachedEntries;
            });

        // Act
        var result = await _service.GetRankingAsync(1, 100, 0);

        // Assert: DBには問い合わせないはず（ロック取得後のキャッシュ再チェックでhit）
        _mockRepo.Verify(r => r.GetTopScoresAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        Assert.Single(result.Entries);
        Assert.Equal("A", result.Entries[0].UserName);
    }

    [Fact]
    public async Task GetRankingAsync_CacheMiss_FetchesFromDbWhenCacheStillEmpty()
    {
        // Arrange: 両方のキャッシュチェックがmiss
        _mockCacheService.Setup(c => c.GetRankingAsync(1, 100, 0))
            .ReturnsAsync((List<RankingEntryDto>?)null);

        var scores = new List<SurvivorScore>
        {
            new() { UserId = TestDataFixture.User1Id, Score = 100, ClearTime = 120f, User = new() { UserId = "pub1", UserName = "A" } },
        };
        _mockRepo.Setup(r => r.GetTopScoresAsync(1, 100, 0))
            .ReturnsAsync(scores);

        // Act
        var result = await _service.GetRankingAsync(1, 100, 0);

        // Assert: DBから取得してキャッシュに保存
        _mockRepo.Verify(r => r.GetTopScoresAsync(1, 100, 0), Times.Once);
        _mockCacheService.Verify(c => c.SetRankingAsync(1, It.IsAny<List<RankingEntryDto>>(), It.IsAny<TimeSpan?>()), Times.Once);
        Assert.Single(result.Entries);
    }
}
