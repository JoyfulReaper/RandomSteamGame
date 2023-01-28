using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RandomSteamGameBlazor.Server.Steam.Queries.RandomGame;

namespace RandomSteamGameBlazor.Server.Controllers;

[Route("api/[controller]")]
[ApiController]
public class SteamController : ControllerBase
{
    private readonly ISender _mediator;

    public SteamController(ISender mediator)
    {
        _mediator = mediator;
    }
    
    [HttpGet("RandomGame/{steamId}")]
    public async Task<IActionResult> RandomGame(long steamId)
    {
        var query = new RandomGameQuery(steamId);
        var result = await _mediator.Send(query);
        
        return Ok(result);
    }
}
