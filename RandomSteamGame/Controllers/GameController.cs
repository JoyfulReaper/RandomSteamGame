/*
 * Random Steam Game
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RandomSteamGame.Services;
using RandomSteamGame.Services.Interfaces;
using RandomSteamGame.Shared.Contracts;

namespace RandomSteamGame.Controllers;


[Route("api/{provider}")]
[AllowAnonymous]
[ApiController]
[EnableRateLimiting("steam_api_limiter")]
public class GameController : ApiController
{
    private readonly GameProviderFactory _factory;

    public GameController(GameProviderFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Gets the list of owned games for a specific Steam ID.
    /// GET /api/steam/{steamId}/library
    /// </summary>
    [HttpGet("{steamId}/library")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(OwnedGamesResponse))]
    public async Task<IActionResult> GetLibrary(string provider, long userId)
    {
        var service = _factory.GetProvider(provider);
        var result = await service.GetOwnedGamesAsync(userId);
        return result.Match(Ok, Problem);
    }

    /// <summary>
    /// Invalidates the cached owned games for a user.
    /// POST /api/steam/{userId}/library/refresh
    /// </summary>
    [HttpPost("{userId}/library/refresh")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RefreshLibrary(string provider, long userId)
    {
        var service = _factory.GetProvider(provider);

        await service.InvalidateOwnedGamesCacheAsync(userId);

        return NoContent();
    }

    /// <summary>
    /// Gets a random game and full details for a user.
    /// GET /api/steam/random-game?steamId=... OR ?vanityUrl=...
    /// </summary>
    [HttpGet("random-game")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(RandomGameResponse))]
    public async Task<IActionResult> GetRandomGame(
        string provider,
        [FromQuery] long? userId,
        [FromQuery] string? vanityUrl)
    {
        var service = _factory.GetProvider(provider);

        var targetId = await ResolveIdentifier(service, userId, vanityUrl);
        if (targetId is null) return Problem();

        var result = await service.GetRandomGameAsync(targetId.Value);
        return result.Match(Ok, Problem);
    }

    /// <summary>
    /// Gets simplified game details for a random game.
    /// GET /api/steam/random-game/details?steamId=... OR ?vanityUrl=...
    /// </summary>
    [HttpGet("random-game/details")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GameDetails))]
    public async Task<IActionResult> GetRandomGameDetails(
        string provider,
        [FromQuery] long? userId,
        [FromQuery] string? vanityUrl)
    {
        var service = _factory.GetProvider(provider);

        var targetId = await ResolveIdentifier(service, userId, vanityUrl);
        if (targetId is null) return Problem();

        var result = await service.GetRandomGameDetailsAsync(targetId.Value);
        return result.Match(Ok, Problem);
    }

    /// <summary>
    /// Utility: Resolves a vanity URL to a Steam ID.
    /// GET /api/steam/resolve/{vanityUrl}
    /// </summary>
    [HttpGet("resolve/{vanityUrl}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(long))]
    public async Task<IActionResult> ResolveVanity(string provider, string vanityUrl)
    {
        var service = _factory.GetProvider(provider);
        var result = await service.ResolveIdentifierAsync(vanityUrl);
        return result.Match(
            result => Ok(result),
            errors => Problem(errors));
    }

    private async Task<long?> ResolveIdentifier(IGameProvider service, long? userId, string? vanityUrl)
    {
        if (userId.HasValue && !string.IsNullOrEmpty(vanityUrl)) return null;
        if (vanityUrl != null)
        {
            var result = await service.ResolveIdentifierAsync(vanityUrl);
            return result.IsError ? null : result.Value;
        }
        return userId;
    }
}