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
    
    public async Task<OwnedGames> GetOwnedGames(Int64 steamId)
    {
        var output = 
            await _httpClient.GetFromJsonAsync<OwnedGamesResponse>($"/IPlayerService/GetOwnedGames/v0001/?key={_steamOptions.ApiKey}&steamid={steamId}&format=json");
        
        return output.Response;
    }
    
}
