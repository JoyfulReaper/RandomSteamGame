/*
 * Random Steam Game
 * * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RandomSteamGame.Services.Interfaces;
using RandomSteamGame.Shared.Contracts;

namespace RandomSteamGame.Controllers;

[Route("api/[controller]")]
[AllowAnonymous]
[ApiController]
[EnableRateLimiting("steam_api_limiter")]
public class SteamController : ApiController
{
    private readonly ISteamService _steamService;

    public SteamController(ISteamService steamService)
    {
        _steamService = steamService;
    }

    [HttpGet("OwnedGames/{steamId}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(OwnedGamesResponse))]
    public async Task<IActionResult> GetOwnedGames(long steamId)
    {
        var result = await _steamService.GetOwnedGamesAsync(steamId);

        return result.Match(
            result => Ok(result),
            errors => Problem(errors));
    }

    [HttpGet("RandomSteamGame/{steamId}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(RandomGameResponse))]
    public async Task<IActionResult> RandomSteamGame(long steamId)
    {
        var result = await _steamService.GetRandomSteamGameAsync(steamId);

        return result.Match(
            result => Ok(result),
            errors => Problem(errors));
    }

    [HttpGet("RandomGameBySteamId/{steamId}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GameDetails))]
    public async Task<IActionResult> RandomGameBySteamId(long steamId)
    {
        var result = await _steamService.GetRandomGameBySteamIdAsync(steamId);

        return result.Match(
            result => Ok(result),
            errors => Problem(errors));
    }

    [HttpGet("RandomGameByVanityUrl/{vanityUrl}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GameDetails))]
    public async Task<IActionResult> RandomGameByVanityUrl(string vanityUrl)
    {
        var steamIdResult = await _steamService.ResolveVanityUrlAsync(vanityUrl);
        if (steamIdResult.IsError)
        {
            return Problem(steamIdResult.Errors);
        }

        var result = await _steamService.GetRandomGameBySteamIdAsync(steamIdResult.Value);

        return result.Match(
            result => Ok(result),
            errors => Problem(errors));
    }

    [HttpGet("ResolveVanityUrl/{vanityUrl}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(long))]
    public async Task<IActionResult> ResolveVanityUrl(string vanityUrl)
    {
        var result = await _steamService.ResolveVanityUrlAsync(vanityUrl);

        return result.Match(
            result => Ok(result),
            errors => Problem(errors));
    }
}