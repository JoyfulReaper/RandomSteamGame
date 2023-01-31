using ErrorOr;
using MediatR;
using SteamApiClient.Contracts.SteamApi;
using SteamApiClient.Contracts.SteamStoreApi;
using SteamApiClient.HttpClients;

namespace RandomSteamGameBlazor.Server.Features.Steam.Queries.RandomGame;

public class RandomGameQueryHandler : IRequestHandler<RandomGameQuery, ErrorOr<AppData>>
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

    public async Task<ErrorOr<AppData>> Handle(RandomGameQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var ownedGames = await _steamClient.GetOwnedGames(request.SteamId);
            
            if (!ownedGames.Games.Any())
            {
                return Errors.Steam.EmptyLibrary;
            }

            AppDetailsResponse? response = await GetAppData(ownedGames);
            if (response?.AppData is null)
            {
                return Errors.Steam.SteamApiSuccessButCouldntGetAppData;
            }
            
            return response.AppData;
        }
        catch (Exception)
        {
            return Errors.Steam.SteamApiFailed;
        }
    }

    private async Task<AppDetailsResponse?> GetAppData(SteamApiClient.Contracts.SteamApi.OwnedGames ownedGames)
    {
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
                    return null;
                }

                _logger.LogWarning("Unable to get app details for {AppId}. Attempt: {attempt}", game.AppId, attempts);
            }
        }

        return response;
    }
}