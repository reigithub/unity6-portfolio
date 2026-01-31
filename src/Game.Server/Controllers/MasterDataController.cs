using Game.Library.Shared.Dto;
using Game.Server.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Game.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[ResponseCache(Duration = 3600)]
public class MasterDataController : ControllerBase
{
    private readonly IMasterDataService _masterDataService;

    public MasterDataController(IMasterDataService masterDataService)
    {
        _masterDataService = masterDataService;
    }

    [HttpGet("version")]
    [ProducesResponseType(typeof(MasterDataVersionDto), StatusCodes.Status200OK)]
    public IActionResult GetVersion()
    {
        var version = _masterDataService.GetCurrentVersion();
        return Ok(version);
    }

    [HttpGet("{tableName}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetTable(
        string tableName,
        [FromHeader(Name = "If-None-Match")] string? etag = null)
    {
        string? currentEtag = _masterDataService.GetTableEtag(tableName);
        if (currentEtag == null)
        {
            return NotFound();
        }

        if (!string.IsNullOrEmpty(etag) && etag == currentEtag)
        {
            return StatusCode(StatusCodes.Status304NotModified);
        }

        byte[]? data = _masterDataService.GetTableBinary(tableName);
        if (data == null)
        {
            return NotFound();
        }

        Response.Headers.ETag = currentEtag;
        return File(data, "application/x-msgpack");
    }
}
