using Mapster;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using RandomSteamGameBlazor.Server.Steam.Queries.RandomGame;
using RandomSteamGameBlazor.Server.Steam.Queries.ResolveVantiy;
using RandomSteamGameBlazor.Shared.Contracts.SteamApiContracts;

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
    
    [HttpGet("RandomGameBySteamId/{steamId}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AppData))]
    public async Task<IActionResult> RandomGame(long steamId)
    {
        var query = new RandomGameQuery(steamId);
        var result = await _mediator.Send(query);
        
        return Ok(result.Adapt<AppData>());
    }

    [HttpGet("RandomGameByVanityUrl/{vanityUrl}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AppData))]
    public async Task<IActionResult> RandomGame(string vanityUrl)
    {
        var steamId = await _mediator.Send(new ResolveVanityQuery(vanityUrl));
        var query = new RandomGameQuery(steamId);
        var result = await _mediator.Send(query);

        return Ok(result.Adapt<AppData>());
    }

    [HttpGet("ResolveVanityUrl/{vanityUrl}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(long))]
    public async Task<IActionResult> ResolveVanityUrl(string vanityUrl)
    {
        var steamId = await _mediator.Send(new ResolveVanityQuery(vanityUrl));
        
        return Ok(steamId);
    }
}
