using SteamApiClient.Contracts.SteamApi;

namespace SteamApiClient.HttpClients;
public interface ISteamClient
{
    Task<OwnedGames> GetOwnedGames(long steamId, bool includeAppInfo = true, bool includePlayedFreeGames = true);
    Task<long> GetSteamIdFromVanityUrl(string vanityUrl);
}