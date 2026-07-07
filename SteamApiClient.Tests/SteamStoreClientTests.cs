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

public class SteamStoreClientTests
{
    private readonly IOptions<SteamClientApiOptions> _options;

    public SteamStoreClientTests()
    {
        _options = Options.Create(new SteamClientApiOptions
        {
            Cache = new CacheSettings
            {
                AppDetails = new CachePolicy { AbsoluteMinutes = 60 }
            }
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
        var client = CreateClient(jsonPayload, HttpStatusCode.OK);

        // Act
        var result = await client.GetAppData(
            targetAppId,
            ct: TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Portal", result.Name);
    }

    [Fact]
    public async Task GetAppData_ApiFails_ReturnsSuccessFalseResponse()
    {
        // Arrange
        var client = CreateClient(string.Empty, HttpStatusCode.InternalServerError);

        // Act
        var result = await client.GetAppData(
            123,
            ct: TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetAppData_ApiFails_DoesNotCacheNullResult()
    {
        // Arrange
        int targetAppId = 400;
        var successPayload = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            {
                targetAppId.ToString(), new
                {
                    success = true,
                    data = new { name = "Portal", steam_appid = targetAppId }
                }
            }
        });
        var handler = new SequencedHttpMessageHandler(
            (string.Empty, HttpStatusCode.InternalServerError),
            (successPayload, HttpStatusCode.OK));
        var client = CreateClient(handler);

        // Act
        var failedResult = await client.GetAppData(
            targetAppId,
            ct: TestContext.Current.CancellationToken);
        var successfulResult = await client.GetAppData(
            targetAppId,
            ct: TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(failedResult);
        Assert.NotNull(successfulResult);
        Assert.Equal("Portal", successfulResult.Name);
        Assert.Equal(2, handler.CallCount);
    }

    private SteamStoreClient CreateClient(string responseContent, HttpStatusCode statusCode)
        => CreateClient(new MockHttpMessageHandler(responseContent, statusCode));

    private SteamStoreClient CreateClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://store.steampowered.com/")
        };

        return new SteamStoreClient(
            httpClient,
            CreateCacheService(),
            _options,
            NullLogger<SteamStoreClient>.Instance);
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

public class SequencedHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<(string ResponseContent, HttpStatusCode StatusCode)> _responses;

    public SequencedHttpMessageHandler(params (string ResponseContent, HttpStatusCode StatusCode)[] responses)
    {
        _responses = new Queue<(string ResponseContent, HttpStatusCode StatusCode)>(responses);
    }

    public int CallCount { get; private set; }
    public List<Uri> RequestUris { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        CallCount++;
        if (request.RequestUri is not null)
        {
            RequestUris.Add(request.RequestUri);
        }

        var responseData = _responses.Count > 0
            ? _responses.Dequeue()
            : throw new InvalidOperationException("No response configured for this request.");

        var response = new HttpResponseMessage(responseData.StatusCode)
        {
            Content = new StringContent(responseData.ResponseContent)
        };

        return Task.FromResult(response);
    }
}
