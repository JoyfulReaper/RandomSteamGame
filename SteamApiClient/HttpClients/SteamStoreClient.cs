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

    public async Task<AppDetailsResponse> GetAppData(int appId)
    {
        var cacheKey = $"appId_{appId}";

        var cached = await _cache.GetAsync<AppDetailsResponse>(cacheKey);
        if (cached is not null)
        {
            return cached;
        }

        AppDetailsResponse? result = null;
        try
        {
            using var response = await _httpClient.GetAsync(
                $"/api/appdetails?appids={appId}&l=english");

            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty(appId.ToString(), out var root))
            {
                return await CreateAndCacheFailure(appId);
            }

            result = JsonSerializer.Deserialize<AppDetailsResponse>(root, _jsonOptions);
        }
        catch
        {
            return await CreateAndCacheFailure(appId);
        }

        if (result is null || !result.Success || result.AppData is null)
        {
            return await CreateAndCacheFailure(appId);
        }

        await _cache.SetAsync(
            cacheKey,
            result,
            _cacheSettings.AppDetails);

        return result;
    }

    private async Task<AppDetailsResponse> CreateAndCacheFailure(int appId)
    {
        var cacheKey = $"appId_{appId}_fail";

        await _cache.SetAsync(
            cacheKey,
            new AppDetailsResponse
            {
                Success = false,
                AppData = null
            },
            new CachePolicy
            {
                AbsoluteMinutes = 10
            });

        return new AppDetailsResponse
        {
            Success = false,
            AppData = null
        };
    }
}