/*
 * Random Steam Game
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SteamApiClient.Contracts.SteamApi;
using SteamApiClient.Exceptions;
using SteamApiClient.Settings;
using System.Net.Http.Headers;
using System.Text.Json;

namespace SteamApiClient.HttpClients;

public class SteamClient : ISteamClient
{
    private readonly HttpClient _httpClient;
    private readonly IDistributedCache _cache;
    private readonly SteamOptions _steamOptions;
    private readonly ILogger _logger;

    private readonly CacheSettings _cacheSettings;

    private const int STEAM_VANITY_SUCCESS = 1;
    private const int STEAM_VANITY_NO_MATCH = 42;
    private const string STAEM_VANITY_NOTFOUND = "NOT_FOUND";

    public SteamClient(
        HttpClient httpClient,
        IOptions<SteamOptions> steamOptions,
        IDistributedCache cache,
        ILogger<SteamClient> logger,
        IOptions<CacheSettings> cacheSettings)
    {
        _httpClient = httpClient;
        _cache = cache;
        _cacheSettings = cacheSettings.Value;
        _steamOptions = steamOptions.Value;
        _logger = logger;

        _httpClient.BaseAddress = new Uri("https://api.steampowered.com");
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(SteamClientConstants.UserAgent, SteamClientConstants.Version));
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(SteamClientConstants.UserAgentComment));
    }

    public async Task<OwnedGames> GetOwnedGames(long steamId, bool includeAppInfo = true, bool includePlayedFreeGames = true)
    {
        var cachedGamesString = await _cache.GetStringAsync(
            $"owned_{steamId}_{includeAppInfo}_{includePlayedFreeGames}");

        if (cachedGamesString is null)
        {
            var arguments = string.Empty;
            if (includeAppInfo)
            {
                arguments += "&include_appinfo=1";
            }

            if (includePlayedFreeGames)
            {
                arguments += "&include_played_free_games=1";
            }

            var ownedGamesString =
                await _httpClient.GetStringAsync(
                $"/IPlayerService/GetOwnedGames/v0001/?key={_steamOptions.ApiKey}&steamid={steamId}&format=json{arguments}&l=english"); // TODO: Add support for other languages

            var successOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow =
                    TimeSpan.FromMinutes(_cacheSettings.OwnedGames.AbsoluteMinutes)
            };

            await _cache.SetStringAsync($"owned_{steamId}_{includeAppInfo}_{includePlayedFreeGames}", ownedGamesString, successOptions);
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
        var cacheKey = $"vanity_{vanityUrl}";
        var cachedResponse = await _cache.GetStringAsync(cacheKey);

        if (cachedResponse is not null)
        {
            if (cachedResponse == STAEM_VANITY_NOTFOUND)
                return 0;

            return long.Parse(cachedResponse);
        }

        var encodedVanity = Uri.EscapeDataString(vanityUrl);
        var responseString = await _httpClient.GetStringAsync(
            $"/ISteamUser/ResolveVanityURL/v0001/?key={_steamOptions.ApiKey}&vanityurl={encodedVanity}&format=json");

        var response = JsonSerializer.Deserialize<ResolveVanityUrlResponse>(responseString,
             new JsonSerializerOptions(JsonSerializerDefaults.Web));

        if (response?.Response?.Success == STEAM_VANITY_NO_MATCH)
        {
            _logger.LogInformation(
                "Steam vanity URL not found: {VanityUrl}",
                vanityUrl);

            var notFoundOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow =
                    TimeSpan.FromMinutes(_cacheSettings.VanityNotFound.AbsoluteMinutes)
            };

            await _cache.SetStringAsync(
                cacheKey,
                STAEM_VANITY_NOTFOUND,
                notFoundOptions
            );

            return 0;
        }

        if (response?.Response?.Success != STEAM_VANITY_SUCCESS)
        {
            throw new VanityResolutionException(
                $"Steam API returned unexpected success code: {response?.Response?.Success}");
        }

        var steamId = response.Response.SteamId!;

        var successOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow =
                TimeSpan.FromMinutes(_cacheSettings.VanitySuccess.AbsoluteMinutes)
        };

        await _cache.SetStringAsync(
            cacheKey,
            steamId,
            successOptions
        );

        return long.Parse(steamId);
    }
}