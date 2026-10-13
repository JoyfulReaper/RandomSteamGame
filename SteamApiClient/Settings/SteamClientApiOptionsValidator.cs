using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace SteamApiClient.Settings;

internal sealed class SteamClientApiOptionsValidator(IConfigurationSection section)
    : IValidateOptions<SteamClientApiOptions>
{
    // Generous operational bounds: one year of caching, one day per admission window.
    private const int MaxCacheMinutes = 525600;
    private const int MaxPermitLimit = 1000000;
    private const int MaxWindowSeconds = 86400;

    public ValidateOptionsResult Validate(string? name, SteamClientApiOptions options)
    {
        var failures = new List<string>();
        if (string.IsNullOrWhiteSpace(options.ApiKey) || options.ApiKey == "STEAM_API_KEY" || options.ApiKey.Length < 32)
            failures.Add("Steam:ApiKey must be a nonblank, non-placeholder key of at least 32 characters.");
        if (string.IsNullOrWhiteSpace(options.ConnectionString))
            failures.Add("Steam:ConnectionString must not be blank.");

        if (!section.GetSection("Cache").Exists() || options.Cache is null)
        {
            failures.Add("Steam:Cache is required.");
        }
        else
        {
            ValidatePolicy("OwnedGames", options.Cache.OwnedGames);
            ValidatePolicy("AppDetails", options.Cache.AppDetails);
            ValidatePolicy("AppDetailsNotFound", options.Cache.AppDetailsNotFound);
            ValidatePolicy("VanitySuccess", options.Cache.VanitySuccess);
            ValidatePolicy("VanityNotFound", options.Cache.VanityNotFound);
            ValidatePolicy("SteamDeckCompatibility", options.Cache.SteamDeckCompatibility);
        }

        if (!section.GetSection("RateLimiting").Exists() || options.RateLimiting is null)
        {
            failures.Add("Steam:RateLimiting is required.");
        }
        else
        {
            ValidateRange("Steam:RateLimiting:PermitLimit", options.RateLimiting.PermitLimit, MaxPermitLimit);
            ValidateRange("Steam:RateLimiting:WindowSeconds", options.RateLimiting.WindowSeconds, MaxWindowSeconds);
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);

        void ValidatePolicy(string policyName, CachePolicy? policy)
        {
            var path = $"Steam:Cache:{policyName}";
            if (!section.GetSection($"Cache:{policyName}").Exists() || policy is null)
                failures.Add($"{path} is required.");
            else
                ValidateRange($"{path}:AbsoluteMinutes", policy.AbsoluteMinutes, MaxCacheMinutes);
        }

        void ValidateRange(string path, int value, int maximum)
        {
            if (string.IsNullOrWhiteSpace(section[path["Steam:".Length..]]))
                failures.Add($"{path} is required.");
            else if (value < 1 || value > maximum)
                failures.Add($"{path} must be between 1 and {maximum} (inclusive).");
        }
    }
}
