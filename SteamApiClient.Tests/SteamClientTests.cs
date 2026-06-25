/*
 * Steam Api Client
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using SteamApiClient.Contracts.SteamApi;
using SteamApiClient.HttpClients;
using SteamApiClient.Services;
using SteamApiClient.Settings;
using System.Net;
using System.Text.Json;

namespace SteamApiClient.Tests;

public class SteamClientTests
{
    private readonly ICacheService _mockCache;
    private readonly IOptions<SteamClientApiOptions> _options;

    public SteamClientTests()
    {
        _mockCache = Substitute.For<ICacheService>();

        _options = Options.Create(new SteamClientApiOptions
        {
            ApiKey = "FAKE_API_KEY",
            Cache = new CacheSettings
            {
                OwnedGames = new CachePolicy { AbsoluteMinutes = 60 },
                VanitySuccess = new CachePolicy { AbsoluteMinutes = 120 }
            }
        });

        // cache interception for OwnedGames read-through calls
        _mockCache.GetOrCreateAsync(
            Arg.Any<string>(),
            Arg.Any<Func<CancellationToken, Task<OwnedGames>>>(),
            Arg.Any<CachePolicy>(),
            Arg.Any<CancellationToken>())
        .Returns(async callInfo =>
        {
            var factory = callInfo.Arg<Func<CancellationToken, Task<OwnedGames>>>();
            var ct = callInfo.Arg<CancellationToken>();
            return await factory(ct);
        });

        // cache interception for long (Vanity URLs) read-through calls
        _mockCache.GetOrCreateAsync(
            Arg.Any<string>(),
            Arg.Any<Func<CancellationToken, Task<long>>>(),
            Arg.Any<CachePolicy>(),
            Arg.Any<CancellationToken>())
        .Returns(async callInfo =>
        {
            var factory = callInfo.Arg<Func<CancellationToken, Task<long>>>();
            var ct = callInfo.Arg<CancellationToken>();
            return await factory(ct);
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
        var mockHandler = new MockHttpMessageHandler(json, HttpStatusCode.OK);
        var httpClient = new HttpClient(mockHandler);

        var client = new SteamClient(httpClient, _options, _mockCache, NullLogger<SteamClient>.Instance);

        // Act
        var result = await client.GetOwnedGames(76561197960287930L);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.GameCount);
        Assert.Equal(220, result.Games[0].AppId);
    }

    [Fact]
    public async Task GetOwnedGames_HttpError_ReturnsEmptyResult()
    {
        // Arrange
        var mockHandler = new MockHttpMessageHandler(string.Empty, HttpStatusCode.InternalServerError);
        var httpClient = new HttpClient(mockHandler);

        var client = new SteamClient(httpClient, _options, _mockCache, NullLogger<SteamClient>.Instance);

        // Act
        var result = await client.GetOwnedGames(76561197960287930L);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.GameCount);
        Assert.Empty(result.Games);
    }

    [Fact]
    public async Task GetOwnedGames_MalformedJson_ReturnsEmptyResult()
    {
        // Arrange
        var invalidJson = "{ \"response\": null }";
        var mockHandler = new MockHttpMessageHandler(invalidJson, HttpStatusCode.OK);
        var httpClient = new HttpClient(mockHandler);

        var client = new SteamClient(httpClient, _options, _mockCache, NullLogger<SteamClient>.Instance);

        // Act
        var result = await client.GetOwnedGames(76561197960287930L);

        // Assert
        Assert.Equal(0, result.GameCount);
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
        var mockHandler = new MockHttpMessageHandler(json, HttpStatusCode.OK);
        var httpClient = new HttpClient(mockHandler);

        var client = new SteamClient(httpClient, _options, _mockCache, NullLogger<SteamClient>.Instance);

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
        var mockHandler = new MockHttpMessageHandler(json, HttpStatusCode.OK);
        var httpClient = new HttpClient(mockHandler);

        var client = new SteamClient(httpClient, _options, _mockCache, NullLogger<SteamClient>.Instance);

        // Act
        var result = await client.GetSteamIdFromVanityUrl("some_fake_url_that_doesnt_exist");

        // Assert
        Assert.Equal(0L, result);
    }

    [Fact]
    public async Task GetSteamIdFromVanityUrl_OtherSteamFailureStatus_ReturnsZero()
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
        var mockHandler = new MockHttpMessageHandler(json, HttpStatusCode.OK);
        var httpClient = new HttpClient(mockHandler);

        var client = new SteamClient(httpClient, _options, _mockCache, NullLogger<SteamClient>.Instance);

        // Act
        var result = await client.GetSteamIdFromVanityUrl("error_route");

        // Assert
        Assert.Equal(0L, result);
    }

    #endregion
}