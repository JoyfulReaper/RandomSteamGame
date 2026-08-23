using SteamApiClient.Contracts.SteamApi;

namespace RandomSteamGame.Services.Interfaces;

public interface ISteamDeckCompatibilityProvider
{
    Task<IReadOnlyDictionary<int, SteamDeckCompatibilityCategory>>
        GetSteamDeckCompatibilityAsync(
            IEnumerable<int> appIds,
            CancellationToken ct = default);
}
