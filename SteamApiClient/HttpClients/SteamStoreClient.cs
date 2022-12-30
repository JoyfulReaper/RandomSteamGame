using System.Net.Http.Headers;
using System.Net.Http;
using SteamApiClient.Contracts.SteamStoreApi;
using System.Text.Json;

namespace SteamApiClient.HttpClients;

public class SteamStoreClient
{
    private readonly HttpClient _httpClient;

    public SteamStoreClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri("https://store.steampowered.com");
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(SteamClientConstants.UserAgent, SteamClientConstants.Version));
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(SteamClientConstants.UserAgentComment));
    }

    public async Task<AppDetailsResponse> GetAppData(int appId)
    {
        var jsonResponse = await _httpClient.GetStringAsync($"/api/appdetails?appids={appId}");

        var withoutRoot = JsonDocument.Parse(jsonResponse).RootElement.GetProperty(appId.ToString());
        var output = JsonSerializer.Deserialize<AppDetailsResponse>(withoutRoot);

        return output!;
    }
}
