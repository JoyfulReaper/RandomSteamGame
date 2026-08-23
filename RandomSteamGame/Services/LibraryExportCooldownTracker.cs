using Microsoft.Extensions.Caching.Memory;
using RandomSteamGame.Services.Interfaces;
using RandomSteamGame.Shared.Interfaces;

namespace RandomSteamGame.Services;

public sealed class LibraryExportCooldownTracker(
    IMemoryCache cache,
    IDateTimeProvider dateTimeProvider)
    : ILibraryExportCooldownTracker
{
    private static readonly TimeSpan Cooldown = TimeSpan.FromHours(72);

    public TimeSpan? GetRetryAfter(string partitionKey)
    {
        var cacheKey = GetCacheKey(partitionKey);
        if (!cache.TryGetValue<DateTimeOffset>(
                cacheKey,
                out var nextAvailableAt))
        {
            return null;
        }

        var now = new DateTimeOffset(dateTimeProvider.UtcNow);
        var retryAfter = nextAvailableAt - now;

        if (retryAfter <= TimeSpan.Zero)
        {
            cache.Remove(cacheKey);
            return null;
        }

        return retryAfter;
    }

    public void MarkSucceeded(string partitionKey)
    {
        var now = new DateTimeOffset(dateTimeProvider.UtcNow);
        var nextAvailableAt = now.Add(Cooldown);

        cache.Set(
            GetCacheKey(partitionKey),
            nextAvailableAt,
            Cooldown);
    }

    private static string GetCacheKey(string partitionKey)
        => $"library_export_cooldown_{partitionKey}";
}