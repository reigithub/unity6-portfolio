using Game.Server.Tables;
using Game.Server.Repositories.Interfaces;
using Game.Server.Services;
using Game.Server.Tests.Fixtures;
using Moq;

namespace Game.Server.Tests.Services;

public class RankingServiceTests
{
    private readonly Mock<IRankingRepository> _mockRepo;
    private readonly RankingService _service;

    public RankingServiceTests()
    {
        _mockRepo = new Mock<IRankingRepository>();
        _service = new RankingService(_mockRepo.Object);
    }

    [Fact]
    public async Task GetRankingAsync_ReturnsOrderedByScore()
    {
        // Arrange
        var scores = new List<UserScore>
        {
            new() { UserId = TestDataFixture.User2Id, Score = 200, ClearTime = 90f, User = new() { UserId = "pub2", DisplayName = "B" } },
            new() { UserId = TestDataFixture.User1Id, Score = 100, ClearTime = 120f, User = new() { UserId = "pub1", DisplayName = "A" } },
        };
        _mockRepo.Setup(r => r.GetTopScoresAsync("Survivor", 1, 100, 0))
            .ReturnsAsync(scores);

        // Act
        var result = await _service.GetRankingAsync("Survivor", 1, 100, 0);

        // Assert
        Assert.Equal(2, result.Entries.Count);
        Assert.Equal("B", result.Entries[0].DisplayName);
        Assert.Equal(1, result.Entries[0].Rank);
        Assert.Equal("A", result.Entries[1].DisplayName);
        Assert.Equal(2, result.Entries[1].Rank);
    }

    [Fact]
    public async Task GetRankingAsync_EmptyResults_ReturnsEmptyList()
    {
        // Arrange
        _mockRepo.Setup(r => r.GetTopScoresAsync("Survivor", 99, 100, 0))
            .ReturnsAsync(new List<UserScore>());

        // Act
        var result = await _service.GetRankingAsync("Survivor", 99, 100, 0);

        // Assert
        Assert.Empty(result.Entries);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task GetUserRankAsync_ExistingUser_ReturnsCorrectRank()
    {
        // Arrange
        var userId = TestDataFixture.User1Id;
        var bestScore = new UserScore
        {
            UserId = userId,
            Score = 5000,
            ClearTime = 120f,
            User = new() { UserId = "testuser01", DisplayName = "Player1" },
        };
        _mockRepo.Setup(r => r.GetUserBestScoreAsync("Survivor", 1, userId))
            .ReturnsAsync(bestScore);
        _mockRepo.Setup(r => r.GetUserRankAsync("Survivor", 1, userId))
            .ReturnsAsync(2);

        // Act
        var result = await _service.GetUserRankAsync("Survivor", 1, userId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Rank);
        Assert.Equal("Player1", result.DisplayName);
        Assert.Equal(5000, result.Score);
    }

    [Fact]
    public async Task GetUserRankAsync_NonExistentUser_ReturnsNull()
    {
        // Arrange
        var noUserId = Guid.Empty;
        _mockRepo.Setup(r => r.GetUserBestScoreAsync("Survivor", 1, noUserId))
            .ReturnsAsync((UserScore?)null);

        // Act
        var result = await _service.GetUserRankAsync("Survivor", 1, noUserId);

        // Assert
        Assert.Null(result);
    }
}
