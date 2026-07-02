/*
 * Steam Api Client
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SteamApiClient.HttpClients;
using SteamApiClient.Services;
using SteamApiClient.Settings;
using System.Net;
using System.Text.Json;

namespace SteamApiClient.Tests;

public class SteamClientTests
{
    private readonly IOptions<SteamClientApiOptions> _options;

    public SteamClientTests()
    {
        _options = Options.Create(new SteamClientApiOptions
        {
            ApiKey = "FAKE_API_KEY",
            Cache = new CacheSettings
            {
                OwnedGames = new CachePolicy { AbsoluteMinutes = 60 },
                VanitySuccess = new CachePolicy { AbsoluteMinutes = 120 },
                VanityNotFound = new CachePolicy { AbsoluteMinutes = 15 }
            }
        });
    }

    #region GetOwnedGames Tests

    [Fact]
    public async Task GetOwnedGames_ValidApiCall_ReturnsParsedGames()
    {
        // Arrange
        var fakeResponsePayload = new
        {
            response = new
            {
                game_count = 2,
                games = new[]
                {
                    new { appid = 220, name = "Half-Life 2", playtime_forever = 1200 },
                    new { appid = 400, name = "Portal", playtime_forever = 600 }
                }
            }
        };

        var json = JsonSerializer.Serialize(fakeResponsePayload);
        var client = CreateClient(json, HttpStatusCode.OK);

        // Act
        var result = await client.GetOwnedGames(76561197960287930L);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.GameCount);
        Assert.Equal(220, result.Games[0].AppId);
    }

    [Fact]
    public async Task GetOwnedGames_HttpError_Throws()
    {
        // Arrange
        var client = CreateClient(string.Empty, HttpStatusCode.InternalServerError);

        // Act / Assert
        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.GetOwnedGames(76561197960287930L));
    }

    [Fact]
    public async Task GetOwnedGames_MalformedJson_Throws()
    {
        // Arrange
        var invalidJson = "{ \"response\": null }";
        var client = CreateClient(invalidJson, HttpStatusCode.OK);

        // Act / Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.GetOwnedGames(76561197960287930L));
    }

    #endregion

    #region GetSteamIdFromVanityUrl Tests

    [Fact]
    public async Task GetSteamIdFromVanityUrl_Success_ReturnsLongId()
    {
        // Arrange
        var fakeResponse = new
        {
            response = new
            {
                success = 1,
                steamid = "76561197960287930"
            }
        };

        var json = JsonSerializer.Serialize(fakeResponse);
        var client = CreateClient(json, HttpStatusCode.OK);

        // Act
        var result = await client.GetSteamIdFromVanityUrl("gabelogannewell");

        // Assert
        Assert.Equal(76561197960287930L, result);
    }

    [Fact]
    public async Task GetSteamIdFromVanityUrl_NotFound_ReturnsZero()
    {
        // Arrange
        var fakeResponse = new
        {
            response = new
            {
                success = 42, // STEAM_VANITY_NO_MATCH
                message = "No match"
            }
        };

        var json = JsonSerializer.Serialize(fakeResponse);
        var client = CreateClient(json, HttpStatusCode.OK);

        // Act
        var result = await client.GetSteamIdFromVanityUrl("some_fake_url_that_doesnt_exist");

        // Assert
        Assert.Equal(0L, result);
    }

    [Fact]
    public async Task GetSteamIdFromVanityUrl_OtherSteamFailureStatus_Throws()
    {
        // Arrange
        var fakeResponse = new
        {
            response = new
            {
                success = 3 // Random error status code
            }
        };

        var json = JsonSerializer.Serialize(fakeResponse);
        var client = CreateClient(json, HttpStatusCode.OK);

        // Act / Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.GetSteamIdFromVanityUrl("error_route"));
    }

    #endregion

    private SteamClient CreateClient(string responseContent, HttpStatusCode statusCode)
    {
        var httpClient = new HttpClient(new MockHttpMessageHandler(responseContent, statusCode))
        {
            BaseAddress = new Uri("https://api.steampowered.com/")
        };

        return new SteamClient(httpClient, _options, CreateCacheService(), NullLogger<SteamClient>.Instance);
    }

    private static ICacheService CreateCacheService()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHybridCache();
        services.AddScoped<ICacheService, CacheService>();

        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<ICacheService>();
    }
}
