using System.Net.Http.Headers;
using System.Net.Http;
using SteamApiClient.Contracts.SteamApi;
using System.Net.Http.Json;
using SteamApiClient.Exceptions;
using SteamApiClient.Options;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace SteamApiClient.HttpClients;

public class SteamClient
{
    private readonly HttpClient _httpClient;
    private readonly IDistributedCache _cache;
    private readonly DistributedCacheEntryOptions _cacheEntryOptions;
    private readonly SteamOptions _steamOptions;

    public SteamClient(
        HttpClient httpClient,
        IOptions<SteamOptions> steamOptions,
        IDistributedCache cache,
        IOptions<DistributedCacheEntryOptions> cacheEntryOptions)
    {
        _httpClient = httpClient;
        _cache = cache;
        _cacheEntryOptions = cacheEntryOptions.Value;
        _steamOptions = steamOptions.Value;

        _httpClient.BaseAddress = new Uri("https://api.steampowered.com");
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(SteamClientConstants.UserAgent, SteamClientConstants.Version));
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(SteamClientConstants.UserAgentComment));
    }

    public async Task<OwnedGames> GetOwnedGames(Int64 steamId)
    {
        var cachedGamesString = await _cache.GetStringAsync($"owned_{steamId}");
        if (cachedGamesString is null)
        {
            var ownedGamesString =
            await _httpClient.GetStringAsync(
                $"/IPlayerService/GetOwnedGames/v0001/?key={_steamOptions.ApiKey}&steamid={steamId}&format=json");

            await _cache.SetStringAsync($"owned_{steamId}", ownedGamesString, _cacheEntryOptions);
            var ownedGames = 
                JsonSerializer.Deserialize<OwnedGamesResponse>(ownedGamesString, new JsonSerializerOptions(JsonSerializerDefaults.Web));

            return ownedGames!.Response;
        }

        var cachedGames =
            JsonSerializer.Deserialize<OwnedGamesResponse>(cachedGamesString, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return cachedGames!.Response;
    }

    public async Task<Int64> GetSteamIdFromVanityUrl(string vanityUrl)
    {
        var cachedSteamIdString = await _cache.GetStringAsync($"vanity_{vanityUrl}");
        if (cachedSteamIdString is null)
        {
            var outputString =
            await _httpClient.GetStringAsync(
                $"/ISteamUser/ResolveVanityURL/v0001/?key={_steamOptions.ApiKey}&vanityurl={vanityUrl}&format=json");

            var output =
                JsonSerializer.Deserialize<ResolveVanityUrlResponse>(outputString, new JsonSerializerOptions(JsonSerializerDefaults.Web));

            if (output!.Response.Success != 1)
            {
                throw new VanityResolutionException("Unable to resolve vanity url");
            }

            await _cache.SetStringAsync($"vanity_{vanityUrl}", outputString, _cacheEntryOptions);
            return Int64.Parse(output.Response.SteamId!);
        }

        var cachedSteamId = 
            JsonSerializer.Deserialize<ResolveVanityUrlResponse>(cachedSteamIdString, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return Int64.Parse(cachedSteamId.Response.SteamId);
    }
}
