using SteamApiClient.Contracts.SteamStoreApi;

namespace SteamApiClient.HttpClients;
public interface ISteamStoreClient
{
    Task<AppDetailsResponse> GetAppData(int appId);
}