using SteamApiClient.Contracts.SteamApi;
using SteamApiClient.Services;

namespace SteamApiClient.HttpClients;

public sealed record OwnedGamesResult(
    OwnedGames OwnedGames,
    OwnedGamesCacheInfo Cache);
