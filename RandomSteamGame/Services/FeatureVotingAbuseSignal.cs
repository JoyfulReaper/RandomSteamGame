using System.Security.Cryptography;
using System.Text;

namespace RandomSteamGame.Services;

// Process-lifetime secret and transient limiter keys only. No voting IP data is persisted
// or sent to telemetry. Normalization also groups rotating IPv6 interfaces by /64.
public sealed class FeatureVotingAbuseSignal
{
    private readonly byte[] _key = RandomNumberGenerator.GetBytes(32);

    public string From(HttpContext context) => Convert.ToHexStringLower(HMACSHA256.HashData(_key,
        Encoding.UTF8.GetBytes("RandomSteamGame.FeatureVoting.IP.v1:" +
            LibraryExportRateLimitPartitionKey.From(context.Connection.RemoteIpAddress))));
}
