/*
 * Steam Api Client
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SteamApiClient.Contracts.SteamApi;
using SteamApiClient.Services;
using SteamApiClient.Settings;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SteamApiClient.HttpClients;

public class SteamClient : ISteamClient
{
    private readonly HttpClient _httpClient;
    private readonly SteamClientApiOptions _steamOptions;
    private readonly ILogger<SteamClient> _logger;
    private readonly ICacheService _cache;

    private static readonly JsonSerializerOptions _jsonOptions =
        new(JsonSerializerDefaults.Web);

    private const int STEAM_VANITY_SUCCESS = 1;
    private const int STEAM_VANITY_NO_MATCH = 42;
    private const int STORE_BROWSE_BATCH_SIZE = 100;

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

        // TODO: Move this to a helper or something
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));

        _httpClient.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue(SteamClientConstants.UserAgent, SteamClientConstants.Version));

        _httpClient.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue(SteamClientConstants.UserAgentComment));
    }

    public async Task<OwnedGames> GetOwnedGames(
        long steamId,
        bool includeAppInfo = true,
        bool includePlayedFreeGames = true,
        CancellationToken ct = default)
    {
        var result = await GetOwnedGamesWithCacheInfo(
            steamId,
            includeAppInfo,
            includePlayedFreeGames,
            ct);

        return result.OwnedGames;
    }

    public async Task<OwnedGamesResult> GetOwnedGamesWithCacheInfo(
        long steamId,
        bool includeAppInfo = true,
        bool includePlayedFreeGames = true,
        CancellationToken ct = default)
    {
        var cacheKey = $"owned_v2_{steamId}_{includeAppInfo}_{includePlayedFreeGames}";
        var tags = new[] { $"steam_user_{steamId}", "owned_games" };

        // checks RAM (L1), falls back to DB (L2) enforces single flight
        var result = await _cache.GetOrCreateWithMetadataAsync(cacheKey, async (token) =>
        {
            // _logger.LogDebug("Cache miss or expired. Fetching OwnedGames from Steam API.");

            var url =
                $"IPlayerService/GetOwnedGames/v0001/" +
                $"?key={_steamOptions.ApiKey}" +
                $"&steamid={steamId}" +
                $"&format=json" +
                $"{(includeAppInfo ? "&include_appinfo=1" : "")}" +
                $"{(includePlayedFreeGames ? "&include_played_free_games=1" : "")}" +
                $"&l=english";

            using var response = await _httpClient.GetAsync(url, token);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Steam API failure (OwnedGames). StatusCode={StatusCode}",
                    response.StatusCode);

                throw new HttpRequestException(
                    $"Steam API failed to return owned games. StatusCode={response.StatusCode}");
            }

            var json = await response.Content.ReadAsStringAsync(token);
            var parsed = JsonSerializer.Deserialize<OwnedGamesResponse>(json, _jsonOptions);

            if (parsed?.Response is null)
            {
                _logger.LogWarning(
                    "Steam API invalid JSON (OwnedGames).");

                throw new InvalidOperationException("Steam API returned an invalid owned-games response.");
            }

            return parsed.Response;

        }, _steamOptions.Cache.OwnedGames, tags, ct);

        return new OwnedGamesResult(result.Value, result.Cache);
    }

    public async Task<long> GetSteamIdFromVanityUrl(
        string vanityUrl,
        CancellationToken ct = default)
    {
        var normalizedVanity = SteamVanityUrlHelper.Normalize(vanityUrl);
        var successCacheKey = SteamVanityUrlHelper.BuildCacheKey(normalizedVanity);
        var notFoundCacheKey = SteamVanityUrlHelper.BuildNotFoundCacheKey(normalizedVanity);
        var tags = new[] { SteamVanityUrlHelper.BuildCacheKey(normalizedVanity), "vanity_urls" };

        var cachedSuccess = await _cache.GetAsync<long?>(successCacheKey, ct);
        if (cachedSuccess.HasValue)
        {
            return cachedSuccess.Value;
        }

        var cachedNotFound = await _cache.GetAsync<bool?>(notFoundCacheKey, ct);
        if (cachedNotFound == true)
        {
            return 0L;
        }

        // _logger.LogDebug("Cache miss or expired. Resolving VanityUrl from Steam API.");

        var encoded = Uri.EscapeDataString(normalizedVanity);
        var url =
            $"ISteamUser/ResolveVanityURL/v0001/" +
            $"?key={_steamOptions.ApiKey}" +
            $"&vanityurl={encoded}&format=json";

        using var response = await _httpClient.GetAsync(url, ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Steam API failure (VanityUrl). StatusCode={StatusCode}",
                response.StatusCode);

            throw new HttpRequestException(
                $"Steam API failed to resolve vanity URL. StatusCode={response.StatusCode}");
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        var parsed = JsonSerializer.Deserialize<ResolveVanityUrlResponse>(json, _jsonOptions);
        var r = parsed?.Response;

        if (r is null)
        {
            _logger.LogWarning(
                "Steam API invalid response (VanityUrl).");

            throw new InvalidOperationException("Steam API returned an invalid vanity response.");
        }

        if (r.Success == STEAM_VANITY_NO_MATCH)
        {
            // _logger.LogInformation("Vanity URL not found.");
            await _cache.SetAsync(notFoundCacheKey, true, _steamOptions.Cache.VanityNotFound, tags, ct);
            return 0L;
        }

        if (r.Success != STEAM_VANITY_SUCCESS)
        {
            _logger.LogWarning(
                "Steam API failure status (VanityUrl). Success={Success}",
                r.Success);

            throw new InvalidOperationException(
                $"Steam API returned vanity failure status {r.Success}.");
        }

        var steamId = long.Parse(r.SteamId!);
        await _cache.SetAsync(successCacheKey, steamId, _steamOptions.Cache.VanitySuccess, tags, ct);
        return steamId;
    }

    public async Task InvalidateOwnedGamesCacheAsync(long userId)
    {
        await _cache.InvalidateByTagAsync($"steam_user_{userId}");
    }

    public async Task<IReadOnlyDictionary<int, SteamDeckCompatibilityCategory>> GetSteamDeckCompatibilityAsync(
        IEnumerable<int> appIds,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(appIds);

        var ids = appIds
            .Where(appId => appId > 0)
            .Distinct()
            .ToArray();

        var result = new Dictionary<int, SteamDeckCompatibilityCategory>();
        var uncached = new List<int>();

        foreach (var appId in ids)
        {
            var cached = await _cache.GetAsync<CachedSteamDeckCompatibility>($"steam_deck_compat:{appId}", ct);

            if (cached is not null)
            {
                result[appId] = cached.Category;
            }
            else
            {
                uncached.Add(appId);
            }
        }

        foreach (var batch in uncached.Chunk(STORE_BROWSE_BATCH_SIZE))
        {
            var fetched = await FetchSteamDeckCompatibilityBatchAsync(batch, ct);

            if (fetched is null)
            {
                foreach (var appId in batch)
                {
                    result[appId] = SteamDeckCompatibilityCategory.Unknown;
                }

                continue;
            }

            foreach (var appId in batch)
            {
                var category = fetched.GetValueOrDefault(appId, SteamDeckCompatibilityCategory.Unknown);

                result[appId] = category;

                await _cache.SetAsync(
                    $"steam_deck_compat:{appId}",
                    new CachedSteamDeckCompatibility(category),
                    _steamOptions.Cache.SteamDeckCompatibility,
                    [
                        "steam_deck_compatibility",
                        $"app_{appId}"
                    ],
                    ct);
            }
        }

        return result;
    }

    private async Task<Dictionary<int, SteamDeckCompatibilityCategory>?> FetchSteamDeckCompatibilityBatchAsync(
        IReadOnlyCollection<int> appIds,
        CancellationToken ct)
    {
        var request = new StoreBrowseRequest(
            appIds
                .Select(appId => new StoreBrowseItemId(appId))
                .ToArray(),
            new StoreBrowseContext(
                Language: "english",
                CountryCode: "US",
                SteamRealm: 1),
            new StoreBrowseDataRequest(
                IncludePlatforms: true));

        var inputJson = JsonSerializer.Serialize(request, _jsonOptions);

        var url =
            "IStoreBrowseService/GetItems/v1/" +
            $"?key={Uri.EscapeDataString(_steamOptions.ApiKey)}" +
            $"&input_json={Uri.EscapeDataString(inputJson)}";

        try
        {
            using var response = await _httpClient.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Steam API failure (Deck compatibility). " +
                    "StatusCode={StatusCode}",
                    response.StatusCode);

                return null;
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            var parsed = JsonSerializer.Deserialize<StoreBrowseItemsResponse>(
                json,
                _jsonOptions);

            if (parsed?.Response?.StoreItems is null)
            {
                _logger.LogWarning(
                    "Steam API returned an invalid Deck " +
                    "compatibility response.");

                return null;
            }

            var result = new Dictionary<int, SteamDeckCompatibilityCategory>();

            foreach (var item in parsed.Response.StoreItems)
            {
                var category =
                    item.Platforms?.SteamDeckCompatCategory switch
                    {
                        1 => SteamDeckCompatibilityCategory.Unsupported,
                        2 => SteamDeckCompatibilityCategory.Playable,
                        3 => SteamDeckCompatibilityCategory.Verified,
                        _ => SteamDeckCompatibilityCategory.Unknown
                    };

                result[item.AppId] = category;
            }

            return result;
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(
                exception,
                "Steam Deck compatibility request failed.");

            return null;
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(
                exception,
                "Steam Deck compatibility response could not be parsed.");

            return null;
        }
    }

    private sealed record StoreBrowseRequest(
        StoreBrowseItemId[] Ids,
        StoreBrowseContext Context,

        [property: JsonPropertyName("data_request")]
        StoreBrowseDataRequest DataRequest);

    private sealed record StoreBrowseItemId([property: JsonPropertyName("appid")] int AppId);

    private sealed record StoreBrowseContext(string Language,

    [property: JsonPropertyName("country_code")]
    string CountryCode,

    [property: JsonPropertyName("steam_realm")]
    int SteamRealm);

    private sealed record StoreBrowseDataRequest([property: JsonPropertyName("include_platforms")]
    bool IncludePlatforms);

    private sealed record CachedSteamDeckCompatibility(SteamDeckCompatibilityCategory Category);
}
