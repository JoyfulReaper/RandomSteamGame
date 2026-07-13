/*
 * Random Steam Game
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using ErrorOr;
using JoyfulReaperLib.MissionControl;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using RandomSteamGame.Common.Errors;
using RandomSteamGame.Events;
using RandomSteamGame.Options;
using RandomSteamGame.Services;
using RandomSteamGame.Services.Interfaces;
using RandomSteamGame.Shared.Contracts;
using SteamApiClient;
using SteamApiClient.Services;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace RandomSteamGame.Controllers;


[Route("api/{provider}")]
[AllowAnonymous]
[ApiController]
[EnableRateLimiting("steam_api_limiter")]
public class GameController : ApiController
{
    private const long MinSteamId = 10_000_000_000_000_000L;
    private const long MaxSteamId = 99_999_999_999_999_999L;

    private readonly GameProviderFactory _factory;
    private readonly IOwnedGamesCacheResetTracker _ownedGamesCacheResetTracker;
    private readonly IAppStatsService _appStatsService;
    private readonly ISteamLibraryExportService _steamLibraryExportService;
    private readonly IMissionControlClient _missionControlClient;
    private readonly ApplicationOptions _applicationOptions;
    private readonly ILogger<GameController> _logger;

    public GameController(
        GameProviderFactory factory,
        IOwnedGamesCacheResetTracker ownedGamesCacheResetTracker,
        IAppStatsService appStatsService,
        ISteamLibraryExportService steamLibraryExportService,
        IMissionControlClient missionControlClient,
        IOptions<ApplicationOptions> applicationOptions,
        ILogger<GameController> logger)
    {
        _missionControlClient = missionControlClient;
        _applicationOptions = applicationOptions.Value;
        _factory = factory;
        _ownedGamesCacheResetTracker = ownedGamesCacheResetTracker;
        _appStatsService = appStatsService;
        _steamLibraryExportService = steamLibraryExportService;
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

        if (!IsValidSteamId(steamId))
        {
            return Problem([Errors.Steam.InvalidSteamId]);
        }

        var result = await service.GetOwnedGamesAsync(steamId);
        return result.Match(Ok, Problem);
    }

    /// <summary>
    /// Exports the list of owned games for a specific Steam ID as CSV.
    /// GET /api/steam/{steamId}/library/export.csv
    /// </summary>
    [HttpGet("{steamId:long}/library/export.csv")]
    [Produces("text/csv")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportLibrary(string provider, long steamId)
    {
        if (!TryGetProvider(provider, out var service))
        {
            return Problem([Errors.Steam.UnsupportedProvider(provider)]);
        }

        if (!IsValidSteamId(steamId))
        {
            return Problem([Errors.Steam.InvalidSteamId]);
        }

        var result = await service.GetOwnedGamesAsync(steamId);
        if (result.IsError)
        {
            return Problem(result.Errors);
        }

        var csvBytes = _steamLibraryExportService.Export(result.Value);
        return File(csvBytes, "text/csv; charset=utf-8", $"steam-library-{steamId}.csv");
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

        if (!IsValidSteamId(userId))
        {
            return Problem([Errors.Steam.InvalidSteamId]);
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
    /// Gets simplified game details for a random game.
    /// GET /api/steam/random-game/details?userId=... OR ?vanityUrl=...
    /// </summary>
    [HttpGet("random-game/details")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GameDetails))]
    public async Task<IActionResult> GetRandomGameDetails(
        string provider,
        [FromQuery] long? userId,
        [FromQuery] string? vanityUrl,
        [FromQuery] bool unplayedOnly = false)
    {
        var occurredAt = DateTimeOffset.UtcNow;
        var correlationId = Guid.NewGuid().ToString("N");
        var stopwatch = Stopwatch.StartNew();

        if (!TryGetProvider(provider, out var service))
        {
            await PublishGamePickEventAsync(
                provider,
                telemetry: null,
                unplayedOnly,
                stopwatch,
                outcome: "unsupported-provider",
                succeeded: false,
                occurredAt,
                correlationId,
                identifierResolutionMilliseconds: 0);

            return Problem([Errors.Steam.UnsupportedProvider(provider)]);
        }

        var identifierValidation = ValidateIdentifier(userId, vanityUrl);
        if (identifierValidation is not null)
        {
            await PublishGamePickEventAsync(
                provider,
                telemetry: null,
                unplayedOnly,
                stopwatch,
                outcome: "invalid-identifier",
                succeeded: false,
                occurredAt,
                correlationId,
                identifierResolutionMilliseconds: 0);

            return Problem([identifierValidation.Value]);
        }

        var identifierStopwatch = Stopwatch.StartNew();
        var targetId = await ResolveIdentifier(service, userId, vanityUrl);
        identifierStopwatch.Stop();
        if (targetId.IsError)
        {
            await PublishGamePickEventAsync(
                provider,
                telemetry: null,
                unplayedOnly,
                stopwatch,
                outcome: "identifier-resolution-failed",
                succeeded: false,
                occurredAt,
                correlationId,
                identifierResolutionMilliseconds: identifierStopwatch.ElapsedMilliseconds);

            return Problem(targetId.Errors);
        }

        var result = await service.GetRandomGamePickAsync(targetId.Value, unplayedOnly);
        if (!result.Succeeded)
        {
            await PublishGamePickEventAsync(
                provider,
                telemetry: result,
                unplayedOnly,
                stopwatch,
                outcome: GetOutcome(result.Errors),
                succeeded: false,
                occurredAt,
                correlationId,
                identifierResolutionMilliseconds: identifierStopwatch.ElapsedMilliseconds);

            return Problem(result.Errors.ToList());
        }

        await TrackRandomGameGeneratedAsync();

        await PublishGamePickEventAsync(
            provider,
            result,
            unplayedOnly,
            stopwatch,
            outcome: GamePickOutcome.Served,
            succeeded: true,
            occurredAt,
            correlationId,
            identifierResolutionMilliseconds: identifierStopwatch.ElapsedMilliseconds);

        return Ok(result.Game);
    }

    private async Task PublishGamePickEventAsync(
        string provider,
        RandomGamePickAttempt? telemetry,
        bool unplayedOnly,
        Stopwatch stopwatch,
        string outcome,
        bool succeeded,
        DateTimeOffset occurredAt,
        string correlationId,
        long identifierResolutionMilliseconds)
    {
        stopwatch.Stop();

        try
        {
            await _missionControlClient.TryPublishAsync(
                eventType:
                    RandomSteamGameEventTypes.GamePickCompleted,
                payload: new GamePickCompletedEvent(
                    Provider: provider,
                    AppId: telemetry?.Game?.Id,
                    // Display metadata only. Use AppId for stable joins, grouping, and identity.
                    GameName: GamePickTelemetryName.Sanitize(telemetry?.Game?.Name),
                    UnplayedOnly: unplayedOnly,
                    DurationMilliseconds:
                        stopwatch.ElapsedMilliseconds,
                    CacheStatus: telemetry?.Cache.StatusName ?? OwnedGamesCacheInfo.Unknown.StatusName,
                    CacheAgeSeconds: telemetry?.Cache.AgeSeconds,
                    EligibleGameCount: telemetry?.EligibleGameCount,
                    LibrarySizeBucket: telemetry is null
                        ? null
                        : telemetry.LibraryGameCount is null
                            ? null
                            : LibrarySizeBuckets.FromCount(telemetry.LibraryGameCount.Value),
                    Timings: telemetry is null
                        ? new GamePickTimings(identifierResolutionMilliseconds, 0, 0)
                        : telemetry.Timings with
                        {
                            IdentifierResolutionMilliseconds = identifierResolutionMilliseconds
                        },
                    CommitSha: string.IsNullOrWhiteSpace(_applicationOptions.CommitSha)
                        ? null
                        : _applicationOptions.CommitSha,
                    Outcome: outcome,
                    Succeeded: succeeded),
                occurredAt: occurredAt,
                correlationId: correlationId,
                cancellationToken: CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Failed to publish game-pick event {CorrelationId}.",
                correlationId);
        }
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

        if (!IsValidVanityUrl(vanityUrl))
        {
            return Problem([Errors.Steam.InvalidVanityUrl]);
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

    private static Error? ValidateIdentifier(long? userId, string? vanityUrl)
    {
        var hasUserId = userId.HasValue;
        var hasVanityUrl = !string.IsNullOrWhiteSpace(vanityUrl);

        if (!hasUserId && !hasVanityUrl)
        {
            return Errors.Steam.IdentifierRequired;
        }

        if (hasUserId && hasVanityUrl)
        {
            return Errors.Steam.AmbiguousIdentifier;
        }

        if (hasUserId && !IsValidSteamId(userId!.Value))
        {
            return Errors.Steam.InvalidSteamId;
        }

        if (hasVanityUrl && !IsValidVanityUrl(vanityUrl!))
        {
            return Errors.Steam.InvalidVanityUrl;
        }

        return null;
    }

    private static bool IsValidSteamId(long steamId)
    {
        return steamId is >= MinSteamId and <= MaxSteamId;
    }

    private static bool IsValidVanityUrl(string vanityUrl)
    {
        return SteamVanityUrlHelper.TryNormalize(vanityUrl, out _);
    }

    private static async Task<ErrorOr<long>> ResolveIdentifier(
        IGameProvider service,
        long? userId,
        string? vanityUrl)
    {
        if (!string.IsNullOrWhiteSpace(vanityUrl))
        {
            return await service.ResolveIdentifierAsync(vanityUrl);
        }

        if (userId.HasValue)
        {
            return userId.Value;
        }

        return Errors.Steam.IdentifierRequired;
    }

    private static string GetOutcome(IReadOnlyList<Error> errors)
    {
        var first = errors.FirstOrDefault();
        return first.Code switch
        {
            "Steam.EmptyLibrary" => GamePickOutcome.EmptyLibrary,
            "Steam.NoSelectableGamesAfterExclusions" => GamePickOutcome.NoEligibleGames,
            "Steam.ApiFailed" => GamePickOutcome.LibraryLoadFailed,
            "Steam.VanityResolutionFailed" => GamePickOutcome.IdentifierResolutionFailed,
            _ => GamePickOutcome.SelectionFailed
        };
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
