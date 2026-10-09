using ErrorOr;
using JoyfulReaperLib.MissionControl;
using Microsoft.Extensions.Options;
using RandomSteamGame.Common.Errors;
using RandomSteamGame.Events;
using RandomSteamGame.Options;
using RandomSteamGame.Services.Interfaces;
using RandomSteamGame.Shared.Contracts;
using SteamApiClient;
using SteamApiClient.Services;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace RandomSteamGame.Services;

// Shared orchestration for HTTP endpoints and direct Blazor server execution.
public sealed class GameApplicationService(
    GameProviderFactory factory,
    IOwnedGamesCacheResetTracker ownedGamesCacheResetTracker,
    IAppStatsService appStatsService,
    IMissionControlClient missionControlClient,
    IVisitorIdProvider visitorIdProvider,
    IOptions<ApplicationOptions> applicationOptions,
    ILogger<GameApplicationService> logger)
{
    private const long MinSteamId = 10_000_000_000_000_000L;
    private const long MaxSteamId = 99_999_999_999_999_999L;
    private readonly GameProviderFactory _factory = factory;
    private readonly IOwnedGamesCacheResetTracker _ownedGamesCacheResetTracker = ownedGamesCacheResetTracker;
    private readonly IAppStatsService _appStatsService = appStatsService;
    private readonly IMissionControlClient _missionControlClient = missionControlClient;
    private readonly IVisitorIdProvider _visitorIdProvider = visitorIdProvider;
    private readonly ApplicationOptions _applicationOptions = applicationOptions.Value;
    private readonly ILogger<GameApplicationService> _logger = logger;

    public async Task<ErrorOr<OwnedGamesResponse>> GetLibrary(string provider, long steamId, CancellationToken ct = default)
    {
        if (!TryGetProvider(provider, out var service))
        {
            return Errors.Steam.UnsupportedProvider(provider);
        }

        if (!IsValidSteamId(steamId))
        {
            return Errors.Steam.InvalidSteamId;
        }

        var result = await service.GetOwnedGamesAsync(steamId, ct);
        return result;
    }

    public async Task<ErrorOr<LibraryRefreshResult>> RefreshLibraryAsync(string provider, long userId, CancellationToken ct = default)
    {
        if (!TryGetProvider(provider, out var service))
        {
            return Errors.Steam.UnsupportedProvider(provider);
        }
        if (!IsValidSteamId(userId))
        {
            return Errors.Steam.InvalidSteamId;
        }
        var nextAvailableAt = await _ownedGamesCacheResetTracker.RefreshAsync(
            userId, token => service.InvalidateOwnedGamesCacheAsync(userId, token), ct);
        return new LibraryRefreshResult(nextAvailableAt);
    }

    public async Task<ErrorOr<GameDetails>> GetRandomGameDetailsAsync(
        string provider,
        long? userId,
        string? vanityUrl,
        GameRequestContext requestContext,
        bool unplayedOnly = false,
        IReadOnlyCollection<int>? excludedGameIds = null,
        CancellationToken ct = default)
    {
        var occurredAt = DateTimeOffset.UtcNow;
        var correlationId = Guid.NewGuid().ToString("N");
        var stopwatch = Stopwatch.StartNew();

        if (!TryGetProvider(provider, out var service))
        {
            await PublishGamePickEventAsync(
                requestContext,
                provider,
                telemetry: null,
                unplayedOnly,
                stopwatch,
                outcome: "unsupported-provider",
                succeeded: false,
                occurredAt,
                correlationId,
                identifierResolutionMilliseconds: 0);

            return Errors.Steam.UnsupportedProvider(provider);
        }

        var identifierValidation = ValidateIdentifier(userId, vanityUrl);
        if (identifierValidation is not null)
        {
            await PublishGamePickEventAsync(
                requestContext,
                provider,
                telemetry: null,
                unplayedOnly,
                stopwatch,
                outcome: "invalid-identifier",
                succeeded: false,
                occurredAt,
                correlationId,
                identifierResolutionMilliseconds: 0);

            return identifierValidation.Value;
        }

        var identifierStopwatch = Stopwatch.StartNew();
        var targetId = await ResolveIdentifier(service, userId, vanityUrl, ct);
        identifierStopwatch.Stop();
        if (targetId.IsError)
        {
            await PublishGamePickEventAsync(
                requestContext,
                provider,
                telemetry: null,
                unplayedOnly,
                stopwatch,
                outcome: "identifier-resolution-failed",
                succeeded: false,
                occurredAt,
                correlationId,
                identifierResolutionMilliseconds: identifierStopwatch.ElapsedMilliseconds);

            return targetId.Errors;
        }

        var result = await service.GetRandomGamePickAsync(
            targetId.Value, unplayedOnly, excludedGameIds ?? requestContext.ExcludedGameIds, ct);
        if (!result.Succeeded)
        {
            await PublishGamePickEventAsync(
                requestContext,
                provider,
                telemetry: result,
                unplayedOnly,
                stopwatch,
                outcome: GetOutcome(result.Errors),
                succeeded: false,
                occurredAt,
                correlationId,
                identifierResolutionMilliseconds: identifierStopwatch.ElapsedMilliseconds);

            return result.Errors.ToList();
        }

        await TrackRandomGameGeneratedAsync();

        await PublishGamePickEventAsync(
            requestContext,
            provider,
            result,
            unplayedOnly,
            stopwatch,
            outcome: GamePickOutcome.Served,
            succeeded: true,
            occurredAt,
            correlationId,
            identifierResolutionMilliseconds: identifierStopwatch.ElapsedMilliseconds);

        return result.Game!;
    }

    private async Task PublishGamePickEventAsync(
        GameRequestContext requestContext,
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
                    VisitorId: GetVisitorIdForTelemetry(requestContext.VisitorIpAddress),
                    Provider: provider,
                    IngressNetwork: IngressNetworkClassifier.FromHost(requestContext.Host),
                    AppId: telemetry?.Game?.Id,
                    // Display metadata only. Use AppId for stable joins, grouping, and identity.
                    GameName: GamePickTelemetryName.Sanitize(telemetry?.Game?.Name),
                    UnplayedOnly: unplayedOnly,
                    DurationMilliseconds: stopwatch.ElapsedMilliseconds,
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
                payloadTypeInfo: RandomSteamGameJsonContext.Default.GamePickCompletedEvent,
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

    public async Task<ErrorOr<long>> ResolveVanityAsync(string provider, string vanityUrl, CancellationToken ct = default)
    {
        if (!TryGetProvider(provider, out var service))
        {
            return Errors.Steam.UnsupportedProvider(provider);
        }

        if (!IsValidVanityUrl(vanityUrl))
        {
            return Errors.Steam.InvalidVanityUrl;
        }

        var result = await service.ResolveIdentifierAsync(vanityUrl, ct);
        return result;
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

    public static bool IsValidSteamId(long steamId)
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
        string? vanityUrl,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(vanityUrl))
        {
            return await service.ResolveIdentifierAsync(vanityUrl, ct);
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

    private string? GetVisitorIdForTelemetry(string? ip)
    {
        try
        {
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

public sealed record LibraryRefreshResult(DateTimeOffset? NextAvailableAt)
{
    public ApiProblem? CooldownProblem => NextAvailableAt is null ? null : new ApiProblem
    {
        Title = "TooManyRequests",
        Status = StatusCodes.Status429TooManyRequests,
        Detail = $"Owned games cache can only be reset once every 12 hours. Try again after {NextAvailableAt.Value.ToLocalTime():f}."
    };
}
