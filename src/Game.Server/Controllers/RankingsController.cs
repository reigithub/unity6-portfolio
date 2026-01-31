using System.Security.Claims;
using Game.Server.Dto.Responses;
using Game.Server.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Game.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RankingsController : ControllerBase
{
    private readonly IRankingService _rankingService;

    public RankingsController(IRankingService rankingService)
    {
        _rankingService = rankingService;
    }

    [HttpGet("{gameMode}/{stageId}")]
    [ProducesResponseType(typeof(RankingResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRanking(
        string gameMode,
        int stageId,
        [FromQuery] int limit = 100,
        [FromQuery] int offset = 0)
    {
        var result = await _rankingService.GetRankingAsync(gameMode, stageId, limit, offset);
        return Ok(result);
    }

    [HttpGet("{gameMode}/{stageId}/me")]
    [Authorize]
    [ProducesResponseType(typeof(RankingEntryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyRank(string gameMode, int stageId)
    {
        string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var result = await _rankingService.GetUserRankAsync(gameMode, stageId, userId);
        return result != null ? Ok(result) : NotFound();
    }
}
