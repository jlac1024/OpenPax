using OpenPax.Sim;
using OpenPax.Sim.Commands;
using static OpenPax.Ui.Tests.UiGames;

namespace OpenPax.Ui.Tests;

public class CommandRoutingTests
{
    [Fact]
    public void InANetworkGameTheScreensSendCommandsAndChangeNothingThemselves()
    {
        var app = InGame();
        var session = app.Session!;
        var sent = new List<Command>();
        session.Link = new RecordingLink(sent);
        var hash = session.Game.Hash();

        app.Command(UiCommand.Console);
        app.Typed("cheats on", 0);
        app.Key(UiKey.Return, false, 0);
        app.Typed("money", 0);
        app.Key(UiKey.Return, false, 0);

        Assert.Equal(new CheatCommand(session.Local.Index, Cheat.GiveLotsOfMoney), Assert.Single(sent));
        Assert.Equal(hash, session.Game.Hash());
    }
}
