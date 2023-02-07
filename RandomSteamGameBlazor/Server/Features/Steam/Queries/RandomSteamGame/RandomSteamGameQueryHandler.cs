using ErrorOr;
using MapsterMapper;
using MediatR;
using RandomSteamGameBlazor.Server.Features.Steam.Queries.OwnedGames;
using RandomSteamGameBlazor.Server.Features.Steam.Queries.RandomGame;
using RandomSteamGameBlazor.Shared.Contracts.RandomSteamGame;
using SteamApiClient.Contracts.SteamStoreApi;

namespace RandomSteamGameBlazor.Server.Features.Steam.Queries.RandomSteamGame;

public class RandomSteamGameQueryHandler : IRequestHandler<RandomSteamGameQuery, ErrorOr<RandomGameResponse>>
{
    private readonly ISender _mediator;
    private readonly IMapper _mapper;

    public RandomSteamGameQueryHandler(
        ISender mediator,
        IMapper mapper)
    {
        _mediator = mediator;
        _mapper = mapper;
    }

    public async Task<ErrorOr<RandomGameResponse>> Handle(RandomSteamGameQuery request, CancellationToken cancellationToken)
    {
        var ownedGames = await _mediator.Send(new OwnedGamesQuery(request.SteamId), cancellationToken);
        if(ownedGames.IsError)
        {
            return ownedGames.Errors;
        }

        var randomGame = await _mediator.Send(new RandomGameQuery(request.SteamId), cancellationToken);
        if(randomGame.IsError)
        {
            return randomGame.Errors;
        }

        var response = _mapper.Map<RandomGameResponse>((ownedGames.Value, randomGame.Value));

        return response;
    }
}