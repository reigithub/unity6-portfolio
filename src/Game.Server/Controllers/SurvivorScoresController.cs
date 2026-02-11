using System.Security.Claims;
using Game.Server.Dto.Requests;
using Game.Server.Dto.Responses;
using Game.Server.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Game.Server.Controllers;

[ApiController]
[Route("api/survivor/scores")]
[Authorize]
public class SurvivorScoresController : ControllerBase
{
    private readonly ISurvivorScoreService _scoreService;

    public SurvivorScoresController(ISurvivorScoreService scoreService)
    {
        _scoreService = scoreService;
    }

    [HttpPost]
    [ProducesResponseType(typeof(SurvivorScoreSubmitResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SubmitScore([FromBody] SubmitSurvivorScoreRequest request)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Unauthorized();
        }

        var result = await _scoreService.SubmitScoreAsync(userId, request);

        return result.Match(
            success => StatusCode(StatusCodes.Status201Created, success),
            error => error.ToActionResult());
    }

    [HttpGet("me")]
    [ProducesResponseType(typeof(List<SurvivorScoreHistoryEntry>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyScores(
        [FromQuery] int? stageId = null,
        [FromQuery] int limit = 50)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Unauthorized();
        }

        var result = await _scoreService.GetUserScoresAsync(userId, stageId, limit);
        return Ok(result);
    }
}
