using System.Security.Claims;
using Game.Library.Shared.Dto;
using Game.Server.Controllers;
using Game.Server.Services.Interfaces;
using Game.Server.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Game.Server.Tests.Controllers;

public class RankingsControllerTests
{
    private readonly Mock<IRankingService> _rankingServiceMock;
    private readonly Mock<ISurvivorValidator> _validatorMock;
    private readonly RankingsController _controller;
    private readonly Guid _testUserId = Guid.NewGuid();

    public RankingsControllerTests()
    {
        _rankingServiceMock = new Mock<IRankingService>();
        _validatorMock = new Mock<ISurvivorValidator>();

        _controller = new RankingsController(
            _rankingServiceMock.Object,
            _validatorMock.Object);

        var claims = new[] { new Claim("sub", _testUserId.ToString()) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal },
        };
    }

    [Fact]
    public async Task GetMyRank_ReturnsOk_WhenRankExists()
    {
        // Arrange
        var entry = new RankingEntryDto
        {
            Rank = 5,
            UserId = _testUserId.ToString(),
            UserName = "TestUser",
            Score = 10000,
        };
        _rankingServiceMock.Setup(x => x.GetUserRankAsync(1, _testUserId))
            .ReturnsAsync(entry);

        // Act
        var result = await _controller.GetMyRank(1);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var data = Assert.IsType<RankingEntryDto>(okResult.Value);
        Assert.Equal(5, data.Rank);
    }

    [Fact]
    public async Task GetMyRank_ReturnsNotFound_WhenNoRankData()
    {
        // Arrange
        _rankingServiceMock.Setup(x => x.GetUserRankAsync(1, _testUserId))
            .ReturnsAsync((RankingEntryDto?)null);

        // Act
        var result = await _controller.GetMyRank(1);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }
}
