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
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(SteamClientConstants.UserAgent, SteamClientConstants.Version));
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(SteamClientConstants.UserAgentComment));
    }

    public async Task<AppDetailsResponse> GetAppData(int appId)
    {
        var cacheKey = $"appId_{appId}";

        var cached = await _cache.GetAsync<AppDetailsResponse>(cacheKey);
        if (cached is not null)
        {
            return cached;
        }

        var jsonResponse = 
            await _httpClient.GetStringAsync(
                $"/api/appdetails?appids={appId}&l=english"); // TODO: Add support for other languages

        var root = JsonDocument.Parse(jsonResponse)
            .RootElement
            .GetProperty(appId.ToString());

        var result = JsonSerializer.Deserialize<AppDetailsResponse>(
            root, 
            _jsonOptions);

        await _cache.SetAsync<AppDetailsResponse>(
            cacheKey,
            result,
            _cacheSettings.AppDetails);


        return result;
    }
}