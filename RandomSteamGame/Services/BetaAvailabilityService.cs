using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using RandomSteamGame.Options;
using RandomSteamGame.Services.Interfaces;

namespace RandomSteamGame.Services;

public sealed class BetaAvailabilityService : IBetaAvailabilityService
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(2);
    private static readonly Uri BetaUri = new("https://randombeta.kgivler.com/api/stats");

    private readonly IMemoryCache _cache;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<BetaAvailabilityService> _logger;
    private readonly ApplicationOptions _applicationOptions;

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

    public Task<bool> IsBetaAvailableAsync(CancellationToken cancellationToken = default)
    {
        if (!_applicationOptions.BetaProbeEnabled)
        {
            return Task.FromResult(false);
        }

        return _cache.GetOrCreateAsync("beta-picker-availability", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;

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
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Beta availability probe failed.");
                return false;
            }
        }) ?? Task.FromResult(false);
    }
}
