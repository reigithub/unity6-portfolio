using Game.Server.Dto.Requests;
using Game.Server.Tables;
using Game.Server.Repositories.Interfaces;
using Game.Server.Services;
using Game.Server.Tests.Fixtures;
using Moq;

namespace Game.Server.Tests.Services;

public class ScoreServiceTests
{
    private static readonly Guid TestUserId = TestDataFixture.User1Id;

    private readonly Mock<IScoreRepository> _mockScoreRepo;
    private readonly Mock<IRankingRepository> _mockRankingRepo;
    private readonly ScoreService _service;

    public ScoreServiceTests()
    {
        _mockScoreRepo = new Mock<IScoreRepository>();
        _mockRankingRepo = new Mock<IRankingRepository>();
        _service = new ScoreService(_mockScoreRepo.Object, _mockRankingRepo.Object);
    }

    [Fact]
    public async Task SubmitScoreAsync_ValidScore_ReturnsSuccess()
    {
        // Arrange
        var request = new SubmitScoreRequest
        {
            StageId = 1,
            Score = 5000,
            ClearTime = 120f,
            GameMode = "Survivor",
            WaveReached = 10,
            EnemiesDefeated = 50,
        };

        _mockRankingRepo.Setup(r => r.GetUserBestScoreAsync("Survivor", 1, TestUserId))
            .ReturnsAsync((UserScore?)null);
        _mockScoreRepo.Setup(r => r.AddAsync(It.IsAny<UserScore>()))
            .ReturnsAsync((UserScore s) => { s.Id = 1; return s; });
        _mockRankingRepo.Setup(r => r.GetUserRankAsync("Survivor", 1, TestUserId))
            .ReturnsAsync(1);

        // Act
        var result = await _service.SubmitScoreAsync(TestUserId, request);

        // Assert
        Dto.Responses.ScoreSubmitResponse? success = null;
        result.Match(
            s => { success = s; return new Microsoft.AspNetCore.Mvc.OkResult(); },
            e => new Microsoft.AspNetCore.Mvc.OkResult());

        Assert.NotNull(success);
        Assert.True(success.IsNewBest);
        Assert.Equal(1, success.CurrentRank);
    }

    [Fact]
    public async Task SubmitScoreAsync_InvalidGameMode_ReturnsBadRequest()
    {
        // Arrange
        var request = new SubmitScoreRequest
        {
            StageId = 1,
            Score = 5000,
            GameMode = "InvalidMode",
        };

        // Act
        var result = await _service.SubmitScoreAsync(TestUserId, request);

        // Assert
        Dto.Responses.ApiError? error = null;
        result.Match(
            s => new Microsoft.AspNetCore.Mvc.OkResult(),
            e => { error = e; return new Microsoft.AspNetCore.Mvc.OkResult(); });

        Assert.NotNull(error);
        Assert.Equal("INVALID_GAME_MODE", error.ErrorCode);
        Assert.Equal(400, error.StatusCode);
    }

    [Fact]
    public async Task SubmitScoreAsync_NotNewBest_SetsIsNewBestFalse()
    {
        // Arrange
        var request = new SubmitScoreRequest
        {
            StageId = 1,
            Score = 3000,
            GameMode = "Survivor",
        };

        var previousBest = new UserScore { Score = 5000 };
        _mockRankingRepo.Setup(r => r.GetUserBestScoreAsync("Survivor", 1, TestUserId))
            .ReturnsAsync(previousBest);
        _mockScoreRepo.Setup(r => r.AddAsync(It.IsAny<UserScore>()))
            .ReturnsAsync((UserScore s) => { s.Id = 2; return s; });
        _mockRankingRepo.Setup(r => r.GetUserRankAsync("Survivor", 1, TestUserId))
            .ReturnsAsync(3);

        // Act
        var result = await _service.SubmitScoreAsync(TestUserId, request);

        // Assert
        Dto.Responses.ScoreSubmitResponse? success = null;
        result.Match(
            s => { success = s; return new Microsoft.AspNetCore.Mvc.OkResult(); },
            e => new Microsoft.AspNetCore.Mvc.OkResult());

        Assert.NotNull(success);
        Assert.False(success.IsNewBest);
    }

    [Fact]
    public async Task GetUserScoresAsync_ReturnsFilteredResults()
    {
        // Arrange
        var scores = new List<UserScore>
        {
            new() { Id = 1, GameMode = "Survivor", StageId = 1, Score = 5000, ClearTime = 120f },
            new() { Id = 2, GameMode = "Survivor", StageId = 1, Score = 3000, ClearTime = 60f },
        };
        _mockScoreRepo.Setup(r => r.GetUserScoresAsync(TestUserId, "Survivor", 1, 50))
            .ReturnsAsync(scores);

        // Act
        var result = await _service.GetUserScoresAsync(TestUserId, "Survivor", 1, 50);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(5000, result[0].Score);
    }
}
