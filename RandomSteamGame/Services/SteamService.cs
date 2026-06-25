/*
 * Random Steam Game
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using ErrorOr;
using Microsoft.Extensions.Caching.Distributed;
using RandomSteamGame.Common.Errors;
using RandomSteamGame.Services.Interfaces;
using RandomSteamGame.Shared.Contracts;
using SteamApiClient.Contracts.SteamStoreApi;
using SteamApiClient.HttpClients;

namespace RandomSteamGame.Services;

public class SteamService : ISteamService
{
    private readonly ISteamClient _steamClient;
    private readonly ISteamStoreClient _steamStoreClient;
    private readonly ILogger<SteamService> _logger;
    private readonly IHtmlSanitizerService _htmlSanitizer;
    private readonly IDistributedCache _cache;

    private const int MAX_ATTEMPTS = 3;

    public SteamService(
        ISteamClient steamClient,
        ISteamStoreClient steamStoreClient,
        IDistributedCache cache,
        IHtmlSanitizerService htmlSanitizerService,
        ILogger<SteamService> logger)
    {
        _htmlSanitizer = htmlSanitizerService;
        _steamClient = steamClient;
        _steamStoreClient = steamStoreClient;
        _logger = logger;
        _cache = cache;
    }

    public async Task<ErrorOr<OwnedGamesResponse>> GetOwnedGamesAsync(long steamId)
    {
        try
        {
            var ownedGames = await _steamClient.GetOwnedGames(steamId);
            if (ownedGames?.Games == null || ownedGames.Games.Count == 0)
            {
                return Errors.Steam.EmptyLibrary;
            }

            return MapToOwnedGamesResponse(steamId, ownedGames);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting owned games for SteamID: {SteamId}", steamId);
            return Errors.Steam.SteamApiFailed;
        }
    }

    public async Task<ErrorOr<long>> ResolveVanityUrlAsync(string vanityUrl)
    {
        try
        {
            var steamId = await _steamClient.GetSteamIdFromVanityUrl(vanityUrl);
            if (steamId == 0)
            {
                _logger.LogWarning("Steam API returned 0 (Not Found) for vanity URL: {VanityUrl}", vanityUrl);
                return Errors.Steam.VanityResolutonFailed;
            }

            return steamId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while resolving vanity URL {VanityUrl}.", vanityUrl);
            return Errors.Steam.VanityResolutonFailed;
        }
    }

    public async Task<ErrorOr<GameDetails>> GetRandomGameBySteamIdAsync(long steamId)
    {
        try
        {
            var ownedGames = await _steamClient.GetOwnedGames(steamId);
            if (ownedGames?.Games == null || ownedGames.Games.Count == 0)
            {
                return Errors.Steam.EmptyLibrary;
            }

            var appData = await GetRandomGameDataAsync(ownedGames);
            if (appData is null)
            {
                return Errors.Steam.SteamApiSuccessButCouldntGetAppData;
            }

            return new GameDetails
            {
                Id = appData.SteamAppId,
                Name = appData.Name,
                Description = _htmlSanitizer.Sanitize(appData.ShortDescription),
                HeaderImage = appData.HeaderImage
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting random game details for SteamID: {SteamId}", steamId);
            return Errors.Steam.SteamApiFailed;
        }
    }

    public async Task<ErrorOr<RandomGameResponse>> GetRandomSteamGameAsync(long steamId)
    {
        try
        {
            var ownedGames = await _steamClient.GetOwnedGames(steamId);
            if (ownedGames?.Games == null || ownedGames.Games.Count == 0)
            {
                return Errors.Steam.EmptyLibrary;
            }

            var appData = await GetRandomGameDataAsync(ownedGames);
            if (appData is null)
            {
                return Errors.Steam.SteamApiSuccessButCouldntGetAppData;
            }

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

    private async Task<AppData?> GetRandomGameDataAsync(SteamApiClient.Contracts.SteamApi.OwnedGames ownedGames)
    {
        int attempts = 0;
        var triedThisRequest = new HashSet<int>();

        while (attempts < MAX_ATTEMPTS && triedThisRequest.Count < ownedGames.Games.Count)
        {
            int index = Random.Shared.Next(0, ownedGames.Games.Count);
            var selectedGame = ownedGames.Games[index];

            if (!triedThisRequest.Add(selectedGame.AppId))
            {
                continue;
            }

            string cacheKey = $"dead_app:{selectedGame.AppId}";
            var isKnownDead = await _cache.GetStringAsync(cacheKey);
            if (isKnownDead != null)
            {
                _logger.LogDebug("Skipping known dead AppId {AppId} (Found in persistent blacklist cache).", selectedGame.AppId);
                continue;
            }

            var response = await _steamStoreClient.GetAppData(selectedGame.AppId);
            if (response?.Success == true && response.AppData != null)
            {
                return response.AppData;
            }

            attempts++;
            _logger.LogDebug("Steam store confirmed AppId {AppId} is unavailable. Blacklisting in cache...", selectedGame.AppId);

            var cacheOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(30) // TODO: Make configurable
            };
            await _cache.SetStringAsync(cacheKey, "false", cacheOptions);
        }

        return null;
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