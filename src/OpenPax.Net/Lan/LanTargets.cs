using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace OpenPax.Net.Lan;

/// <summary>
/// Where the discovery question goes (docs/netcode/lan-discovery.md): the
/// broadcast address of every IPv4 network this machine is on, real LAN or
/// VPN; on VPNs, which mostly carry no broadcasts, each address of a small
/// subnet one by one; and named hosts.
/// </summary>
internal static class LanTargets
{
    /// <summary>The largest subnet asked address by address: a /22, 1022 hosts.</summary>
    private const int SweepPrefix = 22;

    /// <summary>The broadcast address of each up, non-loopback IPv4 network, and 255.255.255.255.</summary>
    public static List<IPAddress> Broadcasts()
    {
        var found = new List<IPAddress> { IPAddress.Broadcast };
        foreach (var (address, prefix, _) in Networks())
        {
            if (prefix <= 30 && Broadcast(address, prefix) is var b && !found.Contains(b))
            {
                found.Add(b);
            }
        }

        return found;
    }

    /// <summary>
    /// Every host address of each tunnel network (WireGuard, ZeroTier, OpenVPN
    /// and the like) of a /22 or smaller, but not this machine's own. A plain
    /// LAN is left to its broadcast; a tunnel of one address (Tailscale's /32)
    /// has nothing to sweep and is asked through its peer list instead.
    /// </summary>
    public static List<IPAddress> Sweep()
    {
        var found = new List<IPAddress>();
        foreach (var (address, prefix, tunnel) in Networks())
        {
            if (!tunnel || prefix is < SweepPrefix or > 30)
            {
                continue;
            }

            var own = ToUInt(address);
            var mask = prefix == 0 ? 0 : uint.MaxValue << (32 - prefix);
            var network = own & mask;
            var last = network | ~mask;
            for (var host = network + 1; host < last; host++)
            {
                if (host != own)
                {
                    found.Add(FromUInt(host));
                }
            }
        }

        return found;
    }

    /// <summary>Host names or addresses resolved to IPv4 addresses; a name that does not resolve is skipped.</summary>
    public static List<IPAddress> Resolve(IEnumerable<string> hosts)
    {
        var found = new List<IPAddress>();
        foreach (var host in hosts)
        {
            try
            {
                found.AddRange(IPAddress.TryParse(host, out var ip) ? [ip] : Dns.GetHostAddresses(host, AddressFamily.InterNetwork));
            }
            catch (SocketException)
            {
                // Not known (yet): asked again next time.
            }
        }

        return found;
    }

    /// <summary>This machine's IPv4 networks: an address, its prefix length, and whether it is a tunnel rather than a LAN.</summary>
    private static List<(IPAddress Address, int Prefix, bool Tunnel)> Networks()
    {
        var found = new List<(IPAddress, int, bool)>();
        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus is not (OperationalStatus.Up or OperationalStatus.Unknown) || nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                {
                    continue;
                }

                var tunnel = IsTunnel(nic);
                foreach (var u in nic.GetIPProperties().UnicastAddresses)
                {
                    if (u.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(u.Address))
                    {
                        found.Add((u.Address, u.PrefixLength, tunnel));
                    }
                }
            }
        }
        catch (NetworkInformationException)
        {
            // No list of networks: the plain broadcast is still sent.
        }

        return found;
    }

    /// <summary>A VPN's virtual interface: no hardware address, or a tunnel or PPP type (wg0, zt*, tun0, utun on macOS, Wintun on Windows).</summary>
    private static bool IsTunnel(NetworkInterface nic) =>
        nic.NetworkInterfaceType is NetworkInterfaceType.Tunnel or NetworkInterfaceType.Ppp or NetworkInterfaceType.Unknown
        || nic.GetPhysicalAddress().GetAddressBytes().Length == 0;

    private static IPAddress Broadcast(IPAddress address, int prefix)
    {
        var mask = prefix == 0 ? 0 : uint.MaxValue << (32 - prefix);
        return FromUInt(ToUInt(address) | ~mask);
    }

    private static uint ToUInt(IPAddress a)
    {
        var b = a.GetAddressBytes();
        return ((uint)b[0] << 24) | ((uint)b[1] << 16) | ((uint)b[2] << 8) | b[3];
    }

    private static IPAddress FromUInt(uint v) => new([(byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v]);
}
