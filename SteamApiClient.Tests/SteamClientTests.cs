/*
 * Steam Api Client
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SteamApiClient.Contracts.SteamApi;
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
                OwnedGames = new CachePolicy
                {
                    AbsoluteMinutes = 60
                },
                AppDetails = new CachePolicy
                {
                    AbsoluteMinutes = 60
                },
                SteamDeckCompatibility = new CachePolicy
                {
                    AbsoluteMinutes = 1440
                },
                VanitySuccess = new CachePolicy
                {
                    AbsoluteMinutes = 120
                },
                VanityNotFound = new CachePolicy
                {
                    AbsoluteMinutes = 15
                }
            }
        });
    }

    [Fact]
    public async Task GetSteamDeckCompatibilityAsync_ReturnsMappedCategories()
    {
        var json = JsonSerializer.Serialize(new
        {
            response = new
            {
                store_items = new[]
                {
                new
                {
                    appid = 620,
                    platforms = new
                    {
                        steam_deck_compat_category = 3
                    }
                },
                new
                {
                    appid = 400,
                    platforms = new
                    {
                        steam_deck_compat_category = 2
                    }
                },
                new
                {
                    appid = 251570,
                    platforms = new
                    {
                        steam_deck_compat_category = 1
                    }
                },
                new
                {
                    appid = 371140,
                    platforms = new
                    {
                        steam_deck_compat_category = 0
                    }
                }
            }
            }
        });

        var client = CreateClient(
            json,
            HttpStatusCode.OK);

        var result =
            await client.GetSteamDeckCompatibilityAsync(
                [620, 400, 251570, 371140],
                TestContext.Current.CancellationToken);

        Assert.Equal(
            SteamDeckCompatibilityCategory.Verified,
            result[620]);

        Assert.Equal(
            SteamDeckCompatibilityCategory.Playable,
            result[400]);

        Assert.Equal(
            SteamDeckCompatibilityCategory.Unsupported,
            result[251570]);

        Assert.Equal(
            SteamDeckCompatibilityCategory.Unknown,
            result[371140]);
    }

    [Fact]
    public async Task GetSteamDeckCompatibilityAsync_UsesCachedResult()
    {
        var json = JsonSerializer.Serialize(new
        {
            response = new
            {
                store_items = new[]
                {
                new
                {
                    appid = 620,
                    platforms = new
                    {
                        steam_deck_compat_category = 3
                    }
                }
            }
            }
        });

        var handler = new SequencedHttpMessageHandler(
            (json, HttpStatusCode.OK));

        var client = CreateClient(handler);

        var first =
            await client.GetSteamDeckCompatibilityAsync(
                [620],
                TestContext.Current.CancellationToken);

        var second =
            await client.GetSteamDeckCompatibilityAsync(
                [620],
                TestContext.Current.CancellationToken);

        Assert.Equal(
            SteamDeckCompatibilityCategory.Verified,
            first[620]);

        Assert.Equal(
            SteamDeckCompatibilityCategory.Verified,
            second[620]);

        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task GetSteamDeckCompatibilityAsync_HttpFailureIsNotCached()
    {
        var successJson = JsonSerializer.Serialize(new
        {
            response = new
            {
                store_items = new[]
                {
                new
                {
                    appid = 620,
                    platforms = new
                    {
                        steam_deck_compat_category = 3
                    }
                }
            }
            }
        });

        var handler = new SequencedHttpMessageHandler(
            (string.Empty, HttpStatusCode.InternalServerError),
            (successJson, HttpStatusCode.OK));

        var client = CreateClient(handler);

        var first =
            await client.GetSteamDeckCompatibilityAsync(
                [620],
                TestContext.Current.CancellationToken);

        var second =
            await client.GetSteamDeckCompatibilityAsync(
                [620],
                TestContext.Current.CancellationToken);

        Assert.Equal(
            SteamDeckCompatibilityCategory.Unknown,
            first[620]);

        Assert.Equal(
            SteamDeckCompatibilityCategory.Verified,
            second[620]);

        Assert.Equal(2, handler.CallCount);
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
        var result = await client.GetOwnedGames(76561197960287930L, ct: TestContext.Current.CancellationToken);

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
            () => client.GetOwnedGames(76561197960287930L, ct: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetOwnedGames_MalformedJson_Throws()
    {
        // Arrange
        var invalidJson = "{ \"response\": null }";
        var client = CreateClient(invalidJson, HttpStatusCode.OK);

        // Act / Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.GetOwnedGames(76561197960287930L, ct: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetOwnedGames_HttpError_DoesNotCacheFailure()
    {
        // Arrange
        var successPayload = JsonSerializer.Serialize(new
        {
            response = new
            {
                game_count = 1,
                games = new[]
                {
                    new { appid = 400, name = "Portal", playtime_forever = 600 }
                }
            }
        });
        var handler = new SequencedHttpMessageHandler(
            (string.Empty, HttpStatusCode.InternalServerError),
            (successPayload, HttpStatusCode.OK));
        var client = CreateClient(handler);

        // Act / Assert
        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.GetOwnedGames(76561197960287930L, ct: TestContext.Current.CancellationToken));

        var result = await client.GetOwnedGames(76561197960287930L, ct: TestContext.Current.CancellationToken);

        Assert.Equal(1, result.GameCount);
        Assert.Equal(400, result.Games[0].AppId);
        Assert.Equal(2, handler.CallCount);
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
        var result = await client.GetSteamIdFromVanityUrl("gabelogannewell", TestContext.Current.CancellationToken);

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
        var result = await client.GetSteamIdFromVanityUrl("some_fake_url_that_doesnt_exist", TestContext.Current.CancellationToken);

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
            () => client.GetSteamIdFromVanityUrl("error_route", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetSteamIdFromVanityUrl_HttpError_DoesNotCacheFailure()
    {
        // Arrange
        var successPayload = JsonSerializer.Serialize(new
        {
            response = new
            {
                success = 1,
                steamid = "76561197960287930"
            }
        });
        var handler = new SequencedHttpMessageHandler(
            (string.Empty, HttpStatusCode.InternalServerError),
            (successPayload, HttpStatusCode.OK));
        var client = CreateClient(handler);

        // Act / Assert
        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.GetSteamIdFromVanityUrl("gabelogannewell", TestContext.Current.CancellationToken));

        var result = await client.GetSteamIdFromVanityUrl("gabelogannewell", TestContext.Current.CancellationToken);

        Assert.Equal(76561197960287930L, result);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task GetSteamIdFromVanityUrl_UsesNormalizedVanityInApiRequest()
    {
        var successPayload = JsonSerializer.Serialize(new
        {
            response = new
            {
                success = 1,
                steamid = "76561197960287930"
            }
        });
        var handler = new SequencedHttpMessageHandler((successPayload, HttpStatusCode.OK));
        var client = CreateClient(handler);

        await client.GetSteamIdFromVanityUrl(
            "https:%2F%2Fsteamcommunity.com%2Fid%2FMister_God%2F",
            TestContext.Current.CancellationToken);

        var requestUri = Assert.Single(handler.RequestUris);
        Assert.Contains("vanityurl=mister_god", requestUri.Query, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("https://steamcommunity.com/profiles/76561197960287930/")]
    [InlineData("https://example.com/id/Mister_God/")]
    [InlineData("has space")]
    public async Task GetSteamIdFromVanityUrl_InvalidNormalizedVanity_ThrowsArgumentException(string vanityInput)
    {
        var client = CreateClient("{}", HttpStatusCode.OK);

        await Assert.ThrowsAsync<ArgumentException>(
            () => client.GetSteamIdFromVanityUrl(vanityInput, TestContext.Current.CancellationToken));
    }

    #endregion

    private SteamClient CreateClient(string responseContent, HttpStatusCode statusCode)
        => CreateClient(new MockHttpMessageHandler(responseContent, statusCode));

    private SteamClient CreateClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
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

