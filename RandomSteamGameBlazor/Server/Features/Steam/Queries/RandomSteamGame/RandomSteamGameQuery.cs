using ErrorOr;
using MediatR;
using RandomSteamGameBlazor.Shared.Contracts.RandomSteamGame;

namespace RandomSteamGameBlazor.Server.Features.Steam.Queries.RandomSteamGame;

public record RandomSteamGameQuery(long SteamId) : IRequest<ErrorOr<RandomGameResponse>>;