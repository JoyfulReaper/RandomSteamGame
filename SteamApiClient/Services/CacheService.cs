/*
 * Random Steam Game
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using SteamApiClient.Settings;
using System.Text.Json;

namespace SteamApiClient.Services;

public class CacheService : ICacheService
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<CacheService> _logger;

    private readonly JsonSerializerOptions _jsonOptions =
        new(JsonSerializerDefaults.Web);

    public CacheService(
        IDistributedCache cache,
        ILogger<CacheService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        var value = await _cache.GetStringAsync(key);

        if (string.IsNullOrWhiteSpace(value))
        {
            _logger.LogDebug("Cache miss: {Key}", key);
            return default;
        }

        _logger.LogDebug("Cache hit: {Key}", key);

        try
        {
            return JsonSerializer.Deserialize<T>(value, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache deserialization failed: {Key}", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, CachePolicy policy)
    {
        if (value is null)
            return;

        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow =
                TimeSpan.FromMinutes(policy.AbsoluteMinutes)
        };

        var json = JsonSerializer.Serialize(value, _jsonOptions);

        await _cache.SetStringAsync(key, json, options);

        _logger.LogDebug("Cache set: {Key} (TTL {Minutes}m)", key, policy.AbsoluteMinutes);
    }
}