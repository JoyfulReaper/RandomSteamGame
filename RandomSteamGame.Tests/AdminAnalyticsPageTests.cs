
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Net;
using System.Security.Claims;

namespace RandomSteamGame.Tests;

[Collection(nameof(ServerBrowserExecutionTests))]
public sealed class AdminAnalyticsPageTests(SeoWebApplicationFactory factory)
    : IClassFixture<SeoWebApplicationFactory>
{
    private const string AdminSteamId = "76561198000000000";
    private const string OtherSteamId = "76561198000000001";
    private const string IdentityPrefix = "https://steamcommunity.com/openid/id/";
    private const string PageUrl = "/admin/analytics";

    [Fact]
    public async Task AnonymousUser_IsRedirectedToLogin()
    {
        using var app = CreateApplication();
        using var client = CreateClient(app);

        using var response = await client.GetAsync(
            PageUrl, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/auth/login", response.Headers.Location?.AbsolutePath);

        AssertPrivateHeaders(response);
    }

    [Fact]
    public async Task NonAdmin_CannotRenderDashboard()
    {
        using var app = CreateApplication();
        using var client = CreateClient(app, OtherSteamId);

        using var response = await client.GetAsync(
            PageUrl, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(
            "/Account/AccessDenied",
            response.Headers.Location?.AbsolutePath);

        var content = await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);

        Assert.DoesNotContain("Random Steam Analytics", content);

        AssertPrivateHeaders(response);
    }

    [Fact]
    public async Task Admin_CanRenderDashboard()
    {
        using var app = CreateApplication();
        using var client = CreateClient(app, AdminSteamId);

        using var response = await client.GetAsync(
            PageUrl, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);

        Assert.Contains("Random Steam Analytics", content);
        Assert.Contains("Loading analytics", content);

        AssertPrivateHeaders(response);
    }

    [Fact]
    public async Task InvalidSteamIdentity_CannotRenderDashboard()
    {
        using var app = CreateApplication();
        using var client = CreateClient(
            app,
            AdminSteamId,
            useFullIdentity: false);

        using var response = await client.GetAsync(
            PageUrl, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(
            "/Account/AccessDenied",
            response.Headers.Location?.AbsolutePath);

        AssertPrivateHeaders(response);
    }

    private static void AssertPrivateHeaders(HttpResponseMessage response)
    {
        Assert.Contains(
            "no-store",
            response.Headers.CacheControl?.ToString());

        Assert.Equal(
            "no-store",
            Assert.Single(response.Headers.GetValues("CDN-Cache-Control")));

        Assert.Equal(
            "noindex, nofollow",
            Assert.Single(response.Headers.GetValues("X-Robots-Tag")));
    }

    private WebApplicationFactory<Program> CreateApplication() =>
        factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["Authentication:AdminSteamIds:0"] = AdminSteamId
                    })));

    private static HttpClient CreateClient(
        WebApplicationFactory<Program> app,
        string? steamId = null,
        bool useFullIdentity = true)
    {
        var client = app.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = false
        });

        if (steamId is null)
            return client;

        var cookieOptions = app.Services
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);

        var identifier = useFullIdentity
            ? IdentityPrefix + steamId
            : steamId;

        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, identifier)],
            CookieAuthenticationDefaults.AuthenticationScheme));

        var ticket = new AuthenticationTicket(
            principal,
            CookieAuthenticationDefaults.AuthenticationScheme);

        var protectedTicket = cookieOptions.TicketDataFormat.Protect(ticket);

        client.DefaultRequestHeaders.TryAddWithoutValidation(
            "Cookie",
            $"{cookieOptions.Cookie.Name}={protectedTicket}");

        return client;
    }
}
