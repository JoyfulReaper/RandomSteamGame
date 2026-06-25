/*
 * Random Steam Game
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

// TODO: replace Concurrent dictitonary with IMemoryCache with entry eviction
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
    private readonly SteamClientApiOptions _steamOptions;
    private readonly ILogger<SteamClient> _logger;
    private readonly ICacheService _cache;

    private static readonly JsonSerializerOptions _jsonOptions =
        new(JsonSerializerDefaults.Web);

    private static readonly ConcurrentDictionary<string, Lazy<Task<object?>>> _inflight = new();

    private const int STEAM_VANITY_SUCCESS = 1;
    private const int STEAM_VANITY_NO_MATCH = 42;

    public SteamClient(
        HttpClient httpClient,
        IOptions<SteamClientApiOptions> steamOptions,
        ICacheService cache,
        ILogger<SteamClient> logger)
    {
        _httpClient = httpClient;
        _steamOptions = steamOptions.Value;
        _cache = cache;
        _logger = logger;

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
                LazyThreadSafetyMode.ExecutionAndPublication));

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

    private async Task<object?> Wrap<T>(Func<Task<T>> factory)
    {
        try
        {
            return await factory();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "SteamClient wrapped failure in single-flight pipeline");
            throw;
        }
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
        {
            _logger.LogDebug("Cache hit: OwnedGames {CacheKey}", cacheKey);
            return cached;
        }

        _logger.LogDebug("Cache miss: OwnedGames {CacheKey}", cacheKey);

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
            {
                _logger.LogWarning(
                    "Steam API failure (OwnedGames). SteamId={SteamId}, StatusCode={StatusCode}",
                    steamId,
                    response.StatusCode);

                return new OwnedGames(0, []);
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            var parsed = JsonSerializer.Deserialize<OwnedGamesResponse>(json, _jsonOptions);

            if (parsed?.Response is null)
            {
                _logger.LogWarning(
                    "Steam API invalid JSON (OwnedGames). SteamId={SteamId}",
                    steamId);

                return new OwnedGames(0, []);
            }

            var result = parsed.Response;

            await _cache.SetAsync(cacheKey, result, _steamOptions.Cache.OwnedGames);

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
        {
            _logger.LogDebug("Cache hit: VanityUrl {VanityUrl}", vanityUrl);
            return cached == "NOT_FOUND" ? 0 : long.Parse(cached);
        }

        _logger.LogDebug("Cache miss: VanityUrl {VanityUrl}", vanityUrl);

        return await SingleFlight(cacheKey, async () =>
        {
            var encoded = Uri.EscapeDataString(vanityUrl);

            var url =
                $"/ISteamUser/ResolveVanityURL/v0001/" +
                $"?key={_steamOptions.ApiKey}" +
                $"&vanityurl={encoded}&format=json";

            using var response = await _httpClient.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Steam API failure (VanityUrl). VanityUrl={VanityUrl}, StatusCode={StatusCode}",
                    vanityUrl,
                    response.StatusCode);

                return 0;
            }

            var json = await response.Content.ReadAsStringAsync(ct);

            var parsed = JsonSerializer.Deserialize<ResolveVanityUrlResponse>(json, _jsonOptions);

            var r = parsed?.Response;

            if (r is null)
            {
                _logger.LogWarning(
                    "Steam API invalid response (VanityUrl). VanityUrl={VanityUrl}",
                    vanityUrl);

                return 0;
            }

            if (r.Success == STEAM_VANITY_NO_MATCH)
            {
                _logger.LogInformation(
                    "Vanity URL not found: {VanityUrl}",
                    vanityUrl);

                return 0;
            }

            if (r.Success != STEAM_VANITY_SUCCESS)
            {
                _logger.LogWarning(
                    "Steam API failure status (VanityUrl). VanityUrl={VanityUrl}, Success={Success}",
                    vanityUrl,
                    r.Success);

                return 0;
            }

            var id = long.Parse(r.SteamId!);

            await _cache.SetAsync(cacheKey, r.SteamId!, _steamOptions.Cache.VanitySuccess);

            return id;
        });
    }
}