/*
 * Random Steam Game
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SteamApiClient.Contracts.SteamApi;
using SteamApiClient.Exceptions;
using SteamApiClient.Services;
using SteamApiClient.Settings;
using System.Net.Http.Headers;
using System.Text.Json;


namespace SteamApiClient.HttpClients;

public class SteamClient : ISteamClient
{
    private readonly HttpClient _httpClient;
    private readonly SteamOptions _steamOptions;
    private readonly ILogger _logger;

    private readonly ICacheService _cache;
    private readonly CacheSettings _cacheSettings;

    private const int STEAM_VANITY_SUCCESS = 1;
    private const int STEAM_VANITY_NO_MATCH = 42;
    private const string STAEM_VANITY_NOTFOUND = "NOT_FOUND";

    private static readonly JsonSerializerOptions _jsonOptions =
        new(JsonSerializerDefaults.Web);

    public SteamClient(
        HttpClient httpClient,
        IOptions<SteamOptions> steamOptions,
        ICacheService cache,
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
        var cacheKey = $"owned_{steamId}_{includeAppInfo}_{includePlayedFreeGames}";

        var cached = await _cache.GetAsync<OwnedGames>(cacheKey);
        if (cached is not null)
        {
            return cached;
        }

        var arguments = string.Empty;
        if (includeAppInfo)
        {
            arguments += "&include_appinfo=1";
        }

        if (includePlayedFreeGames)
        {
            arguments += "&include_played_free_games=1";
        }

        var jsonResponse =
            await _httpClient.GetStringAsync(
                $"/IPlayerService/GetOwnedGames/v0001/?key={_steamOptions.ApiKey}&steamid={steamId}&format=json{arguments}&l=english");

        var response = JsonSerializer.Deserialize<OwnedGamesResponse>(jsonResponse, _jsonOptions)
            ?? throw new Exception("Failed to deserialize Steam response");

        await _cache.SetAsync(
            cacheKey,
            response.Response,
            _cacheSettings.OwnedGames);

        return response.Response;
    }

    public async Task<long> GetSteamIdFromVanityUrl(string vanityUrl)
    {
        var cacheKey = $"vanity_{vanityUrl}";
        var cached = await _cache.GetAsync<string>(cacheKey);

        if (cached is not null)
        {
            if (cached == STAEM_VANITY_NOTFOUND)
                return 0;

            return long.Parse(cached);
        }

        var encodedVanity = Uri.EscapeDataString(vanityUrl);

        var json = await _httpClient.GetStringAsync(
            $"/ISteamUser/ResolveVanityURL/v0001/?key={_steamOptions.ApiKey}&vanityurl={encodedVanity}&format=json");

        var response = JsonSerializer.Deserialize<ResolveVanityUrlResponse>(json, _jsonOptions)
            ?? throw new VanityResolutionException("Invalid response");

        if (response.Response.Success == STEAM_VANITY_NO_MATCH)
        {
            await _cache.SetAsync(
                cacheKey,
                STAEM_VANITY_NOTFOUND,
                _cacheSettings.VanityNotFound);

            return 0;
        }

        if (response.Response.Success != STEAM_VANITY_SUCCESS)
            throw new VanityResolutionException("Unexpected Steam response");

        await _cache.SetAsync(
            cacheKey,
            response.Response.SteamId!,
            _cacheSettings.VanitySuccess);

        return long.Parse(response.Response.SteamId!);
    }
}