using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace WebHosterMcp.Core;

/// <summary>
/// LAN-IP-Detection: findet die erste nicht-loopback IPv4-Adresse
/// auf einem aktiven Netzwerk-Interface. Wird für `result_path` verwendet,
/// damit KI LAN-reichbare URLs bauen kann (statt nur 127.0.0.1).
/// </summary>
public static class LanIpDetector
{
    /// <summary>
    /// Erste nicht-loopback, nicht-link-local IPv4-Adresse.
    /// Null wenn keine geeignete Adresse gefunden wird (Fallback: <c>Host:Ip</c>).
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
            // Network-Access kann fehlen (Sandbox, fehlende Permissions).
            // Caller fällt auf Host:Ip zurück.
            return null;
        }
    }
}
