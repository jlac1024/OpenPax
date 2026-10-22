using OpenPax.Data.Install;
using OpenPax.Data.Tables;
using OpenPax.Host.Headless;
using OpenPax.Sim;
using OpenPax.Sim.Setup;
using OpenPax.TestSupport;

namespace OpenPax.Modding.Tests;

/// <summary>The getting started guide's mod (samples/Bounty), installed into a mods folder and loaded as the game loads it.</summary>
public sealed class BountyTests : IDisposable
{
    private readonly TempFolder _mods = new();

    public void Dispose() => _mods.Dispose();

    private Game NewGame()
    {
        var built = AppContext.BaseDirectory.Replace("OpenPax.Modding.Tests", "Bounty", StringComparison.Ordinal);
        var folder = Path.Combine(_mods.Path, "bounty");
        Directory.CreateDirectory(folder);
        foreach (var name in new[] { "Bounty.dll", "mod.toml", "settings.toml" })
        {
            File.Copy(Path.Combine(built, name), Path.Combine(folder, name), true);
        }

        var host = ModHost.Load(_mods.Path, ["bounty"]);
        Assert.Empty(host.Errors);
        var data = GameData.RequireFolder();
        var tables = GameTablesLoader.Load(
            Path.Combine(data, "Data"),
            DirectionTables.FromExecutable(File.ReadAllBytes(Path.Combine(data, GameInstall.Executable))),
            SpriteRows.Measure(OpenPax.Data.Resources.ResourceFile.Load(Path.Combine(data, "Image", "Combat.img"))));
        var game = OpenPax.Sim.Setup.NewGame.Create(new GameSetup(1234, 30, 2, 3, [new HumanPlayerSetup("Commander", 0, 0, tables.Species[0])]), tables);
        host.NewGame(game);
        return game;
    }

    [Fact]
    public void AKillPaysTheKillerAndIsCounted()
    {
        var game = NewGame();
        var (human, computer) = (game.Players[0], game.Players[1]);
        var ship = computer.Fleets.SelectMany(f => f.Ships).First();
        var money = human.Money;

        game.DamageShip(ship, ship.Structure, human);

        var paid = human.Money - money;
        Assert.True(paid == 50 * (ship.Type + 1) || paid == 100 * (ship.Type + 1), $"paid {paid}");
        Assert.Equal("1", game.ModState["bounty.kills.0"]);
        Assert.Contains(game.TakeNotices(human), n => n.Kind == NoticeKind.ModMessage && n.Text!.StartsWith("Bounty:", StringComparison.Ordinal));
    }

    [Fact]
    public void ALossToNobodyPaysNothing()
    {
        var game = NewGame();
        var human = game.Players[0];
        var ship = game.Players[1].Fleets.SelectMany(f => f.Ships).First();
        var money = human.Money;

        game.DamageShip(ship, ship.Structure);

        Assert.Equal(money, human.Money);
        Assert.False(game.ModState.ContainsKey("bounty.kills.0"));
    }
}
