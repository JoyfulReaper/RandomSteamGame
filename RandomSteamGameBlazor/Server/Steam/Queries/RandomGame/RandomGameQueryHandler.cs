using MediatR;
using SteamApiClient.Contracts.SteamApi;
using SteamApiClient.Contracts.SteamStoreApi;
using SteamApiClient.HttpClients;

namespace RandomSteamGameBlazor.Server.Steam.Queries.RandomGame;

public class RandomGameQueryHandler : IRequestHandler<RandomGameQuery, AppData>
{
    private readonly SteamClient _steamClient;
    private readonly SteamStoreClient _steamStoreClient;
    private readonly ILogger<RandomGameQueryHandler> _logger;
    private const int MaxAttempts = 3;

    public RandomGameQueryHandler(
        SteamClient steamClient,
        SteamStoreClient steamStoreClient,
        ILogger<RandomGameQueryHandler> logger)
	{
        _steamClient = steamClient;
        _steamStoreClient = steamStoreClient;
        _logger = logger;
    }

    public async Task<AppData> Handle(RandomGameQuery request, CancellationToken cancellationToken)
    {
        OwnedGames ownedGames;
        try
        {
            ownedGames = await _steamClient.GetOwnedGames(request.SteamId);
        }
        catch (Exception)
        {
            throw new SteamException($"An error occurred while trying to get the game list for Steam Id: {request.SteamId}. Please verify your Steam ID and try again. " +
                $"Please note, your Steam Profile must be public for this to work.");
        }

        if(!ownedGames.Games.Any())
        {
            throw new SteamException("You do not own any games on Steam. Please add some games to your library and try again.");
        }

        int attempts = 0;
        Game game;
        AppDetailsResponse response = new();
        while (!response.Success)
        {
            game = ownedGames.Games[Random.Shared.Next(0, ownedGames.GameCount - 1)];
            response = await _steamStoreClient.GetAppData(game.AppId);

            if (!response.Success)
            {
                attempts++;
                if (attempts >= MaxAttempts)
                {
                    throw new SteamException($"We were unable to find any games for you after {MaxAttempts} attempts. Aborting.");
                }

                _logger.LogWarning("Unable to get app details for {AppId}. Attempt: {attempt}", game.AppId, attempts);
            }
        }

        return response?.AppData ?? 
            throw new SteamException("Response was successful, but data was missing.");
    }
}