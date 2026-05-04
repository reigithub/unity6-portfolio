using System.Security.Claims;
using Game.Library.Shared.Dto;
using Game.Server.Controllers;
using Game.Server.Services.Interfaces;
using Game.Server.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Game.Server.Tests.Controllers;

public class SurvivorScoresControllerTests
{
    private readonly Mock<ISurvivorScoreService> _scoreServiceMock;
    private readonly Mock<ISurvivorValidator> _validatorMock;
    private readonly SurvivorScoresController _controller;
    private readonly string _testUserIdString = "111122223333";

    public SurvivorScoresControllerTests()
    {
        _scoreServiceMock = new Mock<ISurvivorScoreService>();
        _validatorMock = new Mock<ISurvivorValidator>();

        _controller = new SurvivorScoresController(
            _scoreServiceMock.Object,
            _validatorMock.Object);

        var claims = new[] { new Claim("sub", _testUserIdString) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal },
        };
    }

    [Fact]
    public async Task GetMyScores_ReturnsOk_WithScoreList()
    {
        // Arrange
        var scores = new List<SurvivorScoreHistoryEntry>
        {
            new() { Score = 5000, ClearTime = 60f, WaveReached = 2 },
            new() { Score = 8000, ClearTime = 90f, WaveReached = 3 },
        };
        _scoreServiceMock.Setup(x => x.GetUserScoresAsync(_testUserIdString, 1, 50))
            .ReturnsAsync(scores);

        // Act
        var result = await _controller.GetMyScores(stageId: 1, limit: 50);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var data = Assert.IsAssignableFrom<List<SurvivorScoreHistoryEntry>>(okResult.Value);
        Assert.Equal(2, data.Count);
    }

    [Fact]
    public async Task GetMyScores_ReturnsOk_WithNoStageFilter()
    {
        // Arrange
        _scoreServiceMock.Setup(x => x.GetUserScoresAsync(_testUserIdString, null, 50))
            .ReturnsAsync(new List<SurvivorScoreHistoryEntry>());

        // Act
        var result = await _controller.GetMyScores(stageId: null, limit: 50);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);

        // stageId=null の場合は ValidateStageId が呼ばれないことを検証
        _validatorMock.Verify(v => v.ValidateStageId(It.IsAny<int>()), Times.Never);
    }
}
