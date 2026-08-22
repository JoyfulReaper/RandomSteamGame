using Microsoft.Extensions.Options;
using RandomSteamGame.Options;
using RandomSteamGame.Services.Interfaces;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace RandomSteamGame.Services;

public sealed class VisitorIdProvider : IVisitorIdProvider
{
    private readonly byte[] _key;

    public VisitorIdProvider(IOptions<TelemetryOptions> options)
    {
        var key = options.Value.VisitorHashKey;

        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException($"{TelemetryOptions.SectionName}:{nameof(TelemetryOptions.VisitorHashKey)} is required.");
        }

        _key = Encoding.UTF8.GetBytes(key);
    }

    public string GetVisitorId(string ipAddress)
    {
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