using System.Net;
using System.Net.Sockets;

namespace OpenPax.Net.Lan;

/// <summary>
/// A lobby's answer to LAN discovery: listens for the question on a UDP
/// port and answers whoever asked. If the port is taken the lobby simply
/// cannot be found this way; it still takes players by address.
/// </summary>
public sealed class LanBeacon : IDisposable
{
    private readonly UdpClient? _udp;
    private readonly Func<LanLobby> _describe;

    /// <param name="describe">What to answer with, asked on the beacon's own thread each time.</param>
    public LanBeacon(int port, Func<LanLobby> describe)
    {
        _describe = describe;
        try
        {
            _udp = new UdpClient(new IPEndPoint(IPAddress.Any, port));
        }
        catch (SocketException)
        {
            return;
        }

        new Thread(AnswerLoop) { IsBackground = true, Name = "OpenPax lobby beacon" }.Start();
    }

    /// <summary>The beacon could take its port.</summary>
    public bool Listening => _udp is not null;

    public void Dispose() => _udp?.Dispose();

    private void AnswerLoop()
    {
        var udp = _udp!; // Not null: the loop starts only once the socket is made.
        while (true)
        {
            try
            {
                var from = new IPEndPoint(IPAddress.Any, 0);
                var question = udp.Receive(ref from);
                if (question.AsSpan().SequenceEqual(LanDiscovery.Question))
                {
                    var answer = LanDiscovery.Answer(_describe());
                    udp.Send(answer, answer.Length, from);
                }
            }
            catch (SocketException e) when (LanScan.Unreachable(e))
            {
                // An asker gone before its answer arrived (reported so on Windows).
            }
            catch (Exception e) when (e is SocketException or ObjectDisposedException)
            {
                return; // Closed.
            }
        }
    }
}
