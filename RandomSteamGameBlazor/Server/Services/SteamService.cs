/*
 * Random Steam Game
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using ErrorOr;
using MapsterMapper;
using RandomSteamGameBlazor.Server.Features.Steam;
using SteamApiClient.Contracts.SteamStoreApi;
using SteamApiClient.HttpClients;
using ClientGame = SteamApiClient.Contracts.SteamApi.Game;
using ClientOwnedGames = SteamApiClient.Contracts.SteamApi.OwnedGames;
using SharedOwnedGamesResponse = RandomSteamGameBlazor.Shared.Contracts.RandomSteamGame.OwnedGamesResponse;

namespace RandomSteamGameBlazor.Server.Services;

public class SteamService : ISteamService
{
    private readonly ISteamClient _steamClient;
    private readonly ISteamStoreClient _steamStoreClient;
    private readonly IMapper _mapper;
    private readonly ILogger<SteamService> _logger;
    private const int MAX_ATTEMPTS = 3;

    public SteamService(
        ISteamClient steamClient,
        ISteamStoreClient steamStoreClient,
        IMapper mapper,
        ILogger<SteamService> logger)
    {
        _steamClient = steamClient;
        _steamStoreClient = steamStoreClient;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<ErrorOr<SharedOwnedGamesResponse>> GetOwnedGamesAsync(long steamId)
    {
        try
        {
            var ownedGames = await _steamClient.GetOwnedGames(steamId);
            if (!ownedGames.Games.Any())
            {
                return Errors.Steam.EmptyLibrary;
            }

            return _mapper.Map<SharedOwnedGamesResponse>((ownedGames, steamId));
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
            _logger.LogError(ex, "CRITICAL: Exception while resolving vanity URL {VanityUrl}. Message: {Message}", vanityUrl, ex.Message);
            return Errors.Steam.VanityResolutonFailed;
        }
    }

    public async Task<ErrorOr<Shared.Contracts.Steam.AppData>> GetRandomGameBySteamIdAsync(long steamId)
    {
        try
        {
            var ownedGames = await _steamClient.GetOwnedGames(steamId);

            if (!ownedGames.Games.Any())
            {
                return Errors.Steam.EmptyLibrary;
            }

            var response = await GetAppDataAsync(ownedGames);
            if (response?.AppData is null)
            {
                return Errors.Steam.SteamApiSuccessButCouldntGetAppData;
            }

            return _mapper.Map<Shared.Contracts.Steam.AppData>(response.AppData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting random app data for SteamID: {SteamId}", steamId);
            return Errors.Steam.SteamApiFailed;
        }
    }

    public async Task<ErrorOr<Shared.Contracts.RandomSteamGame.RandomGameResponse>>
        GetRandomSteamGameAsync(long steamId)
    {
        try
        {
            var ownedGames = await _steamClient.GetOwnedGames(steamId);
            if (ownedGames.Games.Count == 0)
            {
                return Errors.Steam.EmptyLibrary;
            }

            var ownedGamesResponse =
                _mapper.Map<SharedOwnedGamesResponse>((ownedGames, steamId));

            var randomGameResult = await GetRandomGameAsync(ownedGames);
            if (randomGameResult.IsError)
            {
                return randomGameResult.Errors;
            }

            return _mapper.Map<
                Shared.Contracts.RandomSteamGame.RandomGameResponse>(
                (ownedGamesResponse, randomGameResult.Value));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error getting random Steam game for SteamID: {SteamId}",
                steamId);

            return Errors.Steam.SteamApiFailed;
        }
    }

    private async Task<ErrorOr<Shared.Contracts.Steam.AppData>>
    GetRandomGameAsync(ClientOwnedGames ownedGames)
    {
        if (ownedGames.Games.Count == 0)
        {
            return Errors.Steam.EmptyLibrary;
        }

        var response = await GetAppDataAsync(ownedGames);

        if (response?.AppData is null)
        {
            return Errors.Steam.SteamApiSuccessButCouldntGetAppData;
        }

        return _mapper.Map<Shared.Contracts.Steam.AppData>(
            response.AppData);
    }

    private async Task<AppDetailsResponse?> GetAppDataAsync(ClientOwnedGames ownedGames)
    {
        int attempts = 0;
        var response = new AppDetailsResponse();
        while (!response.Success)
        {
            ClientGame game = ownedGames.Games[Random.Shared.Next(0, ownedGames.GameCount)];
            response = await _steamStoreClient.GetAppData(game.AppId);

            if (!response.Success)
            {
                attempts++;
                if (attempts >= MAX_ATTEMPTS)
                {
                    return null;
                }

                _logger.LogWarning("Unable to get app details for {AppId}. Attempt: {attempt}", game.AppId, attempts);
            }
            else
            {
                response.AppData?.SteamAppId = game.AppId;
            }
        }

        return response;
    }
}