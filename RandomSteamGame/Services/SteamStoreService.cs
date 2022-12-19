using RandomSteamGame.Constants;
using RandomSteamGame.SteamStoreApiContracts;
using System.Net.Http.Headers;
using System.Text.Json;

namespace RandomSteamGame.Services;

public class SteamStoreService
{
    private readonly HttpClient _httpClient;

    public SteamStoreService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri("https://store.steampowered.com");
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(RandomSteamGameConstants.UserAgent, RandomSteamGameConstants.Version));
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(RandomSteamGameConstants.UserAgentComment));
    }

    public async Task<AppDetailsResponse> GetAppData(int appId)
    {
        using var response = await _httpClient.GetStreamAsync($"/api/appdetails?appids={appId}");

        var withoutRoot = JsonDocument.Parse(response).RootElement.GetProperty(appId.ToString());
        var output = JsonSerializer.Deserialize<AppDetailsResponse>(withoutRoot);

        return output!;
    }
}
