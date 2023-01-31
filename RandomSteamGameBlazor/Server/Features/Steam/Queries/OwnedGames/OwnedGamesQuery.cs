using ErrorOr;
using MediatR;
using RandomSteamGameBlazor.Shared.Contracts.RandomSteamGame;

namespace RandomSteamGameBlazor.Server.Features.Steam.Queries.OwnedGames;

public record OwnedGamesQuery(long steamId) : IRequest<ErrorOr<OwnedGamesResponse>>;
