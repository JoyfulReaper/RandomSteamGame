/*
 * Random Steam Game
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SteamApiClient.Contracts.SteamStoreApi;
using SteamApiClient.Services;
using SteamApiClient.Settings;
using System.Text.Json;

namespace SteamApiClient.HttpClients;

public class SteamStoreClient : ISteamStoreClient
{
    private readonly HttpClient _httpClient;
    private readonly ICacheService _cache;
    private readonly SteamClientApiOptions _steamOptions;
    private readonly IMemoryCache _memo;
    private readonly ILogger<SteamStoreClient> _logger;

    private static readonly JsonSerializerOptions _jsonOptions =
        new(JsonSerializerDefaults.Web);

    public SteamStoreClient(
        HttpClient httpClient,
        ICacheService cache,
        IOptions<SteamClientApiOptions> steamOptions,
        IMemoryCache memo,
        ILogger<SteamStoreClient> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _steamOptions = steamOptions.Value;
        _memo = memo;
        _logger = logger;

        _httpClient.BaseAddress = new Uri("https://store.steampowered.com");
    }

    public Task<AppDetailsResponse> GetAppData(int appId, CancellationToken ct = default)
    {
        var cacheKey = $"app:{appId}";

        return MemoizeAsync(cacheKey, async () =>
        {
            var cached = await _cache.GetAsync<AppDetailsResponse>(cacheKey);
            if (cached is not null)
                return cached;

            using var response = await _httpClient.GetAsync(
                $"/api/appdetails?appids={appId}&l=english", ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Steam Store API failed (AppDetails). AppId: {AppId}, StatusCode: {StatusCode}",
                    appId,
                    response.StatusCode);

                return new AppDetailsResponse(false, null);
            }

            var json = await response.Content.ReadAsStringAsync(ct);

            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty(appId.ToString(), out var root))
            {
                _logger.LogWarning(
                    "Steam Store API malformed response (missing app key). AppId: {AppId}",
                    appId);

                return new AppDetailsResponse(false, null);
            }

            var result = JsonSerializer.Deserialize<AppDetailsResponse>(root, _jsonOptions)
                         ?? new AppDetailsResponse(false, null);

            if (result.Success && result.AppData is not null)
            {
                await _cache.SetAsync(cacheKey, result, _steamOptions.Cache.AppDetails);
            }

            return result;

        }, _steamOptions.Cache.AppDetails.Duration);
    }

    private Task<T> MemoizeAsync<T>(
        string key,
        Func<Task<T>> factory,
        TimeSpan ttl)
    {
        return _memo.GetOrCreate(key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = ttl;

            entry.Value = new Lazy<Task<T>>(
                factory,
                LazyThreadSafetyMode.ExecutionAndPublication);

            return ((Lazy<Task<T>>)entry.Value).Value;

        })!;
    }
}