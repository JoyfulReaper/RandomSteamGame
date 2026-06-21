/*
 * Random Steam Game
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using SteamApiClient.Contracts.SteamStoreApi;
using SteamApiClient.HttpClients;
using SteamApiClient.Services;
using SteamApiClient.Settings;
using System.Text.Json;

public class SteamStoreClient : ISteamStoreClient
{
    private readonly HttpClient _httpClient;
    private readonly ICacheService _cache;
    private readonly CacheSettings _cacheSettings;
    private readonly IMemoryCache _memo;

    private static readonly JsonSerializerOptions _jsonOptions =
        new(JsonSerializerDefaults.Web);

    public SteamStoreClient(
        HttpClient httpClient,
        ICacheService cache,
        IOptions<CacheSettings> cacheSettings,
        IMemoryCache memo)
    {
        _httpClient = httpClient;
        _cache = cache;
        _cacheSettings = cacheSettings.Value;
        _memo = memo;

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
                return new AppDetailsResponse { Success = false };

            var json = await response.Content.ReadAsStringAsync(ct);

            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty(appId.ToString(), out var root))
                return new AppDetailsResponse { Success = false };

            var result = JsonSerializer.Deserialize<AppDetailsResponse>(root, _jsonOptions)
                         ?? new AppDetailsResponse { Success = false };
            if (result.Success && result.AppData is not null)
            {
                await _cache.SetAsync(cacheKey, result, _cacheSettings.AppDetails);
            }

            return result;

        }, _cacheSettings.AppDetails.Duration);
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