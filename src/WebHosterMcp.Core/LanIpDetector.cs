using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace WebHosterMcp.Core;

/// <summary>
/// LAN-IP detection: finds the first non-loopback IPv4 address
/// on an active network interface. Used for `result_path` so the AI
/// can build LAN-reachable URLs (instead of just 127.0.0.1).
/// </summary>
public static class LanIpDetector
{
    /// <summary>
    /// First non-loopback, non-link-local IPv4 address.
    /// Null if no suitable address is found (fallback: <c>Host:Ip</c>).
    /// </summary>
    public static string? GetLanIpv4()
    {
        try
        {
            return NetworkInterface
                .GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up)
                .Where(n => n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .SelectMany(n => n.GetIPProperties().UnicastAddresses)
                .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
                .Select(a => a.Address.ToString())
                .FirstOrDefault(ip => !ip.StartsWith("127.") && !ip.StartsWith("169.254."));
        }
        catch
        {
            // Network access can fail (sandbox, missing permissions).
            // Caller falls back to Host:Ip.
            return null;
        }
    }
}
