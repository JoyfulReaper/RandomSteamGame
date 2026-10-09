using Microsoft.Extensions.Options;
using RandomSteamGame.Options;

namespace RandomSteamGame.Services;

// Process-local reservation. The existing concurrency limiter holds a permit for the entire request.
public sealed class GlobalLibraryExportCooldownTracker(
    IOptions<LibraryExportOptions> options,
    TimeProvider timeProvider)
{
    private readonly object _sync = new();
    private DateTimeOffset _nextAvailableAt;

    public TimeSpan? TryStart()
    {
        lock (_sync)
        {
            var now = timeProvider.GetUtcNow();
            if (_nextAvailableAt > now)
            {
                return _nextAvailableAt - now;
            }

            _nextAvailableAt = now.AddMinutes(options.Value.GlobalCooldownMinutes);
            return null;
        }
    }

    public TimeSpan? GetRetryAfter()
    {
        lock (_sync)
        {
            var remaining = _nextAvailableAt - timeProvider.GetUtcNow();
            return remaining > TimeSpan.Zero ? remaining : null;
        }
    }
}
