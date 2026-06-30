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
        var cacheKey = $"owned_{steamId}_{includeAppInfo}_{includePlayedFreeGames}";
        var tags = new[] { $"steam_user_{steamId}", "owned_games" };

        // checks RAM (L1), falls back to DB (L2) enforces single flight
        return await _cache.GetOrCreateAsync(cacheKey, async (token) =>
        {
            // _logger.LogDebug("Cache miss or expired. Fetching OwnedGames from Steam API for SteamId={SteamId}", steamId);

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
                    "Steam API failure (OwnedGames). SteamId={SteamId}, StatusCode={StatusCode}",
                    steamId,
                    response.StatusCode);

                return new OwnedGames(0, []);
            }

            var json = await response.Content.ReadAsStringAsync(token);
            var parsed = JsonSerializer.Deserialize<OwnedGamesResponse>(json, _jsonOptions);

            if (parsed?.Response is null)
            {
                _logger.LogWarning(
                    "Steam API invalid JSON (OwnedGames). SteamId={SteamId}",
                    steamId);

                return new OwnedGames(0, []);
            }

            return parsed.Response;

        }, _steamOptions.Cache.OwnedGames, tags, ct);
    }

    public async Task<long> GetSteamIdFromVanityUrl(
        string vanityUrl,
        CancellationToken ct = default)
    {
        var cacheKey = $"vanity_{vanityUrl}";
        var tags = new[] { $"vanity_{vanityUrl}", "vanity_urls" };

        return await _cache.GetOrCreateAsync(cacheKey, async (token) =>
        {
            // _logger.LogDebug("Cache miss or expired. Resolving VanityUrl from Steam API: {VanityUrl}", vanityUrl);

            var encoded = Uri.EscapeDataString(vanityUrl);
            var url =
                $"ISteamUser/ResolveVanityURL/v0001/" +
                $"?key={_steamOptions.ApiKey}" +
                $"&vanityurl={encoded}&format=json";

            using var response = await _httpClient.GetAsync(url, token);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Steam API failure (VanityUrl). VanityUrl={VanityUrl}, StatusCode={StatusCode}",
                    vanityUrl,
                    response.StatusCode);

                return 0L;
            }

            var json = await response.Content.ReadAsStringAsync(token);
            var parsed = JsonSerializer.Deserialize<ResolveVanityUrlResponse>(json, _jsonOptions);
            var r = parsed?.Response;

            if (r is null)
            {
                _logger.LogWarning(
                    "Steam API invalid response (VanityUrl). VanityUrl={VanityUrl}",
                    vanityUrl);

                return 0L;
            }

            if (r.Success == STEAM_VANITY_NO_MATCH)
            {
                // _logger.LogInformation("Vanity URL not found: {VanityUrl}", vanityUrl);
                return 0L;
            }

            if (r.Success != STEAM_VANITY_SUCCESS)
            {
                _logger.LogWarning(
                    "Steam API failure status (VanityUrl). VanityUrl={VanityUrl}, Success={Success}",
                    vanityUrl,
                    r.Success);

                return 0L;
            }

            return long.Parse(r.SteamId!);

        }, _steamOptions.Cache.VanitySuccess, tags, ct);
    }

    public async Task InvalidateOwnedGamesCacheAsync(long userId)
    {
        await _cache.InvalidateByTagAsync($"steam_user_{userId}");
    }
}
