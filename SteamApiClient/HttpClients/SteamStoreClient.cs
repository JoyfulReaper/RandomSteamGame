/*
 * Steam Api Client
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

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
    private readonly ILogger<SteamStoreClient> _logger;

    private static readonly JsonSerializerOptions _jsonOptions =
        new(JsonSerializerDefaults.Web);

    public SteamStoreClient(
        HttpClient httpClient,
        ICacheService cache,
        IOptions<SteamClientApiOptions> steamOptions,
        ILogger<SteamStoreClient> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _steamOptions = steamOptions.Value;
        _logger = logger;
    }

    public Task<AppDetailsResponse> GetAppData(int appId, IEnumerable<string>? tags = null, CancellationToken ct = default)
    {
        var cacheKey = $"app:{appId}";
        var entryTags = tags?.ToList() ?? new List<string>();
        entryTags.Add("app_details");
        entryTags.Add($"app_{appId}");

        return _cache.GetOrCreateAsync(cacheKey, async (token) =>
        {
            // _logger.LogDebug("Cache miss or expired. Fetching AppDetails from Steam Store API for AppId: {AppId}", appId);

            using var response = await _httpClient.GetAsync(
                $"api/appdetails?appids={appId}&l=english", token);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Steam Store API failed (AppDetails). AppId: {AppId}, StatusCode: {StatusCode}",
                    appId,
                    response.StatusCode);

                return new AppDetailsResponse(false, null);
            }

            var json = await response.Content.ReadAsStringAsync(token);

            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty(appId.ToString(), out var root))
            {
                _logger.LogWarning(
                    "Steam Store API malformed response (missing app key). AppId: {AppId}",
                    appId);

                return new AppDetailsResponse(false, null);
            }

            return JsonSerializer.Deserialize<AppDetailsResponse>(root, _jsonOptions)
                         ?? new AppDetailsResponse(false, null);

        }, _steamOptions.Cache.AppDetails, entryTags, ct);
    }
}