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
using SteamApiClient.Services;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using SteamDeckCompatibilityCategory = SteamApiClient.Contracts.SteamApi.SteamDeckCompatibilityCategory;

namespace RandomSteamGame.Controllers;


[Route("api/{provider}")]
[AllowAnonymous]
[ApiController]
[EnableRateLimiting("steam_api_limiter")]
public class GameController : ApiController
{
    private readonly GameProviderFactory _factory;
    private readonly GameApplicationService _gameOperations;
    private readonly IAppStatsService _appStatsService;
    private readonly ISteamLibraryExportService _steamLibraryExportService;
    private readonly IMissionControlClient _missionControlClient;
    private readonly ApplicationOptions _applicationOptions;
    private readonly ILogger<GameController> _logger;
    private readonly IVisitorIdProvider _visitorIdProvider;
    private readonly ILibraryExportCooldownTracker _libraryExportCooldownTracker;
    private readonly LibraryExportOptions _libraryExportOptions;
    private readonly GlobalLibraryExportCooldownTracker _globalExportCooldown;

    public GameController(
        GameProviderFactory factory,
        GameApplicationService gameOperations,
        IAppStatsService appStatsService,
        ISteamLibraryExportService steamLibraryExportService,
        IMissionControlClient missionControlClient,
        IVisitorIdProvider visitorIdProvider,
        ILibraryExportCooldownTracker libraryExportCooldownTracker,
        IOptions<ApplicationOptions> applicationOptions,
        ILogger<GameController> logger,
        IOptions<LibraryExportOptions> libraryExportOptions,
        GlobalLibraryExportCooldownTracker globalExportCooldown)
    {
        _missionControlClient = missionControlClient;
        _visitorIdProvider = visitorIdProvider;
        _applicationOptions = applicationOptions.Value;
        _factory = factory;
        _gameOperations = gameOperations;
        _appStatsService = appStatsService;
        _steamLibraryExportService = steamLibraryExportService;
        _logger = logger;
        _libraryExportCooldownTracker = libraryExportCooldownTracker;
        _libraryExportOptions = libraryExportOptions.Value;
        _globalExportCooldown = globalExportCooldown;
    }

    /// <summary>
    /// Gets the list of owned games for a specific Steam ID.
    /// GET /api/steam/{steamId}/library
    /// </summary>
    [HttpGet("{steamId}/library")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(OwnedGamesResponse))]
    public async Task<IActionResult> GetLibrary(string provider, long steamId)
    {
        var result = await _gameOperations.GetLibrary(provider, steamId);
        return result.Match(Ok, Problem);
    }

    /// <summary>
    /// Exports the list of owned games for a specific Steam ID as CSV.
    /// GET /api/steam/{steamId}/library/export.csv
    /// </summary>
    [HttpGet("{steamId:long}/library/export.csv")]
    [EnableRateLimiting("library_export_limiter")]
    [Produces("text/csv")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportLibrary(
        string provider,
        long steamId)
    {
        Response.Headers["Cache-Control"] = "private, no-store";
        Response.Headers["CDN-Cache-Control"] = "no-store";


        if (!TryGetProvider(provider, out var service))
        {
            return Problem([Errors.Steam.UnsupportedProvider(provider)]);
        }

        if (!IsValidSteamId(steamId))
        {
            return Problem([Errors.Steam.InvalidSteamId]);
        }

        var occurredAt = DateTimeOffset.UtcNow;
        var correlationId = Guid.NewGuid().ToString("N");
        var partitionKey = LibraryExportRateLimitPartitionKey.From(HttpContext.Connection.RemoteIpAddress);
        var isGlobal = _libraryExportOptions.RateLimitMode == LibraryExportRateLimitMode.Global;
        var retryAfter = isGlobal
            ? _globalExportCooldown.TryStart()
            : _libraryExportCooldownTracker.GetRetryAfter(partitionKey);

        if (retryAfter is not null)
        {
            var retryAfterSeconds = Math.Max(1, (long)Math.Ceiling(retryAfter.Value.TotalSeconds));
            Response.Headers.RetryAfter = retryAfterSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);

            _ = PublishLibraryExportRejectedEventAsync(
                provider,
                LibraryExportRejectionReason.Cooldown,
                retryAfterSeconds,
                occurredAt,
                correlationId);

            return new ContentResult
            {
                StatusCode = StatusCodes.Status429TooManyRequests,
                ContentType = "text/plain; charset=utf-8",
                Content = isGlobal
                    ? $"Steam library CSV exports share a global cooldown. Please try again in {retryAfterSeconds} seconds."
                    : "Steam library CSV exports are limited to one per IP address " +
                        "every 72 hours after a successful export."
            };
        }


        var stopwatch = Stopwatch.StartNew();

        var result = await service.GetOwnedGamesAsync(steamId);
        if (result.IsError)
        {
            return Problem(result.Errors);
        }

        IReadOnlyDictionary<int, SteamDeckCompatibilityCategory> deckCompatibility =
            new Dictionary<int, SteamDeckCompatibilityCategory>();

        if (service is ISteamDeckCompatibilityProvider deckProvider)
        {
            deckCompatibility = await deckProvider.GetSteamDeckCompatibilityAsync(
                result.Value.Games.Select(game => game.AppId),
                HttpContext.RequestAborted);
        }

        var csvBytes = _steamLibraryExportService.Export(result.Value, deckCompatibility);

        await TrackLibraryExportedAsync();

        if (!isGlobal)
        {
            _libraryExportCooldownTracker.MarkSucceeded(partitionKey);
        }

        var verifiedCount = 0;
        var playableCount = 0;
        var unsupportedCount = 0;
        var unknownCount = 0;

        foreach (var game in result.Value.Games)
        {
            var category =
                deckCompatibility.TryGetValue(game.AppId, out var value)
                    ? value
                    : SteamDeckCompatibilityCategory.Unknown;

            switch (category)
            {
                case SteamDeckCompatibilityCategory.Verified:
                    verifiedCount++;
                    break;

                case SteamDeckCompatibilityCategory.Playable:
                    playableCount++;
                    break;

                case SteamDeckCompatibilityCategory.Unsupported:
                    unsupportedCount++;
                    break;

                default:
                    unknownCount++;
                    break;
            }
        }

        stopwatch.Stop();

        await PublishLibraryExportCompletedEventAsync(
            provider,
            result.Value.Games.Count,
            stopwatch.ElapsedMilliseconds,
            verifiedCount,
            playableCount,
            unsupportedCount,
            unknownCount,
            occurredAt,
            correlationId);

        return File(
            csvBytes,
            "text/csv; charset=utf-8",
            $"steam-library-{steamId}.csv");
    }

    /// <summary>
    /// Invalidates the cached owned games for a user.
    /// POST /api/steam/{userId}/library/refresh
    /// </summary>
    [HttpPost("{userId}/library/refresh")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RefreshLibrary(string provider, long userId)
    {
        var result = await _gameOperations.RefreshLibraryAsync(provider, userId);
        if (result.IsError)
        {
            return Problem(result.Errors);
        }
        return result.Value.CooldownProblem is { } problem
            ? StatusCode(StatusCodes.Status429TooManyRequests, problem)
            : NoContent();
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
        var result = await _gameOperations.GetRandomGameDetailsAsync(
            provider, userId, vanityUrl, GameRequestContext.From(HttpContext), unplayedOnly);
        return result.Match(Ok, Problem);
    }

    private async Task PublishLibraryExportRejectedEventAsync(
        string? provider,
        string reason,
        long? retryAfterSeconds,
        DateTimeOffset occurredAt,
        string correlationId)
    {
        try
        {
            await _missionControlClient.TryPublishAsync(
                eventType: RandomSteamGameEventTypes.LibraryExportRejected,
                payload: new LibraryExportRejectedEvent(
                    VisitorId: GetVisitorIdForTelemetry(),
                    Provider: provider,
                    IngressNetwork: IngressNetworkClassifier.FromHost(HttpContext.Request.Host.Host),
                    Reason: reason,
                    RetryAfterSeconds: retryAfterSeconds,
                    CommitSha: string.IsNullOrWhiteSpace(_applicationOptions.CommitSha)
                        ? null
                        : _applicationOptions.CommitSha),
                occurredAt: occurredAt,
                payloadTypeInfo: RandomSteamGameJsonContext.Default.LibraryExportRejectedEvent,
                correlationId: correlationId,
                cancellationToken: CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Failed to publish library-export rejection event {CorrelationId}.",
                correlationId);
        }
    }

    private async Task PublishLibraryExportCompletedEventAsync(
    string provider,
    int gameCount,
    long durationMilliseconds,
    int verifiedCount,
    int playableCount,
    int unsupportedCount,
    int unknownCount,
    DateTimeOffset occurredAt,
    string correlationId)
    {
        try
        {
            await _missionControlClient.TryPublishAsync(
                eventType: RandomSteamGameEventTypes.LibraryExportCompleted,
                payload: new LibraryExportCompletedEvent(
                    VisitorId: GetVisitorIdForTelemetry(),
                    Provider: provider,
                    IngressNetwork: IngressNetworkClassifier.FromHost(HttpContext.Request.Host.Host),
                    GameCount: gameCount,
                    DurationMilliseconds: durationMilliseconds,
                    VerifiedCount: verifiedCount,
                    PlayableCount: playableCount,
                    UnsupportedCount: unsupportedCount,
                    UnknownCount: unknownCount,
                    CommitSha: string.IsNullOrWhiteSpace(_applicationOptions.CommitSha)
                        ? null
                        : _applicationOptions.CommitSha),
                occurredAt: occurredAt,
                payloadTypeInfo: RandomSteamGameJsonContext.Default.LibraryExportCompletedEvent,
                correlationId: correlationId,
                cancellationToken: CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Failed to publish library-export event {CorrelationId}.",
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
        var result = await _gameOperations.ResolveVanityAsync(provider, vanityUrl);
        return result.Match(value => Ok(value), Problem);
    }

    private bool TryGetProvider(
        string provider,
        [NotNullWhen(true)] out IGameProvider? service)
    {
        return _factory.TryGetProvider(provider, out service);
    }

    private static bool IsValidSteamId(long steamId) => GameApplicationService.IsValidSteamId(steamId);

    private async Task TrackLibraryExportedAsync()
    {
        try
        {
            await _appStatsService.IncrementLibrariesExportedAsync();
        }
        catch (Exception exception)
        {
            // Stats must never prevent a successful export.
            _logger.LogWarning(exception, "Failed to increment libraries exported counter.");
        }
    }

    private string? GetVisitorIdForTelemetry()
    {
        try
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

            return string.IsNullOrWhiteSpace(ip)
                ? null
                : _visitorIdProvider.GetVisitorId(ip);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Failed to generate visitor ID for game-pick telemetry.");

            return null;
        }
    }
}
