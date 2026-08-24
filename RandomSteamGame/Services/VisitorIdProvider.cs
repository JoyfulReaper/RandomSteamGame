using Microsoft.Extensions.Options;
using RandomSteamGame.Options;
using RandomSteamGame.Services.Interfaces;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace RandomSteamGame.Services;

public sealed class VisitorIdProvider : IVisitorIdProvider
{
    private readonly byte[]? _key;

    public VisitorIdProvider(
        IOptions<TelemetryOptions> options,
        ILogger<VisitorIdProvider> logger)
    {
        var key = options.Value.VisitorHashKey;

        if (string.IsNullOrWhiteSpace(key))
        {
            logger.LogWarning(
                "Telemetry VisitorHashKey is not configured. " +
                "Visitor IDs will be omitted from telemetry.");

            return;
        }

        _key = Encoding.UTF8.GetBytes(key);
    }

    public string? GetVisitorId(string ipAddress)
    {
        if (_key is null)
        {
            return null;
        }

        if (!IPAddress.TryParse(ipAddress, out var address))
        {
            throw new ArgumentException(
                "Value is not a valid IP address.",
                nameof(ipAddress));
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        var hash = HMACSHA256.HashData(_key, address.GetAddressBytes());

        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}