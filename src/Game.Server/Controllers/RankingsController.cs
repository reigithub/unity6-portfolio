using Game.Library.Shared.Dto;
using Game.Server.Dto.Responses;
using Game.Server.Services.Interfaces;
using Game.Server.Shared.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Game.Server.Controllers;

[ApiController]
[Route("api/survivor/rankings")]
public class RankingsController : ControllerBase
{
    private readonly IRankingService _rankingService;

    public RankingsController(IRankingService rankingService)
    {
        _rankingService = rankingService;
    }

    [HttpGet("{stageId:int}")]
    [ProducesResponseType(typeof(RankingResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRanking(
        int stageId,
        [FromQuery] int limit = 100,
        [FromQuery] int offset = 0)
    {
        var result = await _rankingService.GetRankingAsync(stageId, limit, offset);
        return Ok(result);
    }

    [HttpGet("{stageId:int}/me")]
    [Authorize]
    [ProducesResponseType(typeof(RankingEntryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyRank(int stageId)
    {
        if (!Guid.TryParse(User.GetUserId(), out var userId))
        {
            return Unauthorized();
        }

        var result = await _rankingService.GetUserRankAsync(stageId, userId);
        return result != null ? Ok(result) : NotFound();
    }
}
