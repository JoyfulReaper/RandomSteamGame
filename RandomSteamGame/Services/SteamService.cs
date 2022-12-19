using Microsoft.Extensions.Options;
using RandomSteamGame.Constants;
using RandomSteamGame.Options;
using RandomSteamGame.SteamApiContracts;
using System.Net.Http.Headers;
using System.Text.Json;

namespace RandomSteamGame.Services;

public class SteamService
{
    private readonly HttpClient _httpClient;
    private readonly SteamOptions _steamOptions;

    public SteamService(HttpClient httpClient, IOptions<SteamOptions> steamOptions)
    {
        _httpClient = httpClient;
        _steamOptions = steamOptions.Value;
        _httpClient.BaseAddress = new Uri("https://api.steampowered.com");
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(RandomSteamGameConstants.UserAgent, RandomSteamGameConstants.Version));
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(RandomSteamGameConstants.UserAgentComment));
    }
    
    public async Task<OwnedGameResponse> GetOwnedGames(Int64 steamId)
    {
        using var response = 
            await _httpClient.GetAsync($"/IPlayerService/GetOwnedGames/v0001/?key={_steamOptions.ApiKey}&steamid={steamId}&format=json");
        response.EnsureSuccessStatusCode();
        
        //var test = await response.Content.ReadAsStringAsync();
        
        var withoutRoot = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("response").ToString();
        var output = JsonSerializer.Deserialize<OwnedGameResponse>(withoutRoot);
        //var output2 = await response.Content.ReadFromJsonAsync<Test>(); --- Want to do it like this.....
        return output;
    }
    
}
