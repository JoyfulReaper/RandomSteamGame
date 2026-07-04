/*
 * Random Steam Game
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using ErrorOr;
using Microsoft.AspNetCore.Http;
using RandomSteamGame.Common.Errors;
using RandomSteamGame.Services.Interfaces;
using RandomSteamGame.Shared.Contracts;
using RandomSteamGame.Shared.Services;
using SteamApiClient.Contracts.SteamStoreApi;
using SteamApiClient.HttpClients;

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

    public async Task<ErrorOr<RandomGameResponse>> GetRandomGameAsync(long userId, bool unplayedOnly = false)
        => await FetchRandomGameAsync(userId, unplayedOnly);

    public async Task<ErrorOr<GameDetails>> GetRandomGameDetailsAsync(long userId, bool unplayedOnly = false)
        => await FetchRandomGameDetailsAsync(userId, unplayedOnly);

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
            _logger.LogError(ex, "Error getting owned games for SteamID: {SteamId}", steamId);
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
                _logger.LogWarning("Steam API returned 0 (Not Found) for vanity URL: {VanityUrl}", vanityUrl);
                return Errors.Steam.VanityResolutionFailed;
            }

            return steamId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while resolving vanity URL {VanityUrl}.", vanityUrl);
            return Errors.Steam.VanityResolutionFailed;
        }
    }

    public async Task<ErrorOr<GameDetails>> FetchRandomGameDetailsAsync(long steamId, bool unplayedOnly = false)
    {
        try
        {
            var ownedGames = await _steamClient.GetOwnedGames(steamId);
            if (ownedGames?.Games == null || ownedGames.Games.Count == 0)
            {
                return Errors.Steam.EmptyLibrary;
            }

            var appDataResult = await GetRandomGameDataAsync(ownedGames, unplayedOnly);
            if (appDataResult.IsError)
            {
                return appDataResult.Errors;
            }

            var appData = appDataResult.Value;
            var matchingGame = ownedGames.Games.FirstOrDefault(g => g.AppId == appData.SteamAppId);
            if (matchingGame == null)
            {
                return Errors.Steam.SteamApiSuccessButCouldntGetAppData;
            }

            return new GameDetails
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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting random game details for SteamID: {SteamId}", steamId);
            return Errors.Steam.SteamApiFailed;
        }
    }

    public async Task<ErrorOr<RandomGameResponse>> FetchRandomGameAsync(long steamId, bool unplayedOnly = false)
    {
        try
        {
            var ownedGames = await _steamClient.GetOwnedGames(steamId);
            if (ownedGames?.Games == null || ownedGames.Games.Count == 0)
            {
                return Errors.Steam.EmptyLibrary;
            }

            var appDataResult = await GetRandomGameDataAsync(ownedGames, unplayedOnly);
            if (appDataResult.IsError)
            {
                return appDataResult.Errors;
            }

            var appData = appDataResult.Value;

            var matchingGame = ownedGames.Games.FirstOrDefault(g => g.AppId == appData.SteamAppId);
            if (matchingGame == null)
            {
                return Errors.Steam.SteamApiSuccessButCouldntGetAppData;
            }

            var appInfo = new SteamAppInformation(
                appData.Name,
                appData.SteamAppId,
                appData.IsFree,
                _htmlSanitizer.Sanitize(appData.AboutTheGame),
                _htmlSanitizer.Sanitize(appData.DetailedDescription),
                appData.ShortDescription,
                appData.Background ?? string.Empty,
                appData.BackgroundRaw
            );

            return new RandomGameResponse(
                steamId,
                appData.SteamAppId,
                appInfo,
                matchingGame.PlaytimeForever,
                matchingGame.RTimeLastPlayed,
                matchingGame.Playtime2Weeks
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating full random Steam game selection for SteamID: {SteamId}", steamId);
            return Errors.Steam.SteamApiFailed;
        }
    }

    private async Task<ErrorOr<AppData>> GetRandomGameDataAsync(
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
            return Errors.Steam.NoSelectableGamesAfterExclusions;
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
                return appData;
            }

            _logger.LogDebug("Steam store confirmed AppId {AppId} is unavailable.", selectedAppId);
        }

        return Errors.Steam.SteamApiSuccessButCouldntGetAppData;
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
}
