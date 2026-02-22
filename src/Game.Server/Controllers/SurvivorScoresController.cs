using Game.Library.Shared.Dto;
using Game.Server.Dto.Responses;
using Game.Server.Services.Interfaces;
using Game.Server.Shared.Extensions;
using Game.Server.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Game.Server.Controllers;

[ApiController]
[Route("api/survivor/scores")]
[Authorize]
public class SurvivorScoresController : ControllerBase
{
    private readonly ISurvivorScoreService _scoreService;
    private readonly ISurvivorValidator _survivorValidator;

    public SurvivorScoresController(ISurvivorScoreService scoreService, ISurvivorValidator survivorValidator)
    {
        _scoreService = scoreService;
        _survivorValidator = survivorValidator;
    }

    [HttpPost]
    [ProducesResponseType(typeof(SurvivorScoreSubmitResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SubmitScore([FromBody] ScoreSubmitDto request)
    {
        if (!Guid.TryParse(User.GetUserId(), out var userId))
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
        if (stageId.HasValue)
        {
            _survivorValidator.ValidateStageId(stageId.Value);
        }
        _survivorValidator.ValidateLimit(limit);

        if (!Guid.TryParse(User.GetUserId(), out var userId))
        {
            return Unauthorized();
        }

        var result = await _scoreService.GetUserScoresAsync(userId, stageId, limit);
        return Ok(result);
    }
}
