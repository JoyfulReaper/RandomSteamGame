using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Data.Sqlite;
using RandomSteamGame.Services;
using RandomSteamGame.Services.Interfaces;
using RandomSteamGame.Shared.Contracts;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RandomSteamGame.Tests;

[Collection(nameof(ServerBrowserExecutionTests))]
public sealed class FeatureVotingHttpTests(SeoWebApplicationFactory factory) : IClassFixture<SeoWebApplicationFactory>, IDisposable
{
    private readonly VotingTestDatabase _db = new();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ApplicationStartupRegistersEmptyVotingSchemaInExistingDatabase()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new("https://randomsteam.kgivler.com"), HandleCookies = false
        });
        using var response = await client.GetAsync("/api/features", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty((await response.Content.ReadFromJsonAsync<FeatureProposalResponse[]>(Ct))!);
        using var scope = factory.Services.CreateScope();
        await using var connection = scope.ServiceProvider.GetRequiredService<SqliteConnection>();
        await connection.OpenAsync(Ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_schema WHERE type='table' AND name IN ('FeatureProposals','FeatureVotes','Visitors','AppStats','RecentPicks');";
        Assert.Equal(5L, await command.ExecuteScalarAsync(Ct));
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("wrong-purpose")]
    [InlineData("invalid-payload")]
    [InlineData("oversized")]
    public async Task InvalidProtectedCookiesCannotRecoverAnotherBrowsersVotes(string kind)
    {
        _db.AddProposal(1);
        using var app = Application();
        using var client = Client(app);
        var browser = await Bootstrap(client);
        Assert.Equal(HttpStatusCode.NoContent, await Vote(client, browser, 1, true));
        var provider = app.Services.GetRequiredService<IDataProtectionProvider>();
        var protector = provider.CreateProtector("RandomSteamGame.FeatureVoting.BrowserIdentity.v1").ToTimeLimitedDataProtector();
        var value = kind switch
        {
            "expired" => protector.Protect(new string('a', 64), DateTimeOffset.UtcNow.AddDays(-1)),
            "wrong-purpose" => provider.CreateProtector("other-feature").Protect(new string('a', 64)),
            "invalid-payload" => protector.Protect(new string('z', 64), TimeSpan.FromDays(1)),
            _ => new string('x', 1025)
        };
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/features");
        request.Headers.Add("Cookie", FeatureVotingIdentity.CookieName + "=" + value);
        using var response = await client.SendAsync(request, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertNoCache(response);
        Assert.False(Assert.Single((await response.Content.ReadFromJsonAsync<FeatureProposalResponse[]>(Ct))!).Supported);
        Assert.NotEmpty(VotingCookie(response));
        Assert.Equal(1L, _db.Scalar("SELECT COUNT(*) FROM FeatureVotes;"));
    }

    [Fact]
    public async Task BrowserCanSupportMultipleProposalsAndWithdrawIdempotently()
    {
        _db.AddProposal(1);
        _db.AddProposal(2);
        using var app = Application();
        using var client = Client(app);
        var browser = await Bootstrap(client);
        Assert.Equal(0L, _db.Scalar("SELECT COUNT(*) FROM FeatureVotes;"));
        for (var i = 0; i < 2; i++)
            Assert.Equal(HttpStatusCode.NoContent, await Vote(client, browser, 1, true));
        Assert.Equal(HttpStatusCode.NoContent, await Vote(client, browser, 2, true));
        var votes = await Board(client, browser.Cookie);
        Assert.All(votes, p => { Assert.True(p.Supported); Assert.Equal(1, p.VoteCount); });
        var other = await Bootstrap(client);
        Assert.All(await Board(client, other.Cookie), p => Assert.False(p.Supported));
        Assert.Equal(HttpStatusCode.NoContent, await Vote(client, other, 1, true));
        for (var i = 0; i < 2; i++)
            Assert.Equal(HttpStatusCode.NoContent, await Vote(client, browser, 1, false));
        votes = await Board(client, browser.Cookie);
        Assert.False(votes[0].Supported);
        Assert.Equal(1, votes[0].VoteCount);
        Assert.True(votes[1].Supported);
        Assert.Equal(2L, _db.Scalar("SELECT COUNT(*) FROM FeatureVotes;"));
    }

    [Fact]
    public async Task ConcurrentHttpVotesCannotDuplicateRows()
    {
        _db.AddProposal(1);
        using var app = Application();
        using var client = Client(app);
        var browser = await Bootstrap(client);
        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => Task.Run(() => Vote(client, browser, 1, true), Ct)));
        Assert.All(results, status => Assert.Equal(HttpStatusCode.NoContent, status));
        Assert.Equal(1L, _db.Scalar("SELECT COUNT(*) FROM FeatureVotes;"));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("cookie-only")]
    [InlineData("token-only")]
    [InlineData("tampered")]
    [InlineData("other-cookie")]
    public async Task InvalidAntiforgeryRejectsBeforeWrites(string mode)
    {
        _db.AddProposal(1);
        using var app = Application();
        using var client = Client(app);
        var browser = await Bootstrap(client);
        using var request = Mutation(1, "{\"supported\":true}");
        if (mode is not "missing" and not "token-only")
            request.Headers.Add("Cookie", mode == "other-cookie" ? (await Bootstrap(client)).Cookie : browser.Cookie);
        if (mode is "token-only" or "tampered" or "other-cookie")
            request.Headers.Add(browser.HeaderName, mode == "tampered" ? "bad-token" : browser.Token);
        using var response = await client.SendAsync(request, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        AssertNoCache(response);
        Assert.Equal(0L, _db.Scalar("SELECT COUNT(*) FROM FeatureVotes;"));
        Assert.Equal(HttpStatusCode.NoContent, await Vote(client, browser, 1, true));
    }

    [Theory]
    [InlineData("0", "{\"supported\":true}", 400)]
    [InlineData("-1", "{\"supported\":true}", 400)]
    [InlineData("abc", "{\"supported\":true}", 400)]
    [InlineData("9223372036854775808", "{\"supported\":true}", 400)]
    [InlineData("1", "{}", 400)]
    [InlineData("1", "{\"supported\":null}", 400)]
    [InlineData("1", "{\"supported\":\"true\"}", 400)]
    [InlineData("1", "{\"supported\":true,\"browserHash\":\"spoof\"}", 400)]
    [InlineData("1", "not-json", 400)]
    [InlineData("99", "{\"supported\":true}", 404)]
    [InlineData("2", "{\"supported\":true}", 409)]
    [InlineData("2", "{\"supported\":false}", 409)]
    public async Task InvalidInputMissingAndClosedProposalsAreRejected(string id, string json, int expected)
    {
        _db.AddProposal(1);
        _db.AddProposal(2, "closed");
        using var app = Application();
        using var client = Client(app);
        var browser = await Bootstrap(client);
        using var request = Mutation(id, json);
        Authenticate(request, browser);
        using var response = await client.SendAsync(request, Ct);
        Assert.Equal(expected, (int)response.StatusCode);
        AssertNoCache(response);
        Assert.Equal(0L, _db.Scalar("SELECT COUNT(*) FROM FeatureVotes;"));
    }

    [Fact]
    public async Task PublicCookieFlagsPersistenceAndInvalidCookiesFollowDeploymentPolicy()
    {
        _db.AddProposal(1);
        using var app = Application();
        using var client = Client(app);
        using var first = await client.GetAsync("/api/features", Ct);
        var cookie = VotingCookie(first);
        Assert.Contains("; secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/features", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("max-age=31536000", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("domain=", cookie, StringComparison.OrdinalIgnoreCase);
        using var scope = app.Services.CreateScope();
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = cookie.Split(';')[0];
        var identity = scope.ServiceProvider.GetRequiredService<FeatureVotingIdentity>();
        var hash = identity.ReadHash(context);
        Assert.NotNull(hash);
        Assert.Equal(64, hash.Length);
        Assert.DoesNotContain(hash, cookie);
        var browser = await Bootstrap(client, cookie.Split(';')[0]);
        Assert.Equal(HttpStatusCode.NoContent, await Vote(client, browser, 1, true));
        Assert.Equal(hash, _db.Scalar("SELECT BrowserIdentityHash FROM FeatureVotes;"));
        Assert.True(Assert.Single(await Board(client, browser.Cookie)).Supported);
        // A new identity service can read the protected cookie with the same key ring.
        Assert.Equal(hash, new FeatureVotingIdentity(scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>(),
            scope.ServiceProvider.GetRequiredService<DeploymentCookiePolicy>()).ReadHash(context));

        using var invalid = new HttpRequestMessage(HttpMethod.Get, "/api/features");
        invalid.Headers.Add("Cookie", FeatureVotingIdentity.CookieName + "=tampered");
        using var replacement = await client.SendAsync(invalid, Ct);
        Assert.Equal(HttpStatusCode.OK, replacement.StatusCode);
        Assert.NotEqual(cookie, VotingCookie(replacement));
        Assert.False(Assert.Single((await replacement.Content.ReadFromJsonAsync<FeatureProposalResponse[]>(Ct))!).Supported);
        var invalidBrowser = browser with { Cookie = browser.Cookie.Replace(cookie.Split(';')[0], FeatureVotingIdentity.CookieName + "=tampered") };
        Assert.Equal(HttpStatusCode.BadRequest, await Vote(client, invalidBrowser, 1, true));
        Assert.Equal(HttpStatusCode.BadRequest, await Vote(client,
            browser with { Cookie = string.Join("; ", browser.Cookie.Split("; ").Where(c => !c.StartsWith(FeatureVotingIdentity.CookieName))) }, 1, true));
        Assert.Equal(1L, _db.Scalar("SELECT COUNT(*) FROM FeatureVotes;"));
    }

    [Fact]
    public async Task BrowserRateLimitDoesNotLimitOtherBrowsersOrSteamEndpoints()
    {
        _db.AddProposal(1);
        using var app = Application(settings: new() { ["FeatureVoting:BrowserPermitLimit"] = "1" });
        using var client = Client(app);
        var browser = await Bootstrap(client);
        Assert.Equal(HttpStatusCode.NoContent, await Vote(client, browser, 1, true));
        Assert.Equal(HttpStatusCode.TooManyRequests, await Vote(client, browser, 1, false));
        Assert.Equal(HttpStatusCode.NoContent, await Vote(client, await Bootstrap(client), 1, true));
        using var stats = await client.GetAsync("/api/stats", Ct);
        Assert.Equal(HttpStatusCode.OK, stats.StatusCode);
    }

    [Theory]
    [InlineData("Public", "198.51.100.10", null, false)]
    [InlineData("Public", "127.0.0.1", null, true)]
    [InlineData("Public", "127.0.0.1", "false", false)]
    public async Task IpRateLimitUsesOnlyTrustedEffectiveAddress(string mode, string peer, string? forwarding, bool distinctClients)
    {
        using var app = Application(mode, mode == "Public" ? "https://randomsteam.kgivler.com" : "http://example.b32.i2p",
            new() { ["FeatureVoting:IpPermitLimit"] = "1", ["Ingress:EnableForwardedHeaders"] = forwarding }, peer: peer);
        using var client = Client(app);
        foreach (var index in Enumerable.Range(1, 2))
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/features");
            request.Headers.Add("X-Forwarded-For", $"203.0.113.{index}");
            using var response = await client.SendAsync(request, Ct);
            Assert.Equal(index == 1 || distinctClients ? HttpStatusCode.OK : HttpStatusCode.TooManyRequests, response.StatusCode);
            AssertNoCache(response);
        }
    }

    [Fact]
    public async Task OverallRateLimitSurvivesRotatingTrustedIpsAndBrowserCookies()
    {
        using var app = Application(settings: new() { ["FeatureVoting:GlobalPermitLimit"] = "1" }, peer: "127.0.0.1");
        using var client = Client(app);
        using var first = new HttpRequestMessage(HttpMethod.Get, "/api/features");
        first.Headers.Add("X-Forwarded-For", "203.0.113.1");
        using var accepted = await client.SendAsync(first, Ct);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        using var second = new HttpRequestMessage(HttpMethod.Get, "/api/features");
        second.Headers.Add("X-Forwarded-For", "203.0.113.2");
        using var rejected = await client.SendAsync(second, Ct);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.NotNull(rejected.Headers.RetryAfter);
        AssertNoCache(rejected);
        Assert.False(rejected.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task OverallCapacityRejectsWithoutReachingPersistence()
    {
        var blocked = new BlockingVotingService();
        using var app = Application(settings: new() { ["FeatureVoting:GlobalConcurrency"] = "1" }, service: blocked);
        using var client = Client(app);
        var running = client.GetAsync("/api/features", Ct);
        try
        {
            await blocked.Started.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
            using var response = await client.GetAsync("/api/features", Ct);
            Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
            Assert.NotNull(response.Headers.RetryAfter);
            AssertNoCache(response);
            using var invalid = await client.PostAsJsonAsync("/api/features/1/vote", new { supported = true }, Ct);
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        }
        finally { blocked.Release.TrySetResult(); }
        using var accepted = await running;
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
    }

    [Fact]
    public async Task ContentionAndDatabaseErrorsReturnControlledFailures()
    {
        _db.AddProposal(1);
        using var app = Application();
        using var client = Client(app);
        var browser = await Bootstrap(client);
        using (var connection = _db.Open())
        using (var transaction = connection.BeginTransaction(deferred: false))
            Assert.Equal(HttpStatusCode.ServiceUnavailable, await Vote(client, browser, 1, true));
        Assert.Equal(HttpStatusCode.NoContent, await Vote(client, browser, 1, true));
        _db.Execute("DROP TABLE FeatureVotes;");
        using var response = await client.GetAsync("/api/features", Ct);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        AssertNoCache(response);
        var body = await response.Content.ReadAsStringAsync(Ct);
        Assert.DoesNotContain("SQLite", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("FeatureVotes", body);
        Assert.DoesNotContain(_db.Path, body);
    }

    [Fact]
    public async Task UnexpectedFailuresAreControlledAndCorsCannotReadPersonalizedResponses()
    {
        using var app = Application(service: new FailingVotingService());
        using var client = Client(app);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/features");
        request.Headers.Add("Origin", "https://randomsteam.kgivler.com");
        using var response = await client.SendAsync(request, Ct);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        AssertNoCache(response);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.DoesNotContain("internal-secret", await response.Content.ReadAsStringAsync(Ct));
    }

    [Theory]
    [InlineData("planned")]
    [InlineData("completed")]
    [InlineData("closed")]
    public async Task NonOpenStatusesRemainVisibleWithHistoricalVotesButRejectAllMutations(string status)
    {
        _db.AddProposal(1);
        using var app = Application();
        using var client = Client(app);
        var browser = await Bootstrap(client);
        Assert.Equal(HttpStatusCode.NoContent, await Vote(client, browser, 1, true));
        using (var connection = _db.Open())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "UPDATE FeatureProposals SET Status = $status;";
            command.Parameters.AddWithValue("$status", status);
            command.ExecuteNonQuery();
        }
        Assert.Equal(HttpStatusCode.Conflict, await Vote(client, browser, 1, true));
        Assert.Equal(HttpStatusCode.Conflict, await Vote(client, browser, 1, false));
        var proposal = Assert.Single(await Board(client, browser.Cookie));
        Assert.Equal(status, proposal.Status);
        Assert.True(proposal.Supported);
        Assert.Equal(1, proposal.VoteCount);
    }

    [Theory]
    [InlineData("DN42", "https://randomsteam.dn42")]
    [InlineData("I2P", "http://example.b32.i2p")]
    [InlineData("Yggdrasil", "http://steam.ygg.kgivler.com")]
    [InlineData("Public", "https://randomsteam.kgivler.com")]
    public async Task AltNetReturns404BeforeCookiesAntiforgeryRateLimitsAndPersistence(string name, string origin)
    {
        _db.AddProposal(1);
        using var publicApp = Application();
        using var publicClient = Client(publicApp);
        var browser = await Bootstrap(publicClient);
        Assert.Equal(HttpStatusCode.NoContent, await Vote(publicClient, browser, 1, true));
        using var altApp = Application("AltNet", origin, settings: new()
        {
            ["Application:NetworkName"] = name,
            ["FeatureVoting:GlobalPermitLimit"] = "1",
            ["FeatureVoting:IpPermitLimit"] = "1",
            ["Ingress:EnableForwardedHeaders"] = "true"
        }, peer: "127.0.0.1").WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IFeatureVotingService>();
            services.AddScoped<IFeatureVotingService>(_ => throw new InvalidOperationException("Disabled voting must not resolve persistence."));
        }));
        using var altClient = Client(altApp, origin);
        // Authenticated requests carry a real voter/antiforgery pair. Unauthenticated and
        // malformed requests must still return 404, rather than 400, 409, or rate-limit 429.
        foreach (var authenticated in new[] { false, true })
        foreach (var method in new[] { HttpMethod.Get, HttpMethod.Post })
        {
            using var request = method == HttpMethod.Get
                ? new HttpRequestMessage(method, "/api/features") : Mutation(1, "{\"supported\":false}");
            if (authenticated) Authenticate(request, browser);
            request.Headers.Host = "randomsteam.kgivler.com";
            request.Headers.Add("X-Forwarded-For", "203.0.113.1");
            request.Headers.Add("X-Forwarded-Proto", "https");
            using var response = await altClient.SendAsync(request, Ct);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            AssertNoCache(response);
            Assert.False(response.Headers.Contains("Set-Cookie"));
        }
        Assert.Equal(1L, _db.Scalar("SELECT COUNT(*) FROM FeatureVotes;"));
        Assert.True(Assert.Single(await Board(publicClient, browser.Cookie)).Supported);
    }

    [Theory]
    [InlineData("https://randomsteam.dn42")]
    [InlineData("https://steam.ygg.kgivler.com")]
    [InlineData("https://example.b32.i2p")]
    public async Task PublicModeIsDeploymentScopedEvenForAlternateHostnames(string origin)
    {
        using var app = Application(settings: new() { ["Application:NetworkName"] = "I2P" });
        using var client = Client(app, origin);
        using var response = await client.GetAsync("/api/features", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEmpty(VotingCookie(response));
        AssertNoCache(response);
    }

    private WebApplicationFactory<Program> Application(string mode = "Public", string origin = "https://randomsteam.kgivler.com",
        Dictionary<string, string?>? settings = null, string peer = "198.51.100.10", IFeatureVotingService? service = null) =>
        factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                var values = settings ?? new();
                values["Application:NetworkMode"] = mode;
                values["Application:CanonicalOrigin"] = origin;
                configuration.AddInMemoryCollection(values);
            });
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IFeatureVotingService>();
                services.AddSingleton<IFeatureVotingService>(service ?? _db.Service);
                services.AddSingleton<IStartupFilter>(new VotingPeerStartupFilter(peer));
            });
        });

    private static HttpClient Client(WebApplicationFactory<Program> app, string origin = "https://randomsteam.kgivler.com") =>
        app.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new(origin), HandleCookies = false });

    private static async Task<Browser> Bootstrap(HttpClient client, string? existingCookie = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/features");
        if (existingCookie is not null) request.Headers.Add("Cookie", existingCookie);
        using var board = await client.SendAsync(request, Ct);
        Assert.Equal(HttpStatusCode.OK, board.StatusCode);
        AssertNoCache(board);
        var voter = existingCookie ?? VotingCookie(board).Split(';')[0];
        if (existingCookie is not null) Assert.False(board.Headers.Contains("Set-Cookie"));
        using var token = await client.GetAsync("/api/antiforgery/token", Ct);
        var payload = await token.Content.ReadFromJsonAsync<JsonElement>(Ct);
        var anti = Assert.Single(token.Headers.GetValues("Set-Cookie")).Split(';')[0];
        return new(voter + "; " + anti, payload.GetProperty("requestToken").GetString()!, payload.GetProperty("headerName").GetString()!);
    }

    private static async Task<FeatureProposalResponse[]> Board(HttpClient client, string cookie)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/features");
        request.Headers.Add("Cookie", cookie);
        using var response = await client.SendAsync(request, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertNoCache(response);
        return (await response.Content.ReadFromJsonAsync<FeatureProposalResponse[]>(Ct))!;
    }

    private static async Task<HttpStatusCode> Vote(HttpClient client, Browser browser, long id, bool supported)
    {
        using var request = Mutation(id, JsonSerializer.Serialize(new { supported }));
        Authenticate(request, browser);
        using var response = await client.SendAsync(request, Ct);
        AssertNoCache(response);
        if (response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable)
            Assert.NotNull(response.Headers.RetryAfter);
        return response.StatusCode;
    }

    private static HttpRequestMessage Mutation(object id, string json) => new(HttpMethod.Post, $"/api/features/{id}/vote")
    { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };

    private static void Authenticate(HttpRequestMessage request, Browser browser)
    {
        request.Headers.Add("Cookie", browser.Cookie);
        request.Headers.Add(browser.HeaderName, browser.Token);
    }

    private static string VotingCookie(HttpResponseMessage response) =>
        Assert.Single(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith(FeatureVotingIdentity.CookieName + "="));

    private static void AssertNoCache(HttpResponseMessage response)
    {
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.True(response.Headers.CacheControl?.Private);
        Assert.Equal("no-store", Assert.Single(response.Headers.GetValues("CDN-Cache-Control")));
    }

    private sealed record Browser(string Cookie, string Token, string HeaderName);

    private sealed class VotingPeerStartupFilter(string peer) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, continuation) =>
            {
                context.Connection.RemoteIpAddress = IPAddress.Parse(peer);
                return continuation();
            });
            next(app);
        };
    }

    private sealed class BlockingVotingService : IFeatureVotingService
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<IReadOnlyList<FeatureProposalResponse>> GetAsync(string browserHash, CancellationToken ct)
        {
            Started.TrySetResult();
            await Release.Task.WaitAsync(ct);
            return [];
        }
        public Task<FeatureVoteResult> SetSupportedAsync(long featureId, string browserHash, bool supported, CancellationToken ct) =>
            throw new NotSupportedException();
    }

    private sealed class FailingVotingService : IFeatureVotingService
    {
        public Task<IReadOnlyList<FeatureProposalResponse>> GetAsync(string browserHash, CancellationToken ct) =>
            throw new InvalidOperationException("internal-secret");
        public Task<FeatureVoteResult> SetSupportedAsync(long featureId, string browserHash, bool supported, CancellationToken ct) =>
            throw new InvalidOperationException("internal-secret");
    }

    public void Dispose() => _db.Dispose();
}
