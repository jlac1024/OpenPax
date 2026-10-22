using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Text.Json;

namespace OpenPax.Net.Lan;

/// <summary>
/// The IPv4 addresses of this machine's Tailscale peers, from
/// <c>tailscale status --json</c>. Tailscale carries no broadcasts, so a
/// lobby on the tailnet is asked directly. No Tailscale, no peers.
/// </summary>
public static class TailscalePeers
{
    private const int Wait = 3000;

    /// <summary>Where the command line tool may be when it is not on the PATH.</summary>
    private static readonly string[] _tools =
    [
        "tailscale",
        "/Applications/Tailscale.app/Contents/MacOS/Tailscale",
        @"C:\Program Files\Tailscale\tailscale.exe",
    ];

    public static List<IPAddress> Find()
    {
        foreach (var tool in _tools)
        {
            if (Status(tool) is { } json)
            {
                return Parse(json);
            }
        }

        return [];
    }

    /// <summary>Every peer's IPv4 Tailscale address in a status reply; online or not, since the flag lags.</summary>
    public static List<IPAddress> Parse(string json)
    {
        var found = new List<IPAddress>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("Peer", out var peers) || peers.ValueKind != JsonValueKind.Object)
            {
                return found;
            }

            foreach (var peer in peers.EnumerateObject())
            {
                if (!peer.Value.TryGetProperty("TailscaleIPs", out var ips) || ips.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var ip in ips.EnumerateArray())
                {
                    if (IPAddress.TryParse(ip.GetString(), out var a) && a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        found.Add(a);
                    }
                }
            }
        }
        catch (JsonException)
        {
            // Not what the tool usually says: no peers.
        }

        return found;
    }

    private static string? Status(string tool)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo(tool, ["status", "--json"])
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            if (p is null)
            {
                return null;
            }

            var output = p.StandardOutput.ReadToEndAsync();
            _ = p.StandardError.ReadToEndAsync();
            if (!p.WaitForExit(Wait))
            {
                p.Kill();
                return null;
            }

            return p.ExitCode == 0 && output.Wait(Wait) ? output.Result : null;
        }
        catch (Exception e) when (e is Win32Exception or InvalidOperationException or IOException)
        {
            return null;
        }
    }
}
