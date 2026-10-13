using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using RandomSteamGame.Options;
using RandomSteamGame.Services.Interfaces;

namespace RandomSteamGame.Services;

public sealed class BetaAvailabilityService : IBetaAvailabilityService, IDisposable
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(2);
    private static readonly Uri BetaUri = new("https://randombeta.kgivler.com/api/stats");

    private readonly IMemoryCache _cache;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<BetaAvailabilityService> _logger;
    private readonly ApplicationOptions _applicationOptions;
    private readonly SemaphoreSlim _probeGate = new(1, 1);

    public BetaAvailabilityService(
        IMemoryCache cache,
        IHttpClientFactory httpClientFactory,
        ILogger<BetaAvailabilityService> logger,
        IOptions<ApplicationOptions> applicationOptions)
    {
        _cache = cache;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _applicationOptions = applicationOptions.Value;
    }

    public async Task<bool> IsBetaAvailableAsync(CancellationToken cancellationToken = default)
    {
        if (!_applicationOptions.BetaProbeEnabled)
        {
            return false;
        }

        if (_cache.TryGetValue<bool>("beta-picker-availability", out var cached))
            return cached;

        await _probeGate.WaitAsync(cancellationToken);
        try
        {
            // Another request may have filled the cache while this caller waited.
            if (_cache.TryGetValue<bool>("beta-picker-availability", out cached))
                return cached;

            var available = await ProbeBetaAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            _cache.Set("beta-picker-availability", available, CacheDuration);
            return available;
        }
        finally
        {
            _probeGate.Release();
        }
    }

    private async Task<bool> ProbeBetaAsync(CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(ProbeTimeout);

        try
        {
            var client = _httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, BetaUri);
            using var response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeoutCts.Token);

            return response.IsSuccessStatusCode || (int)response.StatusCode is >= 300 and < 400;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug("Beta availability probe timed out.");
            return false;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Beta availability probe failed.");
            return false;
        }
    }

    public void Dispose() => _probeGate.Dispose();
}
