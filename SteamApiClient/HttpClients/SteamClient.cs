using System.Net.Http.Headers;
using SteamApiClient.Contracts.SteamApi;
using SteamApiClient.Exceptions;
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

    public async Task<OwnedGames> GetOwnedGames(long steamId, bool includeAppInfo = true, bool includePlayedFreeGames = true)
    {
        var cachedGamesString = await _cache.GetStringAsync($"owned_{steamId}_{includeAppInfo}_{includePlayedFreeGames}");
        if (cachedGamesString is null)
        {
            var arguments = string.Empty;
            if(includeAppInfo)
            {
                arguments += "&include_appinfo=1";
            }

            if (includePlayedFreeGames)
            {
                arguments += "&include_played_free_games=1";
            }

                var ownedGamesString =
            await _httpClient.GetStringAsync(
                $"/IPlayerService/GetOwnedGames/v0001/?key={_steamOptions.ApiKey}&steamid={steamId}&format=json{arguments}");

            await _cache.SetStringAsync($"owned_{steamId}_{includeAppInfo}_{includePlayedFreeGames}", ownedGamesString, _cacheEntryOptions);
            var ownedGames = 
                JsonSerializer.Deserialize<OwnedGamesResponse>(ownedGamesString, new JsonSerializerOptions(JsonSerializerDefaults.Web));

            return ownedGames!.Response;
        }

        var cachedGames =
            JsonSerializer.Deserialize<OwnedGamesResponse>(cachedGamesString, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return cachedGames!.Response;
    }

    public async Task<long> GetSteamIdFromVanityUrl(string vanityUrl)
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
            return long.Parse(output.Response.SteamId!);
        }

        var cachedSteamId = 
            JsonSerializer.Deserialize<ResolveVanityUrlResponse>(cachedSteamIdString, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        
        if (cachedSteamId is null)
        {
            throw new CacheException("Failed to deserialize cached data");
        }
        
        return long.Parse(cachedSteamId.Response.SteamId);
    }
}