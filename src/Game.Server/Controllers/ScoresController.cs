using System.Security.Claims;
using Game.Server.Dto.Requests;
using Game.Server.Dto.Responses;
using Game.Server.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Game.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ScoresController : ControllerBase
{
    private readonly IScoreService _scoreService;

    public ScoresController(IScoreService scoreService)
    {
        _scoreService = scoreService;
    }

    [HttpPost]
    [ProducesResponseType(typeof(ScoreSubmitResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SubmitScore([FromBody] SubmitScoreRequest request)
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
    [ProducesResponseType(typeof(List<ScoreHistoryEntry>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyScores(
        [FromQuery] string? gameMode = null,
        [FromQuery] int? stageId = null,
        [FromQuery] int limit = 50)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Unauthorized();
        }

        var result = await _scoreService.GetUserScoresAsync(userId, gameMode, stageId, limit);
        return Ok(result);
    }
}
