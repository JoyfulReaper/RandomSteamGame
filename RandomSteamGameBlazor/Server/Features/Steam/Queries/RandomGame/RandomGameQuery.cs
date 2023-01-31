using ErrorOr;
using MediatR;
using SteamApiClient.Contracts.SteamStoreApi;

namespace RandomSteamGameBlazor.Server.Features.Steam.Queries.RandomGame;

public record RandomGameQuery(long SteamId) : IRequest<ErrorOr<AppData>>;
