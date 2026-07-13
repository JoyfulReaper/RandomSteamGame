using RandomSteamGame.Events;
using RandomSteamGame.Shared.Contracts;
using SteamApiClient.Services;

namespace RandomSteamGame.Services;

public sealed record RandomGamePickResult(
    GameDetails Game,
    OwnedGamesCacheInfo Cache,
    int EligibleGameCount,
    int LibraryGameCount,
    GamePickTimings Timings);
