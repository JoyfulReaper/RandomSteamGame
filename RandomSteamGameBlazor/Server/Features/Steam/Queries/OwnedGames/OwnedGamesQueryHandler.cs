using ErrorOr;
using MapsterMapper;
using MediatR;
using RandomSteamGameBlazor.Shared.Contracts.RandomSteamGame;
using SteamApiClient.HttpClients;

namespace RandomSteamGameBlazor.Server.Features.Steam.Queries.OwnedGames;

public class OwnedGamesQueryHandler : IRequestHandler<OwnedGamesQuery, ErrorOr<OwnedGamesResponse>>
{
    private readonly SteamClient _steamClient;
    private readonly IMapper _mapper;

    public OwnedGamesQueryHandler(
        SteamClient steamClient,
        IMapper mapper)
    {
        _steamClient = steamClient;
        _mapper = mapper;
    }
    
    public async Task<ErrorOr<OwnedGamesResponse>> Handle(OwnedGamesQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var ownedGames = await _steamClient.GetOwnedGames(request.steamId);
            if(!ownedGames.Games.Any())
            {
                return Errors.Steam.EmptyLibrary;
            }

            var output = _mapper.Map<OwnedGamesResponse>((ownedGames, request.steamId));
            return output;
        }
        catch (Exception ex)
        {
            return Errors.Steam.SteamApiFailed;
        }
    }
}
