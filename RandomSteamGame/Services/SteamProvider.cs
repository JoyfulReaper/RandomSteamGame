/*
 * Random Steam Game
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using ErrorOr;
using RandomSteamGame.Common.Errors;
using RandomSteamGame.Services.Interfaces;
using RandomSteamGame.Shared.Contracts;
using RandomSteamGame.Shared.Services;
using SteamApiClient.Contracts.SteamStoreApi;
using SteamApiClient.HttpClients;
using System.Diagnostics;

namespace RandomSteamGame.Services;

public class SteamProvider : IGameProvider
{
    private readonly ISteamClient _steamClient;
    private readonly ISteamStoreClient _steamStoreClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IHtmlSanitizerService _htmlSanitizer;
    private readonly ILogger<SteamProvider> _logger;

    private const int MAX_ATTEMPTS = 3; // TODO make configurable in appsettings

    public string ProviderKey => "steam";

    public SteamProvider(
        ISteamClient steamClient,
        ISteamStoreClient steamStoreClient,
        IHttpContextAccessor httpContextAccessor,
        IHtmlSanitizerService htmlSanitizerService,
        ILogger<SteamProvider> logger)
    {
        _htmlSanitizer = htmlSanitizerService;
        _steamClient = steamClient;
        _steamStoreClient = steamStoreClient;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<ErrorOr<OwnedGamesResponse>> GetOwnedGamesAsync(long userId)
        => await FetchOwnedGamesAsync(userId);

    public async Task<ErrorOr<GameDetails>> GetRandomGameDetailsAsync(long userId, bool unplayedOnly = false)
    {
        var result = await FetchRandomGamePickAsync(userId, unplayedOnly);
        return result.Succeeded ? result.Game! : result.Errors.ToList();
    }

    public async Task<RandomGamePickAttempt> GetRandomGamePickAsync(long userId, bool unplayedOnly = false)
        => await FetchRandomGamePickAsync(userId, unplayedOnly);

    public async Task<ErrorOr<long>> ResolveIdentifierAsync(string identifier)
        => await FetchSteamIdFromVanityAsync(identifier);

    public async Task<ErrorOr<OwnedGamesResponse>> FetchOwnedGamesAsync(long steamId)
    {
        SteamApiClient.Contracts.SteamApi.OwnedGames ownedGames;
        try
        {
            ownedGames = await _steamClient.GetOwnedGames(steamId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting owned games.");
            return Errors.Steam.SteamApiFailed;
        }

        if (ownedGames?.Games == null || ownedGames.Games.Count == 0)
        {
            return Errors.Steam.EmptyLibrary;
        }

        return MapToOwnedGamesResponse(steamId, ownedGames);
    }

    public async Task InvalidateOwnedGamesCacheAsync(long steamId)
    {
        await _steamClient.InvalidateOwnedGamesCacheAsync(steamId);
    }

    public async Task<ErrorOr<long>> FetchSteamIdFromVanityAsync(string vanityUrl)
    {
        try
        {
            var steamId = await _steamClient.GetSteamIdFromVanityUrl(vanityUrl);
            if (steamId == 0)
            {
                _logger.LogWarning("Steam API returned no match for vanity URL.");
                return Errors.Steam.VanityResolutionFailed;
            }

            return steamId;
        }
        catch (ArgumentException)
        {
            return Errors.Steam.InvalidVanityUrl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while resolving vanity URL.");
            return Errors.Steam.VanityResolutionFailed;
        }
    }

    public async Task<RandomGamePickAttempt> FetchRandomGamePickAsync(long steamId, bool unplayedOnly = false)
    {
        var libraryLoadStopwatch = Stopwatch.StartNew();
        OwnedGamesResult ownedGamesResult;

        try
        {
            ownedGamesResult = await _steamClient.GetOwnedGamesWithCacheInfo(steamId);
        }
        catch (Exception ex)
        {
            libraryLoadStopwatch.Stop();
            _logger.LogError(ex, "Error loading owned games for random-game pick.");
            return RandomGamePickAttempt.Failure(
                [Errors.Steam.SteamApiFailed],
                timings: new Events.GamePickTimings(
                    IdentifierResolutionMilliseconds: 0,
                    LibraryLoadMilliseconds: libraryLoadStopwatch.ElapsedMilliseconds,
                    SelectionMilliseconds: 0));
        }

        libraryLoadStopwatch.Stop();
        var ownedGames = ownedGamesResult.OwnedGames;
        if (ownedGames?.Games == null || ownedGames.Games.Count == 0)
        {
            return RandomGamePickAttempt.Failure(
                [Errors.Steam.EmptyLibrary],
                ownedGamesResult.Cache,
                eligibleGameCount: 0,
                libraryGameCount: 0,
                timings: new Events.GamePickTimings(
                    IdentifierResolutionMilliseconds: 0,
                    LibraryLoadMilliseconds: libraryLoadStopwatch.ElapsedMilliseconds,
                    SelectionMilliseconds: 0));
        }

        var selectionStopwatch = Stopwatch.StartNew();
        var selectionResult = await GetRandomGameDataAsync(ownedGames, unplayedOnly);
        selectionStopwatch.Stop();

        if (!selectionResult.Succeeded)
        {
            return RandomGamePickAttempt.Failure(
                selectionResult.Errors,
                ownedGamesResult.Cache,
                selectionResult.EligibleGameCount,
                ownedGames.Games.Count,
                new Events.GamePickTimings(
                    IdentifierResolutionMilliseconds: 0,
                    LibraryLoadMilliseconds: libraryLoadStopwatch.ElapsedMilliseconds,
                    SelectionMilliseconds: selectionStopwatch.ElapsedMilliseconds));
        }

        var appData = selectionResult.AppData!;
        var matchingGame = ownedGames.Games.FirstOrDefault(g => g.AppId == appData.SteamAppId);
        if (matchingGame == null)
        {
            return RandomGamePickAttempt.Failure(
                [Errors.Steam.SteamApiSuccessButCouldntGetAppData],
                ownedGamesResult.Cache,
                selectionResult.EligibleGameCount,
                ownedGames.Games.Count,
                new Events.GamePickTimings(
                    IdentifierResolutionMilliseconds: 0,
                    LibraryLoadMilliseconds: libraryLoadStopwatch.ElapsedMilliseconds,
                    SelectionMilliseconds: selectionStopwatch.ElapsedMilliseconds));
        }

        var gameDetails = new GameDetails
        {
            Id = appData.SteamAppId,
            Name = appData.Name,
            Description = _htmlSanitizer.Sanitize(appData.AboutTheGame),
            HeaderImage = appData.HeaderImage,
            PlaytimeForever = matchingGame.PlaytimeForever,
            PlaytimeWindowsForever = matchingGame.PlaytimeWindowsForever,
            PlaytimeMacForever = matchingGame.PlaytimeMacForever,
            PlaytimeLinuxForever = matchingGame.PlaytimeLinuxForever,
            Playtime2Weeks = matchingGame.Playtime2Weeks,
            RTimeLastPlayed = matchingGame.RTimeLastPlayed
        };

        return RandomGamePickAttempt.Success(
            gameDetails,
            ownedGamesResult.Cache,
            selectionResult.EligibleGameCount,
            ownedGames.Games.Count,
            new Events.GamePickTimings(
                IdentifierResolutionMilliseconds: 0,
                LibraryLoadMilliseconds: libraryLoadStopwatch.ElapsedMilliseconds,
                SelectionMilliseconds: selectionStopwatch.ElapsedMilliseconds));
    }

    private async Task<SelectionAttempt> GetRandomGameDataAsync(
        SteamApiClient.Contracts.SteamApi.OwnedGames ownedGames,
        bool unplayedOnly)
    {
        var excludedGameIds = GetExcludedGameIds();
        var shuffledGameIds = GameSelectionHelper.GetSelectableGameIds(
            ownedGames.Games,
            excludedGameIds,
            game => game.AppId,
            game => !unplayedOnly || IsUnplayed(game));

        if (shuffledGameIds.Count == 0)
        {
            _logger.LogDebug("All owned games were excluded for the current request.");
            return SelectionAttempt.Failure(
                [Errors.Steam.NoSelectableGamesAfterExclusions],
                eligibleGameCount: 0);
        }

        int attempts = 0;

        foreach (var selectedAppId in shuffledGameIds)
        {
            if (attempts >= MAX_ATTEMPTS)
            {
                break;
            }

            var appData = await _steamStoreClient.GetAppData(selectedAppId);
            attempts++;

            if (appData != null)
            {
                return SelectionAttempt.Success(appData, shuffledGameIds.Count);
            }

            _logger.LogDebug("Steam store confirmed AppId {AppId} is unavailable.", selectedAppId);
        }

        return SelectionAttempt.Failure(
            [Errors.Steam.SteamApiSuccessButCouldntGetAppData],
            shuffledGameIds.Count);
    }

    private HashSet<int> GetExcludedGameIds()
    {
        var cookieValue = _httpContextAccessor.HttpContext?.Request.Cookies["ExcludedGameIds"];

        if (string.IsNullOrWhiteSpace(cookieValue))
        {
            return [];
        }

        return cookieValue
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => int.TryParse(value, out var appId) ? appId : (int?)null)
            .Where(appId => appId.HasValue)
            .Select(appId => appId!.Value)
            .ToHashSet();
    }

    internal static bool IsUnplayed(SteamApiClient.Contracts.SteamApi.Game game)
    {
        return game.PlaytimeForever <= 0 &&
               game.PlaytimeWindowsForever <= 0 &&
               game.PlaytimeMacForever <= 0 &&
               game.PlaytimeLinuxForever <= 0 &&
               game.Playtime2Weeks <= 0 &&
               game.RTimeLastPlayed <= 0;
    }

    internal static int GetDisplayPlaytimeMinutes(SteamApiClient.Contracts.SteamApi.Game game)
    {
        return SteamPlaytimeHelper.GetDisplayPlaytimeMinutes(
            game.PlaytimeForever,
            game.PlaytimeWindowsForever,
            game.PlaytimeMacForever,
            game.PlaytimeLinuxForever,
            game.Playtime2Weeks);
    }

    private static OwnedGamesResponse MapToOwnedGamesResponse(long steamId, SteamApiClient.Contracts.SteamApi.OwnedGames sdkOwnedGames)
    {
        var gamesList = new List<Game>();
        foreach (var g in sdkOwnedGames.Games)
        {
            gamesList.Add(new Game(
                g.AppId,
                g.Name,
                g.PlaytimeForever,
                g.ImgIconUrl,
                g.PlaytimeWindowsForever,
                g.PlaytimeMacForever,
                g.PlaytimeLinuxForever,
                g.RTimeLastPlayed,
                g.Playtime2Weeks
            ));
        }

        return new OwnedGamesResponse(steamId, sdkOwnedGames.GameCount, gamesList);
    }

    private sealed record SelectionAttempt(
        AppData? AppData,
        IReadOnlyList<Error> Errors,
        int EligibleGameCount)
    {
        public bool Succeeded => AppData is not null && Errors.Count == 0;

        public static SelectionAttempt Success(AppData appData, int eligibleGameCount)
            => new(appData, [], eligibleGameCount);

        public static SelectionAttempt Failure(IReadOnlyList<Error> errors, int eligibleGameCount)
            => new(null, errors, eligibleGameCount);
    }
}
