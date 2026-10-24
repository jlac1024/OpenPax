using OpenPax.Data.Install;
using OpenPax.Data.Tables;
using OpenPax.Host.Headless;
using OpenPax.Sim;
using OpenPax.Sim.Setup;
using OpenPax.TestSupport;

namespace OpenPax.Modding.Tests;

/// <summary>Lua mods (docs/modding/lua.md): samples/BountyLua as the game loads it, the hooks, errors and saves across mod changes.</summary>
public sealed class LuaModTests : IDisposable
{
    private readonly TempFolder _mods = new();

    public void Dispose() => _mods.Dispose();

    private void InstallBounty()
    {
        var sample = Path.Combine(GameData.RepositoryRoot(), "samples", "BountyLua");
        var folder = Path.Combine(_mods.Path, "bounty-lua");
        Directory.CreateDirectory(folder);
        foreach (var file in Directory.GetFiles(sample))
        {
            File.Copy(file, Path.Combine(folder, Path.GetFileName(file)), true);
        }
    }

    private void InstallScript(string id, string script)
    {
        var folder = Path.Combine(_mods.Path, id);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "mod.toml"), $"id = \"{id}\"\nversion = \"1.0\"\nscript = \"main.lua\"\n");
        File.WriteAllText(Path.Combine(folder, "main.lua"), script);
    }

    private static Game NewGame(ModHost host, uint seed = 1234)
    {
        var data = GameData.RequireFolder();
        var tables = GameTablesLoader.Load(
            Path.Combine(data, "Data"),
            DirectionTables.FromExecutable(File.ReadAllBytes(Path.Combine(data, GameInstall.Executable))),
            SpriteRows.Measure(OpenPax.Data.Resources.ResourceFile.Load(Path.Combine(data, "Image", "Combat.img"))));
        var game = OpenPax.Sim.Setup.NewGame.Create(new GameSetup(seed, 30, 2, 3, [new HumanPlayerSetup("Commander", 0, 0, tables.Species[0])]), tables);
        host.NewGame(game);
        return game;
    }

    [Fact]
    public void BountyInLuaPaysTheKillerAndCountsTheKill()
    {
        InstallBounty();
        var host = ModHost.Load(_mods.Path, ["bounty-lua"]);
        Assert.Empty(host.Errors);
        var game = NewGame(host);
        var (human, computer) = (game.Players[0], game.Players[1]);
        var ship = computer.Fleets.SelectMany(f => f.Ships).First();
        var money = human.Money;

        game.DamageShip(ship, ship.Structure, human);

        var paid = human.Money - money;
        Assert.True(paid == 50 * (ship.Type + 1) || paid == 100 * (ship.Type + 1), $"paid {paid}");
        Assert.Equal("1", game.ModState["bounty-lua.kills.0"]);
        Assert.Contains(game.TakeNotices(human), n => n.Kind == NoticeKind.ModMessage && n.Text!.StartsWith("Bounty:", StringComparison.Ordinal));
    }

    [Fact]
    public void BountyInLuaPaysNothingForALossToNobody()
    {
        InstallBounty();
        var game = NewGame(ModHost.Load(_mods.Path, ["bounty-lua"]));
        var human = game.Players[0];
        var ship = game.Players[1].Fleets.SelectMany(f => f.Ships).First();
        var money = human.Money;

        game.DamageShip(ship, ship.Structure);

        Assert.Equal(money, human.Money);
        Assert.False(game.ModState.ContainsKey("bounty-lua.kills.0"));
    }

    [Fact]
    public void TheSameGameComesOutTheSameEveryTime()
    {
        // math.random draws from the mod's saved stream, so two runs of a seed agree to the credit.
        InstallBounty();
        double Run()
        {
            var game = NewGame(ModHost.Load(_mods.Path, ["bounty-lua"]), seed: 77);
            var human = game.Players[0];
            foreach (var ship in game.Players[1].Fleets.SelectMany(f => f.Ships).ToList())
            {
                game.DamageShip(ship, ship.Structure, human);
            }

            return human.Money;
        }

        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void ScriptsWorkWithTicksAndTheirOwnFiles()
    {
        InstallScript("ticker", """
            local names = mod.read_text("names.txt")
            local count = 0
            mod.every(2, function(game)
              count = count + 1
              game.store:set("seen", game.tick)
              game.store:set("first_name", names:match("^(%a+)"))
            end)
            """);
        File.WriteAllText(Path.Combine(_mods.Path, "ticker", "names.txt"), "Vega Altair");
        var host = ModHost.Load(_mods.Path, ["ticker"]);
        Assert.Empty(host.Errors);
        var game = NewGame(host);
        for (var i = 0; i < 5; i++)
        {
            game.Step();
        }

        Assert.Equal("4", game.ModState["ticker.seen"]);
        Assert.Equal("Vega", game.ModState["ticker.first_name"]);
    }

    [Fact]
    public void AScriptCannotReadOutsideItsFolder()
    {
        InstallScript("snoop", "mod.read_text('../secret.txt')");
        var host = ModHost.Load(_mods.Path, ["snoop"]);
        Assert.Contains(host.Errors, e => e.Contains("outside the mod's folder", StringComparison.Ordinal));
    }

    [Fact]
    public void AnErrorInAHookIsToldOnceAndTheGameGoesOn()
    {
        InstallScript("broken", "mod.on_step(function(game) local t = nil return t.x end)");
        var game = NewGame(ModHost.Load(_mods.Path, ["broken"]));
        var human = game.Players[0];
        game.Step();
        game.Step();

        var told = game.TakeNotices(human).Where(n => n.Kind == NoticeKind.ModMessage).ToList();
        var notice = Assert.Single(told);
        Assert.Contains("main.lua:1: attempt to index a nil value", notice.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void AnEndlessLoopInAHookIsStopped()
    {
        InstallScript("spin", "mod.on_step(function(game) while true do end end)");
        var game = NewGame(ModHost.Load(_mods.Path, ["spin"]));
        game.Step();
        Assert.Contains(game.TakeNotices(game.Players[0]), n => n.Text?.Contains("ran too long", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void ALoadedSaveTellsWhatChangedAndKeepsAMissingModsState()
    {
        InstallScript("keeper", """
            mod.on_load(function(game, saved)
              game.store:set("upgraded_from", saved or "none")
            end)
            """);
        var host = ModHost.Load(_mods.Path, ["keeper"]);
        var game = NewGame(host);

        // As saved: an older keeper, and a mod that is no longer turned on, with state of its own.
        game.Mods.Clear();
        game.Mods.AddRange(["keeper@0.9", "gone@2.0"]);
        game.ModState["gone.level"] = "5";
        host.Resume(game);

        Assert.Equal("0.9", game.ModState["keeper.upgraded_from"]);
        Assert.Equal(["keeper@1.0", "gone@2.0"], game.Mods);
        Assert.Equal("5", game.ModState["gone.level"]);
        var notes = game.TakeNotices(game.Players[0]).Select(n => n.Text ?? "").ToList();
        Assert.Contains(notes, n => n.Contains("was version 0.9", StringComparison.Ordinal));
        Assert.Contains(notes, n => n.Contains("gone@2.0", StringComparison.Ordinal) && n.Contains("not turned on", StringComparison.Ordinal));
    }

    [Fact]
    public void AModNewToASaveJoinsIt()
    {
        InstallScript("late", "mod.on_load(function(game, saved) game.store:set('joined', saved == nil) end)");
        var host = ModHost.Load(_mods.Path, ["late"]);
        var game = NewGame(host);
        game.Mods.Clear();
        host.Resume(game);
        Assert.Equal("true", game.ModState["late.joined"]);
        Assert.Contains(game.TakeNotices(game.Players[0]), n => n.Text?.Contains("joins it now", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void AManifestMayNotNameBothAnAssemblyAndAScript()
    {
        var folder = Path.Combine(_mods.Path, "both");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "mod.toml"), "id = \"both\"\nassembly = \"Both.dll\"\nscript = \"main.lua\"\n");
        var host = ModHost.Load(_mods.Path, ["both"]);
        Assert.NotEmpty(host.Errors);
    }
}
