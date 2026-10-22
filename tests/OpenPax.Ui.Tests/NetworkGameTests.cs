using OpenPax.Net;
using OpenPax.Net.Client;
using OpenPax.Net.Server;
using OpenPax.Sim.Setup;
using OpenPax.Sim.Time;
using OpenPax.Ui.Network;
using OpenPax.Ui.Screens;
using OpenPax.Ui.Widgets;
using static OpenPax.Ui.Tests.UiGames;

namespace OpenPax.Ui.Tests;

/// <summary>Two apps, a host and a guest, playing one game over loopback through their screens.</summary>
public class NetworkGameTests
{
    [Fact]
    public void AGuestsClickReachesTheServerAndComesBack()
    {
        var tables = Tables();
        var game = NewGame.Create(new GameSetup(31, 30, 4, 3, [new HumanPlayerSetup("Host", 0, 0, tables.Species[0]), new HumanPlayerSetup("Guest", 1, 1, tables.Species[1])]), tables);
        using var server = new GameServer(game, new GameClock(0), host: 0);
        var port = server.Listen(0);
        var host = NewApp();
        host.NetworkGameStarted(game, game.Players[0], new HostLink(server), 0);

        using var client = new GameClient(Connection.Connect("127.0.0.1", port), tables, "Guest");
        long now = 0;
        void Frame(UiApp? guestApp)
        {
            now += 50;
            _ = host.Tick(now).ToList();
            Thread.Sleep(2);
            if (guestApp is null)
            {
                client.Pump();
            }
            else
            {
                _ = guestApp.Tick(now).ToList();
            }
        }

        for (var i = 0; i < 2000 && client.Game is null; i++)
        {
            Frame(null);
        }

        Assert.NotNull(client.Game);
        var guest = NewApp();
        guest.NetworkGameStarted(client.Game, client.Game.Players[client.Player], new ClientLink(client), now);
        Assert.Equal(1, guest.Session!.Local.Index);

        guest.Command(UiCommand.DomesticPolicy);
        Assert.Equal(ScreenMode.DomesticPolicy, guest.Mode);
        var local = guest.Session.Local;
        var spinner = guest.Current.Window.Get<SpinnerPane>(17016);
        var before = local.Spending[1];
        Click(guest, spinner.Left + 5, spinner.Top + 6, now);

        // Nothing changes on the guest until the server's state comes back.
        Assert.Equal(before, local.Spending[1]);
        for (var i = 0; i < 2000 && local.Spending[1] == before; i++)
        {
            Frame(guest);
        }

        Assert.Equal(before - 1, game.Players[1].Spending[1]);
        Assert.Equal(before - 1, local.Spending[1]);
        Assert.Same(local, guest.Session.Local);
        Assert.Equal(0, client.Desyncs);

        // Chat: Return opens the line in a game screen, Return sends it, and both see it.
        bool Heard(UiApp app, string line) => app.Log.History.Concat(app.Log.Waiting).Any(n => n?.Text == line);
        guest.Command(UiCommand.Bridge);
        guest.Key(UiKey.Return, false, now);
        Assert.NotNull(guest.Forms.Top);
        guest.Typed("hello host", now);
        guest.Key(UiKey.Return, false, now);
        Assert.Null(guest.Forms.Top);
        for (var i = 0; i < 2000 && !(Heard(host, "Guest: hello host") && Heard(guest, "Guest: hello host")); i++)
        {
            Frame(guest);
        }

        Assert.True(Heard(host, "Guest: hello host"));
        Assert.True(Heard(guest, "Guest: hello host"));
        host.Key(UiKey.Return, false, now);
        host.Typed("hi", now);
        host.Key(UiKey.Return, false, now);
        for (var i = 0; i < 2000 && !Heard(guest, "Host: hi"); i++)
        {
            Frame(guest);
        }

        Assert.True(Heard(guest, "Host: hi"));

        // Escape closes the line without sending.
        guest.Key(UiKey.Return, false, now);
        guest.Key(UiKey.Escape, false, now);
        Assert.Null(guest.Forms.Top);

        // Nobody pauses a network game or changes its speed by key, as in the original.
        var speed = server.Clock.Speed;
        host.Command(UiCommand.Pause);
        host.Command(UiCommand.SpeedUp);
        guest.Command(UiCommand.SlowDown);
        Assert.False(server.Clock.Paused);
        Assert.Equal(speed, server.Clock.Speed);

        // Opening Options pauses nothing; a guest's speed control is shut, the host's open.
        guest.Command(UiCommand.GameOptions);
        host.Command(UiCommand.GameOptions);
        Assert.False(server.Clock.Paused);
        Assert.False(guest.Current.Window.Get(32056).Enabled);
        Assert.True(host.Current.Window.Get(32056).Enabled);

        // The host's new speed reaches the guest's clock, with the speed notice.
        host.Preferences.Set(Preferences.GameSpeed, speed == 6 ? 5 : 6);
        var wanted = server.Clock.Speed;
        for (var i = 0; i < 2000 && guest.Session.Clock.Speed != wanted; i++)
        {
            Frame(guest);
        }

        Assert.Equal(wanted, guest.Session.Clock.Speed);
    }
}
