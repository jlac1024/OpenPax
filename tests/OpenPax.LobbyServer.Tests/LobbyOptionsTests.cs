using OpenPax.Net;

namespace OpenPax.LobbyServer.Tests;

public class LobbyOptionsTests
{
    [Fact]
    public void ThePortIsTheDefaultOrTheOneGiven()
    {
        Assert.Equal(NetProtocol.DefaultLobbyPort, LobbyOptions.Parse([]).Options?.Port);
        Assert.Equal(5000, LobbyOptions.Parse(["--port", "5000"]).Options?.Port);
    }

    [Fact]
    public void AWrongLineSaysWhy()
    {
        Assert.NotNull(LobbyOptions.Parse(["--port", "0"]).Error);
        Assert.NotNull(LobbyOptions.Parse(["--port"]).Error);
        Assert.NotNull(LobbyOptions.Parse(["--bogus"]).Error);
    }
}
