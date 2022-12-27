using Microsoft.Extensions.Options;
using RandomSteamGame.Constants;
using RandomSteamGame.Exceptions;
using RandomSteamGame.Options;
using RandomSteamGame.SteamApiContracts;
using System.Net.Http.Headers;

namespace RandomSteamGame.Services;

public class SteamClient
{
    private readonly HttpClient _httpClient;
    private readonly MonkeyCacheOptions _monkeyCacheOptions;
    private readonly SteamOptions _steamOptions;

    public SteamClient(
        HttpClient httpClient, 
        IOptions<SteamOptions> steamOptions,
        IOptions<MonkeyCacheOptions> monkeyCacheOptions)
    {
        _httpClient = httpClient;
        _monkeyCacheOptions = monkeyCacheOptions.Value;
        _steamOptions = steamOptions.Value;
        
        _httpClient.BaseAddress = new Uri("https://api.steampowered.com");
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(RandomSteamGameConstants.UserAgent, RandomSteamGameConstants.Version));
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(RandomSteamGameConstants.UserAgentComment));
    }
    
    public async Task<OwnedGames> GetOwnedGames(Int64 steamId)
    {
        var output = 
            await _httpClient.MonkeyCacheGetAsync<OwnedGamesResponse>(
                $"/IPlayerService/GetOwnedGames/v0001/?key={_steamOptions.ApiKey}&steamid={steamId}&format=json",
                _monkeyCacheOptions.CacheDuration);

        return output!.Response;
    }
    
    public async Task<Int64> GetSteamIdFromVanityUrl(string vanityUrl)
    {
        var output =
            await _httpClient.MonkeyCacheGetAsync<ResolveVanityUrlResponse>(
                $"/ISteamUser/ResolveVanityURL/v0001/?key={_steamOptions.ApiKey}&vanityurl={vanityUrl}&format=json",
                _monkeyCacheOptions.CacheDuration);

        if (output.Response.Success != 1)
        {
            throw new VanityResolutionException("Unable to resolve vanity url");
        }

        return Int64.Parse(output.Response.SteamId!);
    }
}
