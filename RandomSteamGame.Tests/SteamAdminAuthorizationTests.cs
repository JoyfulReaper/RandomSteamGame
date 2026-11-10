
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Security.Claims;

namespace RandomSteamGame.Tests;

[Collection(nameof(ServerBrowserExecutionTests))]
public sealed class SteamAdminAuthorizationTests(SeoWebApplicationFactory factory)
    : IClassFixture<SeoWebApplicationFactory>
{
    private const string AdminSteamId = "76561198000000000";
    private const string OtherSteamId = "76561198000000001";
    private const string SteamIdentityPrefix = "https://steamcommunity.com/openid/id/";

    [Theory]
    [InlineData(SteamIdentityPrefix + AdminSteamId, true, true)]
    [InlineData(SteamIdentityPrefix + OtherSteamId, true, false)]
    [InlineData(AdminSteamId, true, false)]
    [InlineData("http://steamcommunity.com/openid/id/" + AdminSteamId, true, false)]
    [InlineData(SteamIdentityPrefix + AdminSteamId, false, false)]
    [InlineData(null, true, false)]
    public async Task SteamAdminPolicy_RequiresExactAuthenticatedSteamIdentity(
        string? nameIdentifier, bool authenticated, bool expected)
    {
        using var application = CreateApplication();

        var configuration = application.Services.GetRequiredService<IConfiguration>();

        Assert.Equal(
            AdminSteamId,
            configuration["Authentication:AdminSteamIds:0"]);

        Claim[] claims = nameIdentifier is null
            ? []
            : [new Claim(ClaimTypes.NameIdentifier, nameIdentifier)];

        var identity = new ClaimsIdentity(
            claims,
            authenticated ? CookieAuthenticationDefaults.AuthenticationScheme : null);

        var principal = new ClaimsPrincipal(identity);

        var authorization = application.Services.GetRequiredService<IAuthorizationService>();
        var result = await authorization.AuthorizeAsync(principal, null, "SteamAdmin");

        Assert.Equal(expected, result.Succeeded);
    }

    [Fact]
    public async Task SteamAdminPolicy_UsesCookieAuthentication()
    {
        using var application = CreateApplication();

        var provider = application.Services.GetRequiredService<IAuthorizationPolicyProvider>();
        var policy = await provider.GetPolicyAsync("SteamAdmin");

        Assert.NotNull(policy);
        Assert.Contains(CookieAuthenticationDefaults.AuthenticationScheme, policy.AuthenticationSchemes);
    }

    [Fact]
    public async Task AdminCheck_AnonymousUserIsRedirectedToLogin()
    {
        using var application = CreateApplication();

        using var client = application.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = false
        });

        using var response = await client.GetAsync(
            "/auth/admin-check",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.Equal("/auth/login", response.Headers.Location.AbsolutePath);
    }

    private WebApplicationFactory<Program> CreateApplication() =>
        factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Authentication:AdminSteamIds:0"] = AdminSteamId
                })));
}
