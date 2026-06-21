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
using System.Collections.Concurrent;
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

    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    private const int STEAM_VANITY_SUCCESS = 1;
    private const int STEAM_VANITY_NO_MATCH = 42;
    private const string STEAM_VANITY_NOTFOUND = "NOT_FOUND";

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
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue(SteamClientConstants.UserAgent, SteamClientConstants.Version));
        _httpClient.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue(SteamClientConstants.UserAgentComment));
    }

    public async Task<OwnedGames> GetOwnedGames(
        long steamId,
        bool includeAppInfo = true,
        bool includePlayedFreeGames = true,
        CancellationToken ct = default)
    {
        var cacheKey = $"owned_{steamId}_{includeAppInfo}_{includePlayedFreeGames}";

        var cached = await _cache.GetAsync<OwnedGames>(cacheKey);
        if (cached is not null)
            return cached;

        var gate = _locks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            cached = await _cache.GetAsync<OwnedGames>(cacheKey);
            if (cached is not null)
                return cached;

            var args = string.Empty;
            if (includeAppInfo)
                args += "&include_appinfo=1";

            if (includePlayedFreeGames)
                args += "&include_played_free_games=1";

            var url =
                $"/IPlayerService/GetOwnedGames/v0001/" +
                $"?key={_steamOptions.ApiKey}" +
                $"&steamid={steamId}" +
                $"&format=json{args}&l=english"; // TODO: Support other langauges

            using var response = await _httpClient.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);

            var parsed = JsonSerializer.Deserialize<OwnedGamesResponse>(json, _jsonOptions)
                         ?? throw new Exception("Invalid Steam response");

            var result = parsed.Response;

            await _cache.SetAsync(cacheKey, result, _cacheSettings.OwnedGames);

            return result;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<long> GetSteamIdFromVanityUrl(
        string vanityUrl,
        CancellationToken ct = default)
    {
        var cacheKey = $"vanity_{vanityUrl}";
        var cached = await _cache.GetAsync<string>(cacheKey);

        if (cached is not null)
        {
            if (cached == STEAM_VANITY_NOTFOUND)
                return 0;

            return long.Parse(cached);
        }

        var encoded = Uri.EscapeDataString(vanityUrl);
        var url =
            $"/ISteamUser/ResolveVanityURL/v0001/" +
            $"?key={_steamOptions.ApiKey}" +
            $"&vanityurl={encoded}&format=json";

        using var response = await _httpClient.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);

        var parsed = JsonSerializer.Deserialize<ResolveVanityUrlResponse>(json, _jsonOptions)
                     ?? throw new VanityResolutionException("Invalid response");

        var r = parsed.Response;

        if (r.Success == STEAM_VANITY_NO_MATCH)
        {
            await _cache.SetAsync(cacheKey, STEAM_VANITY_NOTFOUND, _cacheSettings.VanityNotFound);
            return 0;
        }

        if (r.Success != STEAM_VANITY_SUCCESS)
            throw new VanityResolutionException("Unexpected Steam response");

        await _cache.SetAsync(cacheKey, r.SteamId!, _cacheSettings.VanitySuccess);

        return long.Parse(r.SteamId!);
    }
}