using Mapster;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RandomSteamGameBlazor.Server.Features.Steam.Queries.RandomGame;
using RandomSteamGameBlazor.Server.Features.Steam.Queries.ResolveVantiy;
using RandomSteamGameBlazor.Shared.Contracts.SteamApiContracts;

namespace RandomSteamGameBlazor.Server.Controllers;

[Route("api/[controller]")]
[AllowAnonymous]
[ApiController]
public class SteamController : ApiController
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

        return result.Match(
            result => Ok(result.Adapt<AppData>()),
            errors => Problem(errors));
    }

    [HttpGet("RandomGameByVanityUrl/{vanityUrl}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AppData))]
    public async Task<IActionResult> RandomGame(string vanityUrl)
    {
        var steamId = await _mediator.Send(new ResolveVanityQuery(vanityUrl));
        if (steamId.IsError)
        {
            return Problem(steamId.Errors);
        }

        var query = new RandomGameQuery(steamId.Value);
        var result = await _mediator.Send(query);

        return result.Match(
            result => Ok(result.Adapt<AppData>()),
            errors => Problem(errors));
    }

    [HttpGet("ResolveVanityUrl/{vanityUrl}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(long))]
    public async Task<IActionResult> ResolveVanityUrl(string vanityUrl)
    {
        var steamId = await _mediator.Send(new ResolveVanityQuery(vanityUrl));

        return steamId.Match(
            steamId => Ok(steamId),
            errors => Problem(errors));
    }
}