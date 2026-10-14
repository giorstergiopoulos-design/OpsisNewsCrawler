using System.Net;
using System.Net.Sockets;

namespace OpsisNewsCrawler.Core;

/// <summary>
/// Small safety checks for addresses that come from downloaded content (feed items, image links, site icons):
/// such content must not make the program talk to the user's own machine or local network.
/// </summary>
public static class NetGuard
{
    /// <summary>False for localhost, single-label names, .local/.internal names and private, loopback or link-local IP addresses.</summary>
    public static bool IsPublicHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host)) return false;
        host = host.Trim().TrimEnd('.').ToLowerInvariant();
        if (host.StartsWith('[') && host.EndsWith(']')) host = host[1..^1];
        if (host == "localhost" || host.EndsWith(".localhost") || host.EndsWith(".local") || host.EndsWith(".internal") || host.EndsWith(".lan")) return false;
        if (IPAddress.TryParse(host, out var ip)) return IsPublic(ip);
        return host.Contains('.');
    }

    public static bool IsPublicHttpUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" && IsPublicHost(uri.Host);

    private static bool IsPublic(IPAddress ip)
    {
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        if (IPAddress.IsLoopback(ip) || ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.IPv6Any)) return false;
        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
            return !(ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6Multicast || (ip.GetAddressBytes()[0] & 0xFE) == 0xFC);
        var b = ip.GetAddressBytes();
        return !(b[0] == 10 || b[0] == 127 || b[0] == 0 || (b[0] == 172 && b[1] is >= 16 and <= 31) || (b[0] == 192 && b[1] == 168) ||
                 (b[0] == 169 && b[1] == 254) || (b[0] == 100 && b[1] is >= 64 and <= 127) || b[0] >= 224);
    }
}
