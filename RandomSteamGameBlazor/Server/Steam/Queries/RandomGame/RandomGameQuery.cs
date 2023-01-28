using MediatR;
using SteamApiClient.Contracts.SteamStoreApi;

namespace RandomSteamGameBlazor.Server.Steam.Queries.RandomGame;

public record RandomGameQuery(
    long SteamId) : IRequest<AppData>;
