using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RandomSteamGame.Services;
using RandomSteamGame.Shared.Contracts;
using System.Net;

namespace RandomSteamGame.Tests;

public sealed class IngressAndCookieHttpTests(SeoWebApplicationFactory factory) : IClassFixture<SeoWebApplicationFactory>
{
    [Theory]
    [InlineData("Public", "https://randomsteam.kgivler.com", "Production", "127.0.0.1", null, null, "https", null, "https")]
    [InlineData("Public", "https://randomsteam.kgivler.com", "Production", "127.0.0.1", null, "true", null, "{\"scheme\":\"https\"}", "https")]
    [InlineData("Public", "https://randomsteam.kgivler.com", "Production", "198.51.100.10", null, null, "https", "{\"scheme\":\"https\"}", "http")]
    [InlineData("Public", "https://randomsteam.kgivler.com", "Production", "127.0.0.1", "false", null, "https", "{\"scheme\":\"https\"}", "http")]
    [InlineData("Public", "https://randomsteam.kgivler.com", "Production", "127.0.0.1", null, null, null, "malformed", "http")]
    [InlineData("Public", "https://randomsteam.kgivler.com", "Production", "127.0.0.1", null, "false", null, "{\"scheme\":\"https\"}", "http")]
    [InlineData("AltNet", "http://example.b32.i2p", "Production", "127.0.0.1", "true", "true", "https", "{\"scheme\":\"https\"}", "http")]
    [InlineData("AltNet", "http://example.b32.i2p", "Production", "198.51.100.10", "true", "true", "https", "{\"scheme\":\"https\"}", "http")]
    [InlineData("AltNet", "https://randomsteam.dn42", "Production", "127.0.0.1", null, null, null, null, "https")]
    [InlineData("Public", "https://randomsteam.kgivler.com", "Development", "127.0.0.1", null, null, "https", null, "https")]
    [InlineData("AltNet", "https://randomsteam.dn42", "Development", "127.0.0.1", null, null, null, null, "https")]
    public async Task HstsUsesTrustedEffectiveScheme(
        string mode, string origin, string environment, string peer, string? forwarding,
        string? cloudflare, string? forwardedScheme, string? cfVisitor, string expectedScheme)
    {
        using var application = CreateApplication(mode, origin, peer, environment, forwarding, cloudflare);
        // Use HTTP on the backend and a host outside the framework's HSTS loopback exclusions.
        using var client = application.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://ingress.example"),
            AllowAutoRedirect = false
        });
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        if (forwardedScheme is not null)
            request.Headers.Add("X-Forwarded-Proto", forwardedScheme);
        if (cfVisitor is not null)
            request.Headers.Add("CF-Visitor", cfVisitor);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expectedScheme, Assert.Single(response.Headers.GetValues("X-Test-Scheme")));
        AssertHsts(response, environment == "Production" && expectedScheme == "https");
    }

    [Theory]
    [InlineData("Public", "https://randomsteam.kgivler.com", "https", true)]
    [InlineData("AltNet", "http://example.b32.i2p", "http", false)]
    [InlineData("AltNet", "https://randomsteam.dn42", "https", true)]
    public async Task HstsAndProductionErrorHandlingUseEffectiveScheme(
        string mode, string origin, string expectedScheme, bool expectedHsts)
    {
        using var application = CreateApplication(mode, origin, "127.0.0.1", "Production", "true", "true")
            .WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
                services.AddControllers().AddApplicationPart(typeof(SeoTestFailureController).Assembly)));
        using var client = application.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://ingress.example"),
            AllowAutoRedirect = false
        });
        using var request = new HttpRequestMessage(HttpMethod.Get, "/test-only/seo-failure");
        request.Headers.Add("X-Forwarded-Proto", "https");
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(expectedScheme, Assert.Single(response.Headers.GetValues("X-Test-Scheme")));
        Assert.Equal("noindex, nofollow", Assert.Single(response.Headers.GetValues("X-Robots-Tag")));
        Assert.Contains("500: CORE UNSTABLE", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        AssertHsts(response, expectedHsts);
    }

    private static void AssertHsts(HttpResponseMessage response, bool expected)
    {
        if (expected)
            Assert.Equal("max-age=2592000", Assert.Single(response.Headers.GetValues("Strict-Transport-Security")));
        else
            Assert.False(response.Headers.Contains("Strict-Transport-Security"));
    }

    [Theory]
    [InlineData("Public", "https://randomsteam.kgivler.com", "Production", true, true)]
    [InlineData("Public", "https://randomsteam.kgivler.com", "Development", false, true)]
    [InlineData("AltNet", "http://example.b32.i2p", "Production", false, false)]
    [InlineData("AltNet", "https://randomsteam.dn42", "Production", true, true)]
    public async Task CookiesAndAntiforgeryRoundTripFollowDeploymentPolicy(
        string mode, string origin, string environment, bool secureAntiforgery, bool secureIdentity)
    {
        using var application = CreateApplication(mode, origin, "127.0.0.1", environment);
        using var client = application.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        using var request = new HttpRequestMessage(HttpMethod.Get, "/library-export");
        // Spoofed HTTPS must not turn HTTP AltNet cookies into Secure cookies.
        if (environment == "Production")
        {
            request.Headers.Add("X-Forwarded-Proto", "https");
            request.Headers.Add("CF-Visitor", "{\"scheme\":\"https\"}");
        }
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), value => value.StartsWith(".RandomSteamGame.Antiforgery.v2="));
        Assert.Equal(secureAntiforgery, cookie.Contains("; secure", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("domain=", cookie, StringComparison.OrdinalIgnoreCase);

        var document = await new HtmlParser().ParseDocumentAsync(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var token = Assert.IsType<string>(document.QuerySelector("input[name='__RequestVerificationToken']")?.GetAttribute("value"));
        using var scope = application.Services.CreateScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Request.Scheme = Assert.Single(response.Headers.GetValues("X-Test-Scheme"));
        context.Request.Method = "POST";
        context.Request.Headers.Cookie = cookie.Split(';')[0];
        var antiforgeryOptions = scope.ServiceProvider.GetRequiredService<IOptions<AntiforgeryOptions>>().Value;
        context.Request.Headers[antiforgeryOptions.HeaderName!] = token;
        var antiforgery = scope.ServiceProvider.GetRequiredService<IAntiforgery>();
        await antiforgery.ValidateRequestAsync(context);
        context.Request.Headers.Remove(antiforgeryOptions.HeaderName!);
        await Assert.ThrowsAsync<AntiforgeryValidationException>(() => antiforgery.ValidateRequestAsync(context));

        var policy = application.Services.GetRequiredService<DeploymentCookiePolicy>();
        // Identity flags also come from deployment configuration, even on a misleading request scheme.
        var identityContext = new DefaultHttpContext();
        identityContext.Request.Scheme = secureIdentity ? "http" : "https";
        var accessor = new HttpContextAccessor { HttpContext = identityContext };
        var writer = new ServerSteamIdentityWriter(accessor, policy);
        await writer.SetIdentityAsync(new SteamIdentity("76561197960287930", null, true));
        var identityCookies = identityContext.Response.Headers.SetCookie.ToArray();
        Assert.Equal(3, identityCookies.Length);
        foreach (var value in identityCookies)
        {
            Assert.Equal(secureIdentity, value!.Contains("; secure", StringComparison.OrdinalIgnoreCase));
            Assert.Contains("samesite=lax", value, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("domain=", value, StringComparison.OrdinalIgnoreCase);
        }
        identityContext.Request.Headers.Cookie = string.Join("; ", identityCookies.Take(2).Select(value => value!.Split(';')[0]));
        var identity = await new ServerSteamIdentityReader(accessor).GetIdentityAsync();
        Assert.Equal("76561197960287930", identity.SteamId);
        Assert.True(identity.UnplayedOnly);
        identityContext.Response.Headers.SetCookie = default;
        await writer.ClearAsync();
        Assert.Equal(3, identityContext.Response.Headers.SetCookie.Count);
        Assert.All(identityContext.Response.Headers.SetCookie, value =>
            Assert.Equal(secureIdentity, value!.Contains("; secure", StringComparison.OrdinalIgnoreCase)));
    }

    [Theory]
    [InlineData("Public", "198.51.100.10", null, "http", "198.51.100.10")]
    [InlineData("Public", null, null, "http", "unknown")]
    [InlineData("Public", "127.0.0.1", null, "https", "203.0.113.9")]
    [InlineData("Public", "::1", null, "https", "203.0.113.9")]
    [InlineData("Public", "::ffff:127.0.0.1", null, "https", "203.0.113.9")]
    [InlineData("Public", "127.0.0.1", "false", "http", "127.0.0.1")]
    [InlineData("AltNet", "127.0.0.1", null, "http", "127.0.0.1")]
    [InlineData("AltNet", "127.0.0.1", "true", "http", "203.0.113.9")]
    [InlineData("AltNet", "198.51.100.10", "true", "http", "198.51.100.10")]
    public async Task ForwardingHonorsModeAndImmediatePeerTrust(
        string mode, string? peer, string? enabled, string expectedScheme, string expectedIp)
    {
        using var application = CreateApplication(mode,
            mode == "Public" ? "https://randomsteam.kgivler.com" : "http://example.b32.i2p", peer,
            forwarding: enabled);
        using var client = application.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add("X-Forwarded-For", "192.0.2.99, 203.0.113.9");
        request.Headers.Add("X-Forwarded-Proto", "https");
        request.Headers.Add("CF-Visitor", "{\"scheme\":\"https\"}");
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expectedScheme, Assert.Single(response.Headers.GetValues("X-Test-Scheme")));
        Assert.Equal(expectedIp, Assert.Single(response.Headers.GetValues("X-Test-Peer")));
    }

    [Theory]
    [InlineData(null, "{\"scheme\":\"https\"}", "https")]
    [InlineData(null, "{ \"scheme\" : \"https\" }", "https")]
    [InlineData(null, "malformed", "http")]
    [InlineData("false", "{\"scheme\":\"https\"}", "http")]
    public async Task CloudflareSchemeIsOptionalAndTrusted(string? enabled, string header, string expectedScheme)
    {
        using var application = CreateApplication("Public", "https://randomsteam.kgivler.com", "127.0.0.1", cloudflare: enabled);
        using var client = application.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add("CF-Visitor", header);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(expectedScheme, Assert.Single(response.Headers.GetValues("X-Test-Scheme")));
    }

    [Theory]
    [InlineData("10.0.0.5", "203.0.113.9")]
    [InlineData("127.0.0.1", "127.0.0.1")]
    public async Task ExplicitTrustedProxiesReplaceLoopbackDefaults(string peer, string expectedIp)
    {
        using var application = CreateApplication("AltNet", "http://example.b32.i2p", peer, forwarding: "true", trustedProxy: "10.0.0.5");
        using var client = application.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add("X-Forwarded-For", "203.0.113.9");
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(expectedIp, Assert.Single(response.Headers.GetValues("X-Test-Peer")));
    }

    private WebApplicationFactory<Program> CreateApplication(string mode, string origin, string? peer,
        string environment = "Development", string? forwarding = null, string? cloudflare = null, string? trustedProxy = null) =>
        factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                var settings = new Dictionary<string, string?>
                {
                    ["Application:NetworkMode"] = mode,
                    ["Application:CanonicalOrigin"] = origin,
                    ["Ingress:EnableForwardedHeaders"] = forwarding,
                    ["Ingress:EnableCloudflareVisitorHeader"] = cloudflare
                };
                if (trustedProxy is not null)
                {
                    settings["Ingress:TrustedProxies:0"] = trustedProxy;
                }
                configuration.AddInMemoryCollection(settings);
            });
            builder.ConfigureTestServices(services => services.AddSingleton<IStartupFilter>(new PeerStartupFilter(peer)));
        });

    private sealed class PeerStartupFilter(string? peer) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, continuation) =>
            {
                context.Connection.RemoteIpAddress = peer is null ? null : IPAddress.Parse(peer);
                context.Response.OnStarting(() =>
                {
                    context.Response.Headers["X-Test-Scheme"] = context.Request.Scheme;
                    context.Response.Headers["X-Test-Peer"] = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                    return Task.CompletedTask;
                });
                return continuation();
            });
            next(app);
        };
    }
}
