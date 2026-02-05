using Microsoft.AspNetCore.Mvc;

namespace Game.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new { status = "healthy", timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });
    }
}
