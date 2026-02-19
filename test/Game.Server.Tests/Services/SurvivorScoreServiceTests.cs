using Game.Library.Shared.Dto;
using Game.Server.Tables;
using Game.Server.Repositories.Interfaces;
using Game.Server.Services;
using Game.Server.Services.Interfaces;
using Game.Server.Tests.Fixtures;
using Game.Server.Validation;
using Moq;

namespace Game.Server.Tests.Services;

public class SurvivorScoreServiceTests
{
    private static readonly Guid TestUserId = TestDataFixture.User1Id;

    private readonly Mock<ISurvivorScoreRepository> _mockScoreRepo;
    private readonly Mock<IRankingRepository> _mockRankingRepo;
    private readonly Mock<IRankingService> _mockRankingService;
    private readonly Mock<ISurvivorScoreValidator> _mockScoreValidator;
    private readonly SurvivorScoreService _service;

    public SurvivorScoreServiceTests()
    {
        _mockScoreRepo = new Mock<ISurvivorScoreRepository>();
        _mockRankingRepo = new Mock<IRankingRepository>();
        _mockRankingService = new Mock<IRankingService>();
        _mockScoreValidator = new Mock<ISurvivorScoreValidator>();

        _service = new SurvivorScoreService(
            _mockScoreRepo.Object,
            _mockRankingRepo.Object,
            _mockRankingService.Object,
            _mockScoreValidator.Object);
    }

    [Fact]
    public async Task SubmitScoreAsync_ValidScore_ReturnsSuccess()
    {
        // Arrange
        var request = new ScoreSubmitDto
        {
            StageId = 1,
            Score = 5000,
            ClearTime = 120f,
            WaveReached = 10,
            EnemiesDefeated = 50,
        };

        _mockRankingRepo.Setup(r => r.GetUserBestScoreAsync(1, TestUserId))
            .ReturnsAsync((SurvivorScore?)null);
        _mockScoreRepo.Setup(r => r.AddAsync(It.IsAny<SurvivorScore>()))
            .ReturnsAsync((SurvivorScore s) => { s.Id = 1; return s; });
        _mockRankingRepo.Setup(r => r.GetUserRankAsync(1, TestUserId))
            .ReturnsAsync(1);

        // Act
        var result = await _service.SubmitScoreAsync(TestUserId, request);

        // Assert
        SurvivorScoreSubmitResponse? success = null;
        result.Match(
            s => { success = s; return new Microsoft.AspNetCore.Mvc.OkResult(); },
            e => new Microsoft.AspNetCore.Mvc.OkResult());

        Assert.NotNull(success);
        Assert.True(success.IsNewBest);
        Assert.Equal(1, success.CurrentRank);
    }

    [Fact]
    public async Task SubmitScoreAsync_NotNewBest_SetsIsNewBestFalse()
    {
        // Arrange
        var request = new ScoreSubmitDto
        {
            StageId = 1,
            Score = 3000,
        };

        var previousBest = new SurvivorScore { Score = 5000 };
        _mockRankingRepo.Setup(r => r.GetUserBestScoreAsync(1, TestUserId))
            .ReturnsAsync(previousBest);
        _mockScoreRepo.Setup(r => r.AddAsync(It.IsAny<SurvivorScore>()))
            .ReturnsAsync((SurvivorScore s) => { s.Id = 2; return s; });
        _mockRankingRepo.Setup(r => r.GetUserRankAsync(1, TestUserId))
            .ReturnsAsync(3);

        // Act
        var result = await _service.SubmitScoreAsync(TestUserId, request);

        // Assert
        SurvivorScoreSubmitResponse? success = null;
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
        var scores = new List<SurvivorScore>
        {
            new() { Id = 1, StageId = 1, Score = 5000, ClearTime = 120f },
            new() { Id = 2, StageId = 1, Score = 3000, ClearTime = 60f },
        };
        _mockScoreRepo.Setup(r => r.GetUserScoresAsync(TestUserId, 1, 50))
            .ReturnsAsync(scores);

        // Act
        var result = await _service.GetUserScoresAsync(TestUserId, 1, 50);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(5000, result[0].Score);
    }
}
