using System.Net;
using System.Net.Sockets;

namespace RandomSteamGame.Services;

internal static class LibraryExportRateLimitPartitionKey
{
    public static string From(IPAddress? ipAddress)
    {
        if (ipAddress is null)
        {
            return "unknown";
        }

        if (ipAddress.IsIPv4MappedToIPv6)
        {
            ipAddress = ipAddress.MapToIPv4();
        }

        if (ipAddress.AddressFamily == AddressFamily.InterNetwork)
        {
            return ipAddress.ToString();
        }

        if (ipAddress.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var bytes = ipAddress.GetAddressBytes();

            // Partition IPv6 clients by /64 so rotating interface IDs
            // does not bypass the export cooldown.
            Array.Clear(bytes, 8, 8);

            return $"{new IPAddress(bytes)}/64";
        }

        return ipAddress.ToString();
    }
}