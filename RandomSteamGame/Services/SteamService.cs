using RandomSteamGame.Exceptions;
using SteamApiClient.Contracts.SteamApi;
using SteamApiClient.Contracts.SteamStoreApi;
using SteamApiClient.HttpClients;

namespace RandomSteamGame.Services;


public class SteamService
{
    private readonly SteamClient _steamClient;
    private readonly SteamStoreClient _steamStoreClient;
    private readonly ILogger<SteamService> _logger;

    public SteamService(
        SteamClient steamClient,
        SteamStoreClient steamStoreClient,
        ILogger<SteamService> logger)
    {
        _steamClient = steamClient;
        _steamStoreClient = steamStoreClient;
        _logger = logger;
    }

    public async Task<Data> GetRandomGame(Int64 steamId)
    {
        OwnedGames gamesOwned;
        try
        {
            gamesOwned = await _steamClient.GetOwnedGames(steamId);
        }
        catch (Exception)
        {
            throw new SteamServiceException($"An error occurred while trying to get the game list for Steam Id: {steamId}. Please verify your Steam ID and try again. " +
                $"Please note, your Steam Profile must be public for this to work.");
        }

        int attempts = 0;
        Game game = new();
        AppDetailsResponse response = new();
        while (!response.Success)
        {
            game = gamesOwned.Games[Random.Shared.Next(0, gamesOwned.GameCount - 1)];
            response = await _steamStoreClient.GetAppData(game.AppId);

            if (!response.Success)
            {
                attempts++;
                if (attempts >= 3)
                {
                    throw new SteamServiceException($"We were unable to find any games for you after 3 attempts. Aborting.");
                }

                _logger.LogWarning("Unable to get app details for {AppId}", game.AppId);
            }
        }

        if (response.Data is null)
        {
            throw new SteamServiceException("Response was successfull, but data was missing.");
        }

        return response.Data;
    }

    public async Task<Int64> GetSteamIdFromVanityUrl(string vanityUrl)
    {
        return await _steamClient.GetSteamIdFromVanityUrl(vanityUrl);
    }
}
