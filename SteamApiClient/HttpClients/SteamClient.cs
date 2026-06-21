/*
 * Random Steam Game
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

// TODO: replace Concurrent dictitonary with IMemoryCache with entry eviction
// TODO: Verify logging for cache misses cnad API falires
// TODO: Retry policy

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SteamApiClient.Contracts.SteamApi;
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
    private readonly ILogger<SteamClient> _logger;

    private readonly ICacheService _cache;
    private readonly CacheSettings _cacheSettings;

    private static readonly JsonSerializerOptions _jsonOptions =
        new(JsonSerializerDefaults.Web);

    private static readonly ConcurrentDictionary<string, Lazy<Task<object?>>> _inflight = new();

    private const int STEAM_VANITY_SUCCESS = 1;
    private const int STEAM_VANITY_NO_MATCH = 42;

    public SteamClient(
        HttpClient httpClient,
        IOptions<SteamOptions> steamOptions,
        ICacheService cache,
        ILogger<SteamClient> logger,
        IOptions<CacheSettings> cacheSettings)
    {
        _httpClient = httpClient;
        _steamOptions = steamOptions.Value;
        _cache = cache;
        _logger = logger;
        _cacheSettings = cacheSettings.Value;

        _httpClient.BaseAddress = new Uri("https://api.steampowered.com");

        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));

        _httpClient.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue(SteamClientConstants.UserAgent, SteamClientConstants.Version));

        _httpClient.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue(SteamClientConstants.UserAgentComment));
    }

    private Task<T> SingleFlight<T>(string key, Func<Task<T>> factory)
    {
        var lazy = _inflight.GetOrAdd(
            key,
            _ => new Lazy<Task<object?>>(
                () => Wrap(factory),
                LazyThreadSafetyMode.ExecutionAndPublication)
        );

        return AwaitAndCleanup<T>(key, lazy);
    }

    private async Task<T> AwaitAndCleanup<T>(string key, Lazy<Task<object?>> lazy)
    {
        try
        {
            return (T)(await lazy.Value)!;
        }
        finally
        {
            _inflight.TryRemove(key, out _);
        }
    }

    private static async Task<object?> Wrap<T>(Func<Task<T>> factory)
    {
        return await factory();
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

        return await SingleFlight(cacheKey, async () =>
        {
            var url =
                $"/IPlayerService/GetOwnedGames/v0001/" +
                $"?key={_steamOptions.ApiKey}" +
                $"&steamid={steamId}" +
                $"&format=json" +
                $"{(includeAppInfo ? "&include_appinfo=1" : "")}" +
                $"{(includePlayedFreeGames ? "&include_played_free_games=1" : "")}" +
                $"&l=english";

            using var response = await _httpClient.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
                return new OwnedGames();

            var json = await response.Content.ReadAsStringAsync(ct);

            var parsed = JsonSerializer.Deserialize<OwnedGamesResponse>(json, _jsonOptions);

            var result = parsed?.Response ?? new OwnedGames();

            await _cache.SetAsync(cacheKey, result, _cacheSettings.OwnedGames);

            return result;
        });
    }

    public async Task<long> GetSteamIdFromVanityUrl(
        string vanityUrl,
        CancellationToken ct = default)
    {
        var cacheKey = $"vanity_{vanityUrl}";

        var cached = await _cache.GetAsync<string>(cacheKey);

        if (cached is not null)
            return cached == "NOT_FOUND" ? 0 : long.Parse(cached);

        return await SingleFlight(cacheKey, async () =>
        {
            var encoded = Uri.EscapeDataString(vanityUrl);

            var url =
                $"/ISteamUser/ResolveVanityURL/v0001/" +
                $"?key={_steamOptions.ApiKey}" +
                $"&vanityurl={encoded}&format=json";

            using var response = await _httpClient.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
                return 0;

            var json = await response.Content.ReadAsStringAsync(ct);

            var parsed =
                JsonSerializer.Deserialize<ResolveVanityUrlResponse>(json, _jsonOptions);

            var r = parsed?.Response;

            if (r is null || r.Success == STEAM_VANITY_NO_MATCH)
                return 0;

            if (r.Success != STEAM_VANITY_SUCCESS)
                return 0;

            var id = long.Parse(r.SteamId!);

            await _cache.SetAsync(cacheKey, r.SteamId!, _cacheSettings.VanitySuccess);

            return id;
        });
    }
}