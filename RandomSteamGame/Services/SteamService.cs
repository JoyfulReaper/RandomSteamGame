/*
 * Random Steam Game
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using ErrorOr;
using Microsoft.Extensions.Options;
using RandomSteamGame.Common.Errors;
using RandomSteamGame.Services.Interfaces;
using RandomSteamGame.Shared.Contracts;
using SteamApiClient.Contracts.SteamStoreApi;
using SteamApiClient.HttpClients;
using SteamApiClient.Services;
using SteamApiClient.Settings;

namespace RandomSteamGame.Services;

public class SteamService : ISteamService
{
    private readonly ISteamClient _steamClient;
    private readonly ISteamStoreClient _steamStoreClient;
    private readonly ILogger<SteamService> _logger;
    private readonly IHtmlSanitizerService _htmlSanitizer;
    private readonly SteamClientApiOptions _steamOptions;
    private readonly ICacheService _cacheService;

    private const int MAX_ATTEMPTS = 3;

    public record AppDataWrapper(AppData? Data);

    public SteamService(
        ISteamClient steamClient,
        ISteamStoreClient steamStoreClient,
        IOptions<SteamClientApiOptions> steamOptions,
        IHtmlSanitizerService htmlSanitizerService,
        ICacheService cacheService,
        ILogger<SteamService> logger)
    {
        _htmlSanitizer = htmlSanitizerService;
        _steamClient = steamClient;
        _steamStoreClient = steamStoreClient;
        _logger = logger;
        _cacheService = cacheService;
        _steamOptions = steamOptions.Value;
    }

    public async Task<ErrorOr<OwnedGamesResponse>> GetOwnedGamesAsync(long steamId)
    {
        var ownedGames = await _steamClient.GetOwnedGames(steamId);

        if (ownedGames?.Games == null || ownedGames.Games.Count == 0)
        {
            return Errors.Steam.EmptyLibrary;
        }

        return MapToOwnedGamesResponse(steamId, ownedGames);
    }

    public async Task RefreshOwnedGamesCacheAsync()
    {
        // You can keep the CacheService injected here, or add a method to ISteamClient
        await _cacheService.InvalidateByTagAsync("owned_games");
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
                Description = _htmlSanitizer.Sanitize(appData.AboutTheGame),
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
        var policy = _steamOptions.Cache.AppDetails;

        while (attempts < MAX_ATTEMPTS && triedThisRequest.Count < ownedGames.Games.Count)
        {
            var selectedGame = ownedGames.Games[Random.Shared.Next(0, ownedGames.Games.Count)];
            if (!triedThisRequest.Add(selectedGame.AppId)) continue;

            string cacheKey = $"app_data:{selectedGame.AppId}";

            var cachedResult = await _cacheService.GetOrCreateAsync(
                cacheKey,
                async ct =>
                {
                    var response = await _steamStoreClient.GetAppData(selectedGame.AppId, tags: new[] { "app_data" }, ct);

                    return response?.Success == true
                        ? new AppDataWrapper(response.AppData)
                        : new AppDataWrapper(null);
                },
                policy,
                tags: new[] { "app_data" }
            );

            if (cachedResult.Data != null)
            {
                return cachedResult.Data;
            }

            attempts++;
            _logger.LogDebug("Steam store confirmed AppId {AppId} is unavailable.", selectedGame.AppId);
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