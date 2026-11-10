
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RandomSteamGame.Services.Analytics;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;

namespace RandomSteamGame.Tests;

[Collection(nameof(ServerBrowserExecutionTests))]
public sealed class RandomSteamAnalyticsEndpointTests(SeoWebApplicationFactory factory)
    : IClassFixture<SeoWebApplicationFactory>
{
    private const string AdminSteamId = "76561198000000000";
    private const string OtherSteamId = "76561198000000001";
    private const string IdentityPrefix = "https://steamcommunity.com/openid/id/";
    private const string ApiKey = "test-analytics-api-key-0123456789abcdef";
    private const string Url = "/api/admin/analytics/overview";

    [Fact]
    public async Task AnonymousUser_CannotAccessAnalytics()
    {
        int calls = 0;

        using var app = CreateApplication((_, _) =>
        {
            Interlocked.Increment(ref calls);
            throw new InvalidOperationException("Archive must not be called.");
        });

        using var client = CreateClient(app);
        using var response = await client.GetAsync(Url, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.Equal("/auth/login", response.Headers.Location.AbsolutePath);
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task NonAdmin_CannotAccessAnalytics()
    {
        int calls = 0;

        using var app = CreateApplication((_, _) =>
        {
            Interlocked.Increment(ref calls);
            throw new InvalidOperationException("Archive must not be called.");
        });

        using var client = CreateClient(app, IdentityPrefix + OtherSteamId);
        using var response = await client.GetAsync(Url, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task Admin_ReceivesAnalyticsOverview()
    {
        var expected = new RandomSteamOverview(
            new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 11, 8, 0, 0, 0, TimeSpan.Zero),
            100, 45, 80, 75, 5, 12);

        int calls = 0;

        using var app = CreateApplication((request, _) =>
        {
            Interlocked.Increment(ref calls);

            Assert.Equal(
                "https://archive.example.test/api/randomsteam/analytics/overview?days=7",
                request.RequestUri?.AbsoluteUri);

            Assert.Equal(
                ApiKey,
                Assert.Single(request.Headers.GetValues("X-RandomSteam-Analytics-Key")));

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(expected)
            });
        });

        using var client = CreateClient(app, IdentityPrefix + AdminSteamId);
        using var response = await client.GetAsync($"{Url}?days=7", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());

        var actual = await response.Content.ReadFromJsonAsync<RandomSteamOverview>(
            TestContext.Current.CancellationToken);

        Assert.Equal(expected, actual);
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(91)]
    public async Task Admin_InvalidDaysReturnsBadRequest(int days)
    {
        using var app = CreateApplication((_, _) =>
            throw new InvalidOperationException("Archive must not be called."));

        using var client = CreateClient(app, IdentityPrefix + AdminSteamId);
        using var response = await client.GetAsync(
            $"{Url}?days={days}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Admin_DisabledAnalyticsReturnsServiceUnavailable()
    {
        using var app = CreateApplication(
            (_, _) => throw new InvalidOperationException("Archive must not be called."),
            enabled: false);

        using var client = CreateClient(app, IdentityPrefix + AdminSteamId);
        using var response = await client.GetAsync(Url, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task Admin_UpstreamFailureReturnsServiceUnavailable(HttpStatusCode status)
    {
        using var app = CreateApplication((_, _) =>
            Task.FromResult(new HttpResponseMessage(status)));

        using var client = CreateClient(app, IdentityPrefix + AdminSteamId);
        using var response = await client.GetAsync(Url, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Admin_UpstreamTimeoutReturnsServiceUnavailable()
    {
        using var app = CreateApplication((_, _) =>
            throw new TaskCanceledException("Simulated upstream timeout."));

        using var client = CreateClient(app, IdentityPrefix + AdminSteamId);
        using var response = await client.GetAsync(Url, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Admin_InvalidUpstreamJsonReturnsServiceUnavailable()
    {
        using var app = CreateApplication((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("not valid JSON")
            }));

        using var client = CreateClient(app, IdentityPrefix + AdminSteamId);
        using var response = await client.GetAsync(Url, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    private WebApplicationFactory<Program> CreateApplication(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler,
        bool enabled = true)
    {
        return factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Authentication:AdminSteamIds:0"] = AdminSteamId,
                    ["RandomSteamAnalytics:Enabled"] = enabled.ToString(),
                    ["RandomSteamAnalytics:ArchiveBaseUrl"] = "https://archive.example.test/",
                    ["RandomSteamAnalytics:ApiKey"] = ApiKey
                }));

            builder.ConfigureTestServices(services =>
            {
                services.AddHttpClient<RandomSteamAnalyticsClient>()
                    .ConfigurePrimaryHttpMessageHandler(() => new StubHandler(handler));
            });
        });
    }

    private static HttpClient CreateClient(
        WebApplicationFactory<Program> app,
        string? identity = null)
    {
        var client = app.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = false
        });

        if (identity is not null)
        {
            var cookieOptions = app.Services
                .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
                .Get(CookieAuthenticationDefaults.AuthenticationScheme);

            var principal = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, identity)],
                CookieAuthenticationDefaults.AuthenticationScheme));

            var ticket = new AuthenticationTicket(
                principal,
                CookieAuthenticationDefaults.AuthenticationScheme);

            var protectedTicket = cookieOptions.TicketDataFormat.Protect(ticket);

            client.DefaultRequestHeaders.TryAddWithoutValidation(
                "Cookie",
                $"{cookieOptions.Cookie.Name}={protectedTicket}");
        }

        return client;
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
