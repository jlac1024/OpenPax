using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace OpenPax.Net.Lan;

/// <summary>
/// Looks for lobbies on every network this machine is on, LAN or VPN
/// (docs/netcode/lan-discovery.md): broadcasts the discovery question, asks
/// tunnel subnets address by address, Tailscale peers and named hosts too,
/// and collects the answers on a background thread. Ask again with
/// <see cref="Send"/> now and then; a lobby that stops answering drops off.
/// </summary>
public sealed class LanScan : IDisposable
{
    /// <summary>Sweeps, peers and names are asked on every this many asks (broadcasts every time), so a VPN is not flooded.</summary>
    private const int WideEvery = 5;

    private readonly UdpClient _udp;
    private readonly int _port;
    private readonly IPEndPoint[] _also;
    private readonly string[] _hosts;
    private readonly ConcurrentDictionary<string, (LanLobby Lobby, long Heard)> _heard = new();
    private long _asked;
    private int _sending;

    /// <param name="port">The lobby port the question goes to.</param>
    /// <param name="hosts">Host names or addresses asked directly (a VPN peer, config.json's lanHosts).</param>
    /// <param name="also">Places to ask besides the others (a test asks its own machine on its own port).</param>
    public LanScan(int port, IEnumerable<string> hosts, params IPEndPoint[] also)
    {
        _port = port;
        _also = also;
        _hosts = [.. hosts];
        _udp = new UdpClient(new IPEndPoint(IPAddress.Any, 0)) { EnableBroadcast = true };
        new Thread(HearLoop) { IsBackground = true, Name = "OpenPax LAN scan" }.Start();
    }

    /// <summary>The lobbies heard since the last wide ask before this one, by name.</summary>
    public IReadOnlyList<LanLobby> Found =>
        [.. _heard.Values.Where(h => h.Heard > Interlocked.Read(ref _asked) - (2 * WideEvery)).Select(h => h.Lobby).OrderBy(l => l.Name, StringComparer.OrdinalIgnoreCase).ThenBy(l => l.Address, StringComparer.Ordinal)];

    /// <summary>Asks again, on a worker thread (finding peers and names can take a moment); an ask still under way is not doubled.</summary>
    public void Send()
    {
        var ask = Interlocked.Increment(ref _asked);
        if (Interlocked.Exchange(ref _sending, 1) == 1)
        {
            return;
        }

        ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                SendNow(wide: ask % WideEvery == 1);
            }
            finally
            {
                Interlocked.Exchange(ref _sending, 0);
            }
        });
    }

    public void Dispose() => _udp.Dispose();

    private void SendNow(bool wide)
    {
        var question = LanDiscovery.Question.ToArray();
        var targets = LanTargets.Broadcasts().Select(a => new IPEndPoint(a, _port)).Concat(_also);
        if (wide)
        {
            targets = targets.Concat(LanTargets.Sweep().Concat(TailscalePeers.Find()).Concat(LanTargets.Resolve(_hosts)).Distinct().Select(a => new IPEndPoint(a, _port)));
        }

        foreach (var target in targets)
        {
            try
            {
                _udp.Send(question, question.Length, target);
            }
            catch (Exception e) when (e is SocketException or ObjectDisposedException)
            {
                // A network that will not take it; the others still might. Closed mid-ask ends it next time round.
                if (e is ObjectDisposedException)
                {
                    return;
                }
            }
        }
    }

    /// <summary>
    /// Windows reports a datagram that found nobody listening as an error on
    /// the next receive; that says nothing about this socket, so it does not
    /// end the loop.
    /// </summary>
    internal static bool Unreachable(SocketException e) =>
        e.SocketErrorCode is SocketError.ConnectionReset or SocketError.ConnectionRefused or SocketError.HostUnreachable or SocketError.NetworkUnreachable;

    private void HearLoop()
    {
        while (true)
        {
            try
            {
                var from = new IPEndPoint(IPAddress.Any, 0);
                var datagram = _udp.Receive(ref from);
                if (LanDiscovery.Read(datagram, from.Address.ToString()) is { } lobby)
                {
                    _heard[lobby.Address] = (lobby, Interlocked.Read(ref _asked));
                }
            }
            catch (SocketException e) when (Unreachable(e))
            {
            }
            catch (Exception e) when (e is SocketException or ObjectDisposedException)
            {
                return; // Closed.
            }
        }
    }
}
