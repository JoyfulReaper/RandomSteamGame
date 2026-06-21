/*
 * Random Steam Game
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using SteamApiClient.Contracts.SteamStoreApi;
using SteamApiClient.Exceptions;
using SteamApiClient.Settings;
using System.Net.Http.Headers;
using System.Text.Json;

namespace SteamApiClient.HttpClients;

public class SteamStoreClient : ISteamStoreClient
{
    private readonly HttpClient _httpClient;
    private readonly IDistributedCache _cache;
    private readonly CacheSettings _cacheSettings;

    public SteamStoreClient(
        HttpClient httpClient,
        IDistributedCache cache,
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
        var cachedAppDataString = await _cache.GetStringAsync($"appId_{appId}");
        if (cachedAppDataString is null)
        {
            var jsonResponse = await _httpClient.GetStringAsync($"/api/appdetails?appids={appId}&l=english"); // TODO: Add support for other languages

            var successOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow =
                    TimeSpan.FromMinutes(_cacheSettings.AppDetails.AbsoluteMinutes)
            };

            await _cache.SetStringAsync($"appId_{appId}", jsonResponse, successOptions);

            var withoutRoot = JsonDocument.Parse(jsonResponse).RootElement.GetProperty(appId.ToString());
            var output = JsonSerializer.Deserialize<AppDetailsResponse>(withoutRoot);

            return output!;
        }

        var cachedWithoutRoot = JsonDocument.Parse(cachedAppDataString).RootElement.GetProperty(appId.ToString());
        var cachedOutput = JsonSerializer.Deserialize<AppDetailsResponse>(cachedWithoutRoot);

        if (cachedOutput is null)
        {
            throw new CacheException("Failed to deserialize cached data");
        }

        return cachedOutput;
    }
}