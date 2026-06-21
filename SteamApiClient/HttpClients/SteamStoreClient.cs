/*
 * Random Steam Game
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using Microsoft.Extensions.Options;
using SteamApiClient.Contracts.SteamStoreApi;
using SteamApiClient.Services;
using SteamApiClient.Settings;
using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text.Json;

namespace SteamApiClient.HttpClients;

public class SteamStoreClient : ISteamStoreClient
{
    private readonly HttpClient _httpClient;
    private readonly ICacheService _cache;
    private readonly CacheSettings _cacheSettings;
    private static readonly JsonSerializerOptions _jsonOptions =
            new(JsonSerializerDefaults.Web);

    private static readonly ConcurrentDictionary<int, SemaphoreSlim> _locks = new();

    public SteamStoreClient(
        HttpClient httpClient,
        ICacheService cache,
        IOptions<CacheSettings> cacheSettings)
    {
        _httpClient = httpClient;
        _cache = cache;
        _cacheSettings = cacheSettings.Value;

        _httpClient.BaseAddress = new Uri("https://store.steampowered.com");
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue(SteamClientConstants.UserAgent, SteamClientConstants.Version));
        _httpClient.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue(SteamClientConstants.UserAgentComment));
    }

    public async Task<AppDetailsResponse> GetAppData(
        int appId,
        CancellationToken ct = default)
    {
        var cacheKey = $"appId_{appId}";

        var cached = await _cache.GetAsync<AppDetailsResponse>(cacheKey);
        if (cached is not null)
            return cached;

        var gate = _locks.GetOrAdd(appId, _ => new SemaphoreSlim(1, 1));

        await gate.WaitAsync(ct);
        try
        {
            cached = await _cache.GetAsync<AppDetailsResponse>(cacheKey);
            if (cached is not null)
                return cached;

            using var response = await _httpClient.GetAsync(
                $"/api/appdetails?appids={appId}&l=english", ct);

            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);

            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty(appId.ToString(), out var root))
                return await CacheFailure(appId);

            var result = JsonSerializer.Deserialize<AppDetailsResponse>(root, _jsonOptions);

            if (result is null || !result.Success || result.AppData is null)
                return await CacheFailure(appId);

            await _cache.SetAsync(cacheKey, result, _cacheSettings.AppDetails);

            return result;
        }
        catch
        {
            return await CacheFailure(appId);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<AppDetailsResponse> CacheFailure(int appId)
    {
        var cacheKey = $"appId_{appId}";

        var failure = new AppDetailsResponse
        {
            Success = false,
            AppData = null
        };

        await _cache.SetAsync(cacheKey, failure, _cacheSettings.AppDetails);

        return failure;
    }
}