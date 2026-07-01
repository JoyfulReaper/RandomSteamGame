/*
 * Random Steam Game
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using ErrorOr;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RandomSteamGame.Common.Errors;
using RandomSteamGame.Services;
using RandomSteamGame.Services.Interfaces;
using RandomSteamGame.Shared.Contracts;
using System.Diagnostics.CodeAnalysis;

namespace RandomSteamGame.Controllers;


[Route("api/{provider}")]
[AllowAnonymous]
[ApiController]
[EnableRateLimiting("steam_api_limiter")]
public class GameController : ApiController
{
    private readonly GameProviderFactory _factory;
    private readonly IOwnedGamesCacheResetTracker _ownedGamesCacheResetTracker;
    private readonly IAppStatsService _appStatsService;
    private readonly ILogger<GameController> _logger;

    public GameController(
        GameProviderFactory factory,
        IOwnedGamesCacheResetTracker ownedGamesCacheResetTracker,
        IAppStatsService appStatsService,
        ILogger<GameController> logger)
    {
        _factory = factory;
        _ownedGamesCacheResetTracker = ownedGamesCacheResetTracker;
        _appStatsService = appStatsService;
        _logger = logger;
    }

    /// <summary>
    /// Gets the list of owned games for a specific Steam ID.
    /// GET /api/steam/{steamId}/library
    /// </summary>
    [HttpGet("{steamId}/library")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(OwnedGamesResponse))]
    public async Task<IActionResult> GetLibrary(string provider, long steamId)
    {
        if (!TryGetProvider(provider, out var service))
        {
            return Problem([Errors.Steam.UnsupportedProvider(provider)]);
        }

        var result = await service.GetOwnedGamesAsync(steamId);
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
        if (!TryGetProvider(provider, out var service))
        {
            return Problem([Errors.Steam.UnsupportedProvider(provider)]);
        }

        var nextAvailableAt = await _ownedGamesCacheResetTracker.GetNextAvailableAtAsync(userId);
        if (nextAvailableAt is not null)
        {
            return StatusCode(
                StatusCodes.Status429TooManyRequests,
                new ApiProblem
                {
                    Title = "TooManyRequests",
                    Status = StatusCodes.Status429TooManyRequests,
                    Detail = $"Owned games cache can only be reset once every 12 hours. Try again after {nextAvailableAt.Value.ToLocalTime():f}."
                });
        }

        await service.InvalidateOwnedGamesCacheAsync(userId);
        await _ownedGamesCacheResetTracker.MarkResetAsync(userId);

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
        [FromQuery] string? vanityUrl,
        [FromQuery] bool unplayedOnly = false)
    {
        if (!TryGetProvider(provider, out var service))
        {
            return Problem([Errors.Steam.UnsupportedProvider(provider)]);
        }

        var targetId = await ResolveIdentifier(service, userId, vanityUrl);
        if (targetId.IsError)
        {
            return Problem(targetId.Errors);
        }

        var result = await service.GetRandomGameAsync(targetId.Value, unplayedOnly);
        if (result.IsError)
        {
            return Problem(result.Errors);
        }

        await TrackRandomGameGeneratedAsync();
        return Ok(result.Value);
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
        [FromQuery] string? vanityUrl,
        [FromQuery] bool unplayedOnly = false)
    {
        if (!TryGetProvider(provider, out var service))
        {
            return Problem([Errors.Steam.UnsupportedProvider(provider)]);
        }

        var targetId = await ResolveIdentifier(service, userId, vanityUrl);
        if (targetId.IsError)
        {
            return Problem(targetId.Errors);
        }

        var result = await service.GetRandomGameDetailsAsync(targetId.Value, unplayedOnly);
        if (result.IsError)
        {
            return Problem(result.Errors);
        }

        await TrackRandomGameGeneratedAsync();
        return Ok(result.Value);
    }

    /// <summary>
    /// Utility: Resolves a vanity URL to a Steam ID.
    /// GET /api/steam/resolve/{vanityUrl}
    /// </summary>
    [HttpGet("resolve/{vanityUrl}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(long))]
    public async Task<IActionResult> ResolveVanity(string provider, string vanityUrl)
    {
        if (!TryGetProvider(provider, out var service))
        {
            return Problem([Errors.Steam.UnsupportedProvider(provider)]);
        }

        var result = await service.ResolveIdentifierAsync(vanityUrl);
        return result.Match(
            result => Ok(result),
            errors => Problem(errors));
    }

    private bool TryGetProvider(
        string provider,
        [NotNullWhen(true)] out IGameProvider? service)
    {
        return _factory.TryGetProvider(provider, out service);
    }

    private static async Task<ErrorOr<long>> ResolveIdentifier(
        IGameProvider service,
        long? userId,
        string? vanityUrl)
    {
        if (userId.HasValue && !string.IsNullOrWhiteSpace(vanityUrl))
        {
            return Errors.Steam.AmbiguousIdentifier;
        }

        if (!string.IsNullOrWhiteSpace(vanityUrl))
        {
            return await service.ResolveIdentifierAsync(vanityUrl);
        }

        if (userId is null)
        {
            return Errors.Steam.IdentifierRequired;
        }

        return userId.Value;
    }

    private async Task TrackRandomGameGeneratedAsync()
    {
        try
        {
            await _appStatsService.IncrementRandomGamesGeneratedAsync();
        }
        catch (Exception ex)
        {
            // This should never block game generation.
            _logger.LogWarning(ex, "Failed to increment random games generated counter.");
        }
    }

}
