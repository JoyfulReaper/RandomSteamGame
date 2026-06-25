/*
 * Steam Api Client
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using SteamApiClient.Contracts.SteamStoreApi;
using SteamApiClient.HttpClients;
using SteamApiClient.Services;
using SteamApiClient.Settings;
using System.Net;
using System.Text.Json;

namespace SteamApiClient.Tests;

public class SteamStoreClientTests
{
    private readonly ICacheService _mockCache;
    private readonly IOptions<SteamClientApiOptions> _options;

    public SteamStoreClientTests()
    {
        _mockCache = Substitute.For<ICacheService>();

        _options = Options.Create(new SteamClientApiOptions
        {
            Cache = new CacheSettings
            {
                AppDetails = new CachePolicy { AbsoluteMinutes = 60 }
            }
        });

        // simulate a cache miss
        _mockCache.GetOrCreateAsync(
            Arg.Any<string>(),
            Arg.Any<Func<CancellationToken, Task<AppDetailsResponse>>>(),
            Arg.Any<CachePolicy>(),
            Arg.Any<CancellationToken>())
        .Returns(async callInfo =>
        {
            var factory = callInfo.Arg<Func<CancellationToken, Task<AppDetailsResponse>>>();
            var ct = callInfo.Arg<CancellationToken>();
            return await factory(ct);
        });
    }

    [Fact]
    public async Task GetAppData_ValidAppId_ReturnsSuccessfulResponse()
    {
        // Arrange
        int targetAppId = 400; // Portal
        var fakeSteamResponse = new Dictionary<string, object>
        {
            {
                targetAppId.ToString(), new
                {
                    success = true,
                    data = new { name = "Portal", steam_appid = targetAppId }
                }
            }
        };

        var jsonPayload = JsonSerializer.Serialize(fakeSteamResponse);

        // Mock the network layer handler
        var mockHandler = new MockHttpMessageHandler(jsonPayload, HttpStatusCode.OK);
        var httpClient = new HttpClient(mockHandler);

        var client = new SteamStoreClient(
            httpClient,
            _mockCache,
            _options,
            NullLogger<SteamStoreClient>.Instance // dummy logger
        );

        // Act
        var result = await client.GetAppData(targetAppId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.AppData);
        Assert.Equal("Portal", result.AppData.Name);
    }

    [Fact]
    public async Task GetAppData_ApiFails_ReturnsSuccessFalseResponse()
    {
        // Arrange
        var mockHandler = new MockHttpMessageHandler(string.Empty, HttpStatusCode.InternalServerError);
        var httpClient = new HttpClient(mockHandler);

        var client = new SteamStoreClient(httpClient, _mockCache, _options, NullLogger<SteamStoreClient>.Instance);

        // Act
        var result = await client.GetAppData(123);

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.AppData);
    }
}


// handler to intercept HttpClient queries
public class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly string _responseContent;
    private readonly HttpStatusCode _statusCode;

    public MockHttpMessageHandler(string responseContent, HttpStatusCode statusCode)
    {
        _responseContent = responseContent;
        _statusCode = statusCode;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var response = new HttpResponseMessage(_statusCode)
        {
            Content = new StringContent(_responseContent)
        };

        return Task.FromResult(response);
    }
}