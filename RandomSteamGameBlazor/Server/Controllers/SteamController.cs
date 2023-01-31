using Mapster;
using MapsterMapper;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RandomSteamGameBlazor.Server.Features.Steam.Queries.OwnedGames;
using RandomSteamGameBlazor.Server.Features.Steam.Queries.RandomGame;
using RandomSteamGameBlazor.Server.Features.Steam.Queries.ResolveVantiy;
using RandomSteamGameBlazor.Shared.Contracts.RandomSteamGame;
using RandomSteamGameBlazor.Shared.Contracts.Steam;

namespace RandomSteamGameBlazor.Server.Controllers;

[Route("api/[controller]")]
[AllowAnonymous]
[ApiController]
public class SteamController : ApiController
{
    private readonly ISender _mediator;
    private readonly IMapper _mapper;

    public SteamController(
        ISender mediator,
        IMapper mapper)
    {
        _mediator = mediator;
        _mapper = mapper;
    }

    [HttpGet("OwnedGames/{steamId}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(OwnedGamesResponse))]
    public async Task<IActionResult> GetOwnedGames(long steamId)
    {
        var query = new OwnedGamesQuery(steamId);
        var result = await _mediator.Send(query);

        return result.Match(
            result => Ok(result),
            errors => Problem(errors));
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