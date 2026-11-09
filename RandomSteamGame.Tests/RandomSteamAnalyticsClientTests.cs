using RandomSteamGame.Options;
using RandomSteamGame.Services.Analytics;
using System.Net;
using System.Net.Http.Json;

namespace RandomSteamGame.Tests;

public sealed class RandomSteamAnalyticsClientTests
{
    private const string ApiKey = "test-analytics-api-key-0123456789abcdef";
    private const string BaseUrl = "https://archive.example.test/";
    private const string OverviewPath = "api/randomsteam/analytics/overview";

    [Fact]
    public async Task GetOverviewAsync_SendsApiKeyAndDeserializesResponse()
    {
        var expected = new RandomSteamOverview(
            new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 11, 8, 0, 0, 0, TimeSpan.Zero),
            100, 45, 80, 75, 5, 12);

        var (http, client) = CreateClient((request, _) =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal(
                $"{BaseUrl}{OverviewPath}?days=7",
                request.RequestUri?.AbsoluteUri);

            Assert.Equal(
                ApiKey,
                Assert.Single(request.Headers.GetValues("X-RandomSteam-Analytics-Key")));

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(expected)
            });
        });

        using (http)
        {
            var actual = await client.GetOverviewAsync(7);
            Assert.Equal(expected, actual);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(91)]
    public async Task GetOverviewAsync_RejectsInvalidDays(int days)
    {
        var (http, client) = CreateClient((_, _) =>
            throw new InvalidOperationException("HTTP should not be called."));

        using (http)
        {
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
                () => client.GetOverviewAsync(days));
        }
    }

    [Fact]
    public async Task GetOverviewAsync_DisabledClientDoesNotSendRequest()
    {
        var (http, client) = CreateClient(
            (_, _) => throw new InvalidOperationException("HTTP should not be called."),
            enabled: false);

        using (http)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => client.GetOverviewAsync());
        }
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task GetOverviewAsync_PropagatesUpstreamHttpErrors(HttpStatusCode status)
    {
        var (http, client) = CreateClient((_, _) =>
            Task.FromResult(new HttpResponseMessage(status)));

        using (http)
        {
            var exception = await Assert.ThrowsAsync<HttpRequestException>(
                () => client.GetOverviewAsync());

            Assert.Equal(status, exception.StatusCode);
        }
    }

    [Fact]
    public async Task GetOverviewAsync_RejectsEmptyJsonResponse()
    {
        var (http, client) = CreateClient((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create<RandomSteamOverview>(null)
            }));

        using (http)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => client.GetOverviewAsync());
        }
    }

    [Fact]
    public async Task GetOverviewAsync_PropagatesCancellation()
    {
        var started = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var (http, client) = CreateClient(async (_, cancellationToken) =>
        {
            started.TrySetResult(true);

            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        using (http)
        using (var cancellation = new CancellationTokenSource())
        {
            var pending = client.GetOverviewAsync(
                cancellationToken: cancellation.Token);

            await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => pending);
        }
    }

    private static (HttpClient Http, RandomSteamAnalyticsClient Client) CreateClient(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler,
        bool enabled = true)
    {
        var http = new HttpClient(new StubHandler(handler))
        {
            BaseAddress = new Uri(BaseUrl)
        };

        var options = Microsoft.Extensions.Options.Options.Create(new RandomSteamAnalyticsOptions
        {
            Enabled = enabled,
            ArchiveBaseUrl = BaseUrl,
            ApiKey = ApiKey
        });

        return (http, new RandomSteamAnalyticsClient(http, options));
    }

    private sealed class StubHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return handler(request, cancellationToken);
        }
    }
}
