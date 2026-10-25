using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using RandomSteamGame.Options;

namespace RandomSteamGame.Services;

internal static class FeatureVotingRateLimits
{
    public const string PolicyName = "feature_voting_limiter";

    internal static bool AppliesTo(HttpContext context) => context.GetEndpoint()?.Metadata
        .GetMetadata<EnableRateLimitingAttribute>()?.PolicyName == PolicyName;

    public static IServiceCollection AddFeatureVotingRateLimits(this IServiceCollection services)
    {
        services.AddOptions<FeatureVotingOptions>()
            .BindConfiguration(FeatureVotingOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddSingleton<FeatureVotingIdentity>();
        services.AddSingleton<FeatureVotingAbuseSignal>();
        services.AddOptions<RateLimiterOptions>()
            .Configure<IOptions<FeatureVotingOptions>, FeatureVotingIdentity, FeatureVotingAbuseSignal>(
                (options, configured, identity, signal) =>
                {
                    var limits = configured.Value;
                    options.GlobalLimiter = PartitionedRateLimiter.CreateChained(
                        options.GlobalLimiter!,
                        PartitionedRateLimiter.Create<HttpContext, string>(context => AppliesTo(context)
                            ? RateLimitPartition.GetConcurrencyLimiter("feature-capacity", _ => new ConcurrencyLimiterOptions
                            {
                                PermitLimit = limits.GlobalConcurrency, QueueLimit = 0
                            })
                            : RateLimitPartition.GetNoLimiter("other")),
                        PartitionedRateLimiter.Create<HttpContext, string>(context => AppliesTo(context)
                            ? Window("feature-global", limits.GlobalPermitLimit, limits)
                            : RateLimitPartition.GetNoLimiter("other")),
                        PartitionedRateLimiter.Create<HttpContext, string>(context => AppliesTo(context)
                            ? Window(signal.From(context), limits.IpPermitLimit, limits)
                            : RateLimitPartition.GetNoLimiter("other")));

                    options.AddPolicy(PolicyName, context => HttpMethods.IsPost(context.Request.Method)
                        ? Window(identity.ReadHash(context) ?? "missing", limits.BrowserPermitLimit, limits)
                        : RateLimitPartition.GetNoLimiter("feature-read"));

                    var previousRejection = options.OnRejected;
                    options.OnRejected = async (context, ct) =>
                    {
                        if (!AppliesTo(context.HttpContext))
                        {
                            if (previousRejection is not null)
                                await previousRejection(context, ct);
                            return;
                        }
                        var seconds = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retry)
                            ? Math.Max(1, (int)Math.Ceiling(retry.TotalSeconds)) : 1;
                        context.HttpContext.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
                        await Results.Problem(statusCode: 429,
                            detail: "Feature voting is busy. Please slow down and try again.")
                            .ExecuteAsync(context.HttpContext);
                    };
                });
        return services;
    }

    private static RateLimitPartition<string> Window(string key, int permits, FeatureVotingOptions limits) =>
        RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permits, Window = TimeSpan.FromSeconds(limits.WindowSeconds),
            QueueLimit = 0, AutoReplenishment = true
        });
}
