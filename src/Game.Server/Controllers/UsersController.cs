using Game.Library.Shared.Dto;
using Game.Server.Dto.Responses;
using Game.Server.Services.Interfaces;
using Game.Server.Shared.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Game.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet("me")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMe()
    {
        if (!Guid.TryParse(User.GetUserId(), out var userId))
        {
            return Unauthorized();
        }

        var user = await _userService.GetUserAsync(userId);
        return user != null ? Ok(user) : NotFound();
    }

    [HttpPut("me")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateUserRequest request)
    {
        if (!Guid.TryParse(User.GetUserId(), out var userId))
        {
            return Unauthorized();
        }

        var result = await _userService.UpdateUserAsync(userId, request);

        return result.Match(
            success => Ok(success),
            error => error.ToActionResult());
    }
}
