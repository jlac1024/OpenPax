using System.Net;
using OpenPax.Net.Lan;
using OpenPax.Net.Lobby;

namespace OpenPax.Net.Tests;

/// <summary>LAN discovery: a lobby answers the question, by broadcast or asked by name, and Tailscale's peer list is read.</summary>
public sealed class LanScanTests : IDisposable
{
    private readonly LobbyServer _lobby = new() { Name = "test lobby" };
    private readonly int _port;

    public LanScanTests()
    {
        _port = _lobby.Listen(0);
    }

    public void Dispose() => _lobby.Dispose();

    private static LanLobby? Wait(LanScan scan, string address)
    {
        for (var i = 0; i < 300; i++)
        {
            if (scan.Found.FirstOrDefault(l => l.Address == address) is { } found)
            {
                return found;
            }

            Thread.Sleep(10);
        }

        return null;
    }

    [Fact]
    public void ALobbyAskedByNameAnswersWithItself()
    {
        Assert.True(_lobby.Discoverable);
        using var scan = new LanScan(_port, ["localhost"]);
        scan.Send();
        var found = Wait(scan, "127.0.0.1:" + _port);
        Assert.NotNull(found);
        Assert.Equal(("test lobby", NetProtocol.Version, 0), (found.Name, found.Version, found.Players));
    }

    [Fact]
    public void AnAnswerReadsBack()
    {
        var bytes = LanDiscovery.Answer(new LanLobby("lin-dev", "", 10421, 1, 3));
        Assert.Equal(new LanLobby("lin-dev", "100.72.6.50", 10421, 1, 3), LanDiscovery.Read(bytes, "100.72.6.50"));
        Assert.Null(LanDiscovery.Read("HGLOBBY?"u8, "x"));
    }

    [Fact]
    public void TailscalePeersAreTheirIpv4Addresses()
    {
        const string json = """
            {"Self":{"TailscaleIPs":["100.103.21.115"]},
             "Peer":{"a":{"TailscaleIPs":["100.72.6.50","fd7a:115c:a1e0::aa32:633"],"Online":true},
                     "b":{"TailscaleIPs":["100.81.169.65"],"Online":false},
                     "c":{}}}
            """;
        Assert.Equal([IPAddress.Parse("100.72.6.50"), IPAddress.Parse("100.81.169.65")], TailscalePeers.Parse(json));
        Assert.Empty(TailscalePeers.Parse("not json"));
    }
}
