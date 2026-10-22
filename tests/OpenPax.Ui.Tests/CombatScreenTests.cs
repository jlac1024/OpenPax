using OpenPax.Sim;
using OpenPax.Sim.Combat;
using OpenPax.Sim.Players;
using OpenPax.Ui.Battle;
using OpenPax.Ui.Screens;
using OpenPax.Ui.Widgets;
using static OpenPax.Ui.Tests.UiGames;

namespace OpenPax.Ui.Tests;

public class CombatScreenTests
{
    /// <summary>A game with the local fleet at war in a battle with a computer fleet, and the battle window open by F9.</summary>
    private static (UiApp App, GameSession Session, Sim.Combat.Battle Battle, BattleFieldPane Field) InBattle()
    {
        var app = InGame();
        var session = app.Session!;
        var game = session.Game;
        var human = session.Local;
        var computer = game.Players.First(p => p != human);
        var events = StepEvents.None;
        game.SetTreaty(human, computer, Treaty.War, Treaty.InForce, ref events);
        game.SetTreaty(computer, human, Treaty.War, Treaty.InForce, ref events);
        game.StartFleetBattle(human.Fleets.First(f => f.Ships.Count > 0), computer.Fleets.First(f => f.Ships.Count > 0));
        app.Command(UiCommand.Mode20);
        Assert.Equal(ScreenMode.Combat, app.Mode);
        var field = app.Current.Window.Get<BattleFieldPane>(24001);
        return (app, session, game.Battles[0], field);
    }

    private static void Press(UiApp app, int id)
    {
        var pane = app.Current.Window.Get(id);
        Assert.True(pane.Shown && pane.Enabled, $"{id} cannot be pressed");
        Click(app, pane.Left + 5, pane.Top + 5);
    }

    private static void ClickShip(UiApp app, BattleFieldPane field, BattleShip ship, bool right = false)
    {
        var (x, y) = field.ToScreen(ship.X, ship.Y);
        app.MouseDown(x, y, 1, 0, right);
        app.MouseUp(x, y);
        _ = app.Tick(0).ToList();
    }

    [Fact]
    public void F9ShowsTheBattleCenteredOnTheLocalSideAndDraws()
    {
        var (app, session, battle, field) = InBattle();
        Assert.Same(battle, field.Battle);
        var own = battle.Ships[session.Local.Index][0];
        var (x, y) = field.ToScreen(own.X, own.Y);
        Assert.InRange(x, field.Left, field.Left + field.Width);
        Assert.InRange(y, field.Top, field.Top + field.Height);
        Assert.False(app.Current.Window.Get(24003).Visible);

        _ = app.Tick(100).ToList();
        app.Draw(new OpenPax.Render.Framebuffer(640, 480));
        Press(app, 24151);
        Assert.True(app.Current.Window.Get(24003).Visible);
        Assert.Equal(BattleFieldPane.BarHeight, field.Height);
        for (var t = 0; t < 400; t += 40)
        {
            _ = app.Tick(200 + t).ToList();
        }

        app.Draw(new OpenPax.Render.Framebuffer(640, 480));
    }

    [Fact]
    public void ClickingAnOwnShipChoosesItAndShowsItsCommands()
    {
        var (app, session, battle, field) = InBattle();
        Press(app, 24151);
        var own = battle.Ships[session.Local.Index][0];
        ClickShip(app, field, own);
        Assert.Equal([own], field.Selection);
        Assert.True(app.Current.Window.Get(24100).Shown);
        Assert.False(app.Current.Window.Get(24103).Shown);
    }

    [Fact]
    public void MoveThenAClickSendsTheShipThere()
    {
        var (app, session, battle, field) = InBattle();
        Press(app, 24151);
        var own = battle.Ships[session.Local.Index][0];
        ClickShip(app, field, own);
        Press(app, 24100);

        // The buttons hide while the command waits; a frame later the pointer has left them and the prompt shows.
        _ = app.Tick(0).ToList();
        Assert.Equal(1, field.Mode);
        Assert.Equal(app.Assets.Strings.Get(new OpenPax.Data.Views.TextRef(25001, 0)), app.Current.Window.Get<WrappedTextPane>(24004).Text);
        Click(app, field.Left + 100, field.Top + 100);
        Assert.Equal(0, field.Mode);
        Assert.Equal(BattleShip.Moving, own.State);
    }

    [Fact]
    public void ARightClickOnAnEnemyAttacksIt()
    {
        var (app, session, battle, field) = InBattle();
        var own = battle.Ships[session.Local.Index][0];
        ClickShip(app, field, own);
        var enemy = Enumerable.Range(0, Sim.Combat.Battle.Players).Where(p => p != session.Local.Index).SelectMany(p => battle.Ships[p]).First();
        field.CenterOn(enemy.X, enemy.Y);
        ClickShip(app, field, enemy, right: true);
        Assert.Equal(BattleShip.Pursuing, own.State);
        Assert.Same(enemy, own.Target);
    }

    [Fact]
    public void KeysChooseCommandsAndGroups()
    {
        var (app, session, battle, field) = InBattle();
        var own = battle.Ships[session.Local.Index][0];
        ClickShip(app, field, own);
        app.Typed("!", 0);
        Assert.Equal(1, own.Ship.CombatGroup);
        app.Typed("0", 0);
        Assert.Equal(battle.Ships[session.Local.Index].Count, field.Selection.Count);
        app.Typed("1", 0);
        Assert.Equal([own], field.Selection);
        app.Typed("m", 0);
        Assert.Equal(1, field.Mode);
        app.Key(UiKey.Backspace, false, 0);
        Assert.Equal(0, field.Mode);
    }

    [Fact]
    public void AutoHandsTheSideToTheComputer()
    {
        var (app, session, battle, _) = InBattle();
        Press(app, 24150);
        Assert.True(battle.Commanders[session.Local.Index]!.Computer); // the local side has a commander
        Assert.True(battle.AutoEngaged);
        Assert.Equal((byte)18, app.Current.Window.Get<ButtonPane>(24150).Color);
    }

    [Fact]
    public void TheBattleRunsAndTheWindowLeavesWhenItEnds()
    {
        var (app, session, battle, _) = InBattle();
        var steps = battle.Steps;
        for (var t = 40; t <= 400; t += 40)
        {
            _ = app.Tick(t).ToList();
        }

        Assert.True(battle.Steps > steps);
        session.Game.Battles.Remove(battle);
        _ = app.Tick(1000).ToList();
        Assert.Equal(ScreenMode.SectorDisplay, app.Mode);
    }
}
