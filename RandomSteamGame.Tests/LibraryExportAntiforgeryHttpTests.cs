using ErrorOr;
using JoyfulReaperLib.MissionControl;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RandomSteamGame.Services;
using RandomSteamGame.Events;
using RandomSteamGame.Services.Interfaces;
using RandomSteamGame.Shared.Contracts;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization.Metadata;
using DeckCategory = SteamApiClient.Contracts.SteamApi.SteamDeckCompatibilityCategory;

namespace RandomSteamGame.Tests;

[Collection(nameof(ServerBrowserExecutionTests))]
public sealed class LibraryExportAntiforgeryHttpTests : IClassFixture<SeoWebApplicationFactory>
{
    private const string ExportUrl = "/api/steam/76561197960287930/library/export.csv";
    private const string TokenUrl = "/api/antiforgery/token";
    private readonly SeoWebApplicationFactory _factory;
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public LibraryExportAntiforgeryHttpTests(SeoWebApplicationFactory factory) => _factory = factory;

    [Theory]
    [InlineData("PerIp", "missing")]
    [InlineData("PerIp", "cookieOnly")]
    [InlineData("PerIp", "tokenOnly")]
    [InlineData("PerIp", "tampered")]
    [InlineData("PerIp", "otherCookie")]
    [InlineData("PerIp", "crossSiteForm")]
    [InlineData("Global", "missing")]
    [InlineData("Global", "cookieOnly")]
    [InlineData("Global", "tokenOnly")]
    [InlineData("Global", "tampered")]
    [InlineData("Global", "otherCookie")]
    [InlineData("Global", "crossSiteForm")]
    public async Task InvalidCredentialsRejectBeforeExportWorkOrCooldown(string limitMode, string invalid)
    {
        var probe = new ExportProbe();
        using var application = CreateApplication(probe, "AltNet", "http://example.b32.i2p", limitMode);
        using var client = CreateClient(application, "http://example.b32.i2p");
        var tokens = await BootstrapAsync(client);
        AssertNoWork(probe, application);
        using var request = new HttpRequestMessage(HttpMethod.Post, ExportUrl);
        if (invalid is not "missing" and not "tokenOnly")
        {
            var cookie = invalid == "otherCookie" ? (await BootstrapAsync(client)).Cookie : tokens.Cookie;
            request.Headers.Add("Cookie", cookie);
        }
        if (invalid is "tokenOnly" or "tampered" or "otherCookie")
            request.Headers.Add(tokens.HeaderName, invalid == "tampered" ? "invalid-token" : tokens.RequestToken);
        if (invalid == "crossSiteForm")
        {
            // Supply even a real cookie: a cross-site simple form still cannot supply the secret request token.
            request.Headers.Add("Origin", "https://attacker.example");
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["steamId"] = "76561197960287930",
                ["__RequestVerificationToken"] = "attacker-token"
            });
        }
        using var rejected = await client.SendAsync(request, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.Contains("verification failed", await rejected.Content.ReadAsStringAsync(Ct));
        Assert.True(rejected.Headers.CacheControl?.NoStore);
        AssertNoWork(probe, application);

        using var accepted = await SendValidAsync(client, tokens);
        AssertSuccessfulExport(accepted, probe);
        using var cooldown = await SendValidAsync(client, tokens);
        Assert.Equal(HttpStatusCode.TooManyRequests, cooldown.StatusCode);
        Assert.Equal(1, probe.LibraryCalls);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("HEAD")]
    public async Task SafeMethodsCannotGenerateExport(string method)
    {
        var probe = new ExportProbe();
        using var application = CreateApplication(probe, "AltNet", "http://example.b32.i2p");
        using var client = CreateClient(application, "http://example.b32.i2p");
        using var request = new HttpRequestMessage(new HttpMethod(method), ExportUrl);
        using var rejected = await client.SendAsync(request, Ct);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, rejected.StatusCode);
        Assert.Contains("POST", rejected.Content.Headers.Allow);
        AssertNoWork(probe, application);
        using var accepted = await SendValidAsync(client, await BootstrapAsync(client));
        AssertSuccessfulExport(accepted, probe);
    }

    [Theory]
    [InlineData("Public", "https://randomsteam.kgivler.com", true)]
    [InlineData("AltNet", "https://randomsteam.dn42", true)]
    [InlineData("AltNet", "http://example.b32.i2p", false)]
    public async Task ValidPostPreservesCsvAndDeploymentAwareCookiePolicy(string mode, string origin, bool secure)
    {
        var probe = new ExportProbe();
        using var application = CreateApplication(probe, mode, origin);
        using var client = CreateClient(application, origin);
        var tokens = await BootstrapAsync(client);
        Assert.Equal(secure, tokens.SetCookie.Contains("; secure", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("httponly", tokens.SetCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", tokens.SetCookie, StringComparison.OrdinalIgnoreCase);
        AssertNoWork(probe, application);
        using var response = await SendValidAsync(client, tokens);
        AssertSuccessfulExport(response, probe);
        Assert.Equal("game,id,hours,hours_2_weeks,hours_windows,hours_mac,hours_linux,last_played,steam_deck,steam_store_url\r\nPortal,400,1.5,0.5,1,0.25,0.25,,verified,https://store.steampowered.com/app/400/\r\n", await response.Content.ReadAsStringAsync(Ct));
    }

    [Theory]
    [InlineData("https://attacker.example")]
    [InlineData("https://randomsteam.kgivler.com")]
    public async Task TokenBootstrapIsNotCorsReadableEvenForConfiguredOrigins(string origin)
    {
        var probe = new ExportProbe();
        using var application = CreateApplication(probe, "AltNet", "http://example.b32.i2p");
        using var client = CreateClient(application, "http://example.b32.i2p");
        using var request = new HttpRequestMessage(HttpMethod.Get, TokenUrl);
        request.Headers.Add("Origin", origin);
        using var response = await client.SendAsync(request, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Equal("no-store", Assert.Single(response.Headers.GetValues("CDN-Cache-Control")));
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>(Ct);
        Assert.Equal(new[] { "headerName", "requestToken" }, body!.Keys.Order().ToArray());
        AssertNoWork(probe, application);

        using var preflight = new HttpRequestMessage(HttpMethod.Options, ExportUrl);
        preflight.Headers.Add("Origin", origin);
        preflight.Headers.Add("Access-Control-Request-Method", "POST");
        preflight.Headers.Add("Access-Control-Request-Headers", "RequestVerificationToken");
        using var preflightResponse = await client.SendAsync(preflight, Ct);
        Assert.False(preflightResponse.Headers.Contains("Access-Control-Allow-Credentials"));
        Assert.DoesNotContain("RequestVerificationToken", string.Join(",", preflightResponse.Headers.TryGetValues("Access-Control-Allow-Headers", out var headers) ? headers : []), StringComparison.OrdinalIgnoreCase);
        AssertNoWork(probe, application);
    }

    [Fact]
    public async Task InvalidPostIsRejectedBeforeBusyGlobalAdmission()
    {
        var probe = new ExportProbe { Block = true };
        using var application = CreateApplication(probe, "AltNet", "http://example.b32.i2p", "Global");
        using var client = CreateClient(application, "http://example.b32.i2p");
        var tokens = await BootstrapAsync(client);
        var running = SendValidAsync(client, tokens);
        try
        {
            await probe.Started.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
            using var invalid = await client.PostAsync(ExportUrl, null, Ct);
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
            Assert.Empty(probe.Events);
            Assert.Equal(1, probe.LibraryCalls);
        }
        finally
        {
            probe.Release.TrySetResult();
        }
        using var accepted = await running;
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
    }

    private WebApplicationFactory<Program> CreateApplication(ExportProbe probe, string mode, string origin, string limitMode = "PerIp") =>
        _factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Application:NetworkMode"] = mode,
                ["Application:CanonicalOrigin"] = origin,
                ["Steam:LibraryExport:RateLimitMode"] = limitMode,
                ["Cors:AllowedOrigins:0"] = "https://attacker.example"
            }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IGameProvider>();
                services.AddSingleton<IGameProvider>(probe);
                services.RemoveAll<IAppStatsService>();
                services.AddSingleton<IAppStatsService>(probe);
                services.RemoveAll<ISteamLibraryExportService>();
                services.AddSingleton<ISteamLibraryExportService>(probe);
                services.RemoveAll<ILibraryExportCooldownTracker>();
                services.AddSingleton<ILibraryExportCooldownTracker>(probe);
                services.RemoveAll<IMissionControlClient>();
                services.AddSingleton<IMissionControlClient>(probe);
            });
        });

    private static HttpClient CreateClient(WebApplicationFactory<Program> application, string origin) =>
        application.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri(origin), HandleCookies = false, AllowAutoRedirect = false });

    private static async Task<Tokens> BootstrapAsync(HttpClient client)
    {
        using var response = await client.GetAsync(TokenUrl, Ct);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<TokenPayload>(Ct);
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), value => value.StartsWith(".RandomSteamGame.Antiforgery.v2="));
        return new(payload!.RequestToken, payload.HeaderName, cookie.Split(';')[0], cookie);
    }

    private static async Task<HttpResponseMessage> SendValidAsync(HttpClient client, Tokens tokens)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, ExportUrl);
        request.Headers.Add("Cookie", tokens.Cookie);
        request.Headers.Add(tokens.HeaderName, tokens.RequestToken);
        return await client.SendAsync(request, Ct);
    }

    private static void AssertNoWork(ExportProbe probe, WebApplicationFactory<Program> application)
    {
        Assert.Equal(0, probe.LibraryCalls);
        Assert.Equal(0, probe.DeckCalls);
        Assert.Equal(0, probe.CsvCalls);
        Assert.Equal(0, probe.StatsCalls);
        Assert.Equal(0, probe.CooldownChecks);
        Assert.Equal(0, probe.Marks);
        Assert.Empty(probe.Events);
        Assert.Null(application.Services.GetRequiredService<GlobalLibraryExportCooldownTracker>().GetRetryAfter());
    }

    private static void AssertSuccessfulExport(HttpResponseMessage response, ExportProbe probe)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("utf-8", response.Content.Headers.ContentType?.CharSet);
        Assert.Equal("steam-library-76561197960287930.csv", response.Content.Headers.ContentDisposition?.FileName);
        Assert.True(response.Headers.CacheControl?.Private);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Equal("no-store", Assert.Single(response.Headers.GetValues("CDN-Cache-Control")));
        Assert.Equal(1, probe.LibraryCalls);
        Assert.Equal(1, probe.DeckCalls);
        Assert.Equal(1, probe.CsvCalls);
        Assert.Equal(1, probe.StatsCalls);
        Assert.Single(probe.Events);
    }

    private sealed record TokenPayload(string RequestToken, string HeaderName);
    private sealed record Tokens(string RequestToken, string HeaderName, string Cookie, string SetCookie);

    private sealed class ExportProbe : IGameProvider, ISteamDeckCompatibilityProvider, IAppStatsService,
        ISteamLibraryExportService, ILibraryExportCooldownTracker, IMissionControlClient
    {
        public string ProviderKey => "steam";
        public int LibraryCalls, DeckCalls, CsvCalls, StatsCalls, CooldownChecks, Marks;
        public List<string> Events { get; } = [];
        public bool Block { get; init; }
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<ErrorOr<OwnedGamesResponse>> GetOwnedGamesAsync(long userId, CancellationToken ct = default)
        {
            Interlocked.Increment(ref LibraryCalls);
            Started.TrySetResult();
            if (Block) await Release.Task.WaitAsync(ct);
            return new OwnedGamesResponse(userId, 1, [new Game(400, "Portal", 90, null, 60, 15, 15, 0, 30)]);
        }
        public Task<IReadOnlyDictionary<int, DeckCategory>> GetSteamDeckCompatibilityAsync(IEnumerable<int> appIds, CancellationToken ct = default)
        {
            DeckCalls++;
            return Task.FromResult<IReadOnlyDictionary<int, DeckCategory>>(new Dictionary<int, DeckCategory> { [400] = DeckCategory.Verified });
        }
        public byte[] Export(OwnedGamesResponse library, IReadOnlyDictionary<int, DeckCategory> compatibility)
        {
            CsvCalls++;
            return new SteamLibraryExportService().Export(library, compatibility);
        }
        public TimeSpan? GetRetryAfter(string partitionKey)
        {
            CooldownChecks++;
            return Marks == 0 ? null : TimeSpan.FromHours(72);
        }
        public void MarkSucceeded(string partitionKey) => Marks++;
        public Task IncrementLibrariesExportedAsync() { StatsCalls++; return Task.CompletedTask; }
        public Task IncrementRandomGamesGeneratedAsync() => throw new NotSupportedException();
        public Task<AppStatsResponse> GetStatsAsync() => throw new NotSupportedException();
        public Task<AppStatsResponse> RecordHitAsync(string ip, string? userAgent = null, string ingressNetwork = IngressNetworkClassifier.Unknown) => throw new NotSupportedException();
        public Task<ErrorOr<GameDetails>> GetRandomGameDetailsAsync(long userId, bool unplayedOnly = false, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<RandomGamePickAttempt> GetRandomGamePickAsync(long userId, bool unplayedOnly = false, IReadOnlyCollection<int>? excludedGameIds = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ErrorOr<long>> ResolveIdentifierAsync(string identifier, CancellationToken ct = default) => throw new NotSupportedException();
        public Task InvalidateOwnedGamesCacheAsync(long userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> TryPublishAsync<TPayload>(string eventType, TPayload payload, JsonTypeInfo<TPayload> payloadTypeInfo,
            DateTimeOffset occurredAt, string? correlationId = null, CancellationToken cancellationToken = default)
        {
            if (payload is LibraryExportCompletedEvent or LibraryExportRejectedEvent)
                Events.Add(eventType);
            return Task.FromResult(true);
        }
    }
}
