using System.Net;
using System.Net.Sockets;

namespace OrderBuddy.ScreenManager.Core.Diagnostics;

/// <summary>Classifies hosts/addresses as local-network (LAN, loopback, link-local) or public.</summary>
public static class NetworkAddressClassifier
{
    public static bool IsLocalNetworkHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        var trimmed = host.Trim().Trim('[', ']');
        if (trimmed.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || trimmed.EndsWith(".local", StringComparison.OrdinalIgnoreCase)
            || trimmed.EndsWith(".lan", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IPAddress.TryParse(trimmed, out var address) && IsPrivateAddress(address);
    }

    public static bool IsPrivateAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address))
        {
            return true;
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = address.GetAddressBytes();
            return b[0] == 10
                || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
                || (b[0] == 192 && b[1] == 168)
                || (b[0] == 169 && b[1] == 254);
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return address.IsIPv6LinkLocal
                || address.IsIPv6SiteLocal
                || (address.GetAddressBytes()[0] & 0xFE) == 0xFC;
        }

        return false;
    }
}
