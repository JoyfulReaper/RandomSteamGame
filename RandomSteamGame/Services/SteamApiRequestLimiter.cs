using SteamApiClient.Settings;
using System.Threading.RateLimiting;

namespace RandomSteamGame.Services;

// One admission window shared by HTTP middleware and direct server component calls.
public sealed class SteamApiRequestLimiter : RateLimiter
{
    private readonly FixedWindowRateLimiter _limiter;

    // This application-wide instance lives until shutdown, not until a middleware
    // partition becomes idle. A null duration prevents partition eviction/disposal.
    public override TimeSpan? IdleDuration => null;

    public override RateLimiterStatistics? GetStatistics() => _limiter.GetStatistics();

    public SteamApiRequestLimiter(RateLimitingOptions options)
    {
        _limiter = new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
        {
            Window = TimeSpan.FromSeconds(options.WindowSeconds),
            PermitLimit = options.PermitLimit,
            AutoReplenishment = true,
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        });
    }

    protected override RateLimitLease AttemptAcquireCore(int permitCount) => _limiter.AttemptAcquire(permitCount);

    protected override ValueTask<RateLimitLease> AcquireAsyncCore(int permitCount, CancellationToken cancellationToken) =>
        _limiter.AcquireAsync(permitCount, cancellationToken);

    protected override ValueTask DisposeAsyncCore() => _limiter.DisposeAsync();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _limiter.Dispose();
        }
        base.Dispose(disposing);
    }
}
