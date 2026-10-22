using OpenPax.Data.Install;
using OpenPax.Data.Tables;
using OpenPax.Host.Headless;
using OpenPax.Sim;
using OpenPax.Sim.Players;
using OpenPax.Sim.Setup;
using OpenPax.TestSupport;

namespace OpenPax.Modding.Tests;

/// <summary>The mod API beyond battles (docs/modding/api.md): events, actions, a mod's store and random numbers.</summary>
public sealed class ModApiTests
{
    private static GameTables? _tables;

    private static GameTables Tables()
    {
        var folder = GameData.RequireFolder();
        return _tables ??= GameTablesLoader.Load(
            Path.Combine(folder, "Data"),
            DirectionTables.FromExecutable(File.ReadAllBytes(Path.Combine(folder, GameInstall.Executable))),
            SpriteRows.Measure(OpenPax.Data.Resources.ResourceFile.Load(Path.Combine(folder, "Image", "Combat.img"))));
    }

    private static Game NewGame(uint seed = 1234) =>
        OpenPax.Sim.Setup.NewGame.Create(new GameSetup(seed, 30, 2, 3, [new HumanPlayerSetup("Commander", 0, 0, Tables().Species[0])]), Tables());

    private static ModContext Context(string id = "test") => new(new ModInfo(ModManifest.Parse($"id = \"{id}\"\nname = \"Test\"\nversion = \"1\"\n"), Path.GetTempPath()));

    private static List<ModEvent> Listen(Game game)
    {
        var heard = new List<ModEvent>();
        game.Happened += (_, e) => heard.Add(e);
        return heard;
    }

    [Fact]
    public void LearningATechIsAnEventAndTakenUp()
    {
        var game = NewGame();
        var heard = Listen(game);
        var human = game.Players[0];
        var armor = game.Tables.OfKind(TechInfo.ArmorKind).Last();

        game.TeachTech(human, armor);

        Assert.Contains(heard, e => e.Kind == ModEventKind.TechLearned && e.Player == human && e.Tech == armor);
        Assert.Equal(armor.Id, human.BestArmor);
    }

    [Fact]
    public void ACaptureIsOneEventNotAnEndAndAStart()
    {
        var game = NewGame();
        var (human, computer) = (game.Players[0], game.Players[1]);
        var planet = computer.Colonies[0].Planet;
        var heard = Listen(game);

        game.CaptureColony(computer.Colonies[0], human);

        Assert.Contains(heard, e => e.Kind == ModEventKind.ColonyChangedHands && e.Player == human && e.Other == computer && e.Place == planet);
        Assert.DoesNotContain(heard, e => e.Kind is ModEventKind.ColonyFounded or ModEventKind.ColonyDestroyed);
        Assert.Contains(heard, e => e.Kind == ModEventKind.TreatyChanged && e.State == Treaty.War);
    }

    [Fact]
    public void WarAndPeaceAreTreatyEvents()
    {
        var game = NewGame();
        var (a, b) = (game.Players[0], game.Players[1]);
        var heard = Listen(game);

        game.DeclareWar(a, b);
        Assert.True(Game.AtWar(a, b));
        Assert.Equal(Treaty.War, Game.TreatyLevel(a, b));
        game.MakePeace(a, b);
        Assert.False(Game.AtWar(a, b));
        Assert.Equal(Treaty.Neutral, Game.TreatyLevel(b, a));
        Assert.Contains(heard, e => e.Kind == ModEventKind.TreatyChanged && e.State == Treaty.Neutral && e.Value == Treaty.InForce);
    }

    [Fact]
    public void SpawnedShipsCanBeDestroyedAndSaySo()
    {
        var game = NewGame();
        var (human, computer) = (game.Players[0], game.Players[1]);
        var design = human.Designs.OfType<Design>().First();
        var fleet = game.SpawnShips(human, human.Colonies[0].Planet, design, 30);
        Assert.Equal(Fleet.MaxShips, fleet.Ships.Count);
        Assert.All(fleet.Ships, s => Assert.Equal(human, s.Owner));

        var heard = Listen(game);
        var ship = fleet.Ships[0];
        Assert.True(game.DamageShip(ship, ship.Structure, computer));
        Assert.Contains(heard, e => e.Kind == ModEventKind.ShipDestroyed && e.Ship == ship && e.Player == human && e.Other == computer);
    }

    [Fact]
    public void MoneyPopulationAndAMessage()
    {
        var game = NewGame();
        var human = game.Players[0];
        var money = human.Money;
        Game.GiveMoney(human, 500);
        Assert.Equal(money + 500, human.Money);

        game.SetPopulation(human.Colonies[0], 12.5);
        Assert.Equal(12.5, human.Colonies[0].CurrentPopulation);

        game.Tell(human, "Hello from a mod");
        var notice = Assert.Single(game.TakeNotices(human), n => n.Kind == NoticeKind.ModMessage);
        Assert.Equal("Hello from a mod", notice.Text);
        game.Tell(game.Players[1], "nobody hears this"); // a computer player
        Assert.DoesNotContain(game.TakeNotices(game.Players[1]), n => n.Kind == NoticeKind.ModMessage);
    }

    [Fact]
    public void AStoreKeepsAModsValuesUnderItsIdAndInTheHash()
    {
        var game = NewGame();
        var before = game.Hash();
        var store = Context("alpha").Store(game);
        store.SetInt("count", 3);
        store.SetDouble("share", 0.1);
        store.SetBool("on", true);
        store.Set("name", "x");

        Assert.Equal(3, store.GetInt("count"));
        Assert.Equal(0.1, store.GetDouble("share"));
        Assert.True(store.GetBool("on"));
        Assert.Equal(["count", "name", "on", "share"], store.Keys);
        Assert.Equal("3", game.ModState["alpha.count"]);
        Assert.Empty(Context("beta").Store(game).Keys);
        Assert.NotEqual(before, game.Hash());

        store.Set("name", null);
        Assert.Null(store.Get("name"));
        Assert.Equal(7, store.GetInt("missing", 7));
    }

    [Fact]
    public void ARandomStreamIsTheSameForTheSameGameAndModAndLeavesTheGamesOwnAlone()
    {
        int[] Draws(uint seed, string id)
        {
            var game = NewGame(seed);
            var state = game.Random.State;
            var random = Context(id).Random(game);
            var draws = Enumerable.Range(0, 20).Select(_ => random.Next(1000)).ToArray();
            Assert.Equal(state, game.Random.State);
            return draws;
        }

        Assert.Equal(Draws(1, "a"), Draws(1, "a"));
        Assert.NotEqual(Draws(1, "a"), Draws(1, "b"));
        Assert.NotEqual(Draws(1, "a"), Draws(2, "a"));

        var g = NewGame();
        var r = Context().Random(g);
        Assert.All(Enumerable.Range(0, 200).Select(_ => r.NextDouble()), d => Assert.InRange(d, 0, 0.9999999999));
        Assert.All(Enumerable.Range(0, 200).Select(_ => r.Next(3, 6)), n => Assert.InRange(n, 3, 5));
    }

    [Fact]
    public void TypedHandlersHearTheirKindOnly()
    {
        var game = NewGame();
        var context = Context();
        var learned = new List<TechInfo>();
        var wars = 0;
        context.OnTechLearned((_, _, tech) => learned.Add(tech));
        context.OnTreatyChanged((_, _, _, state, _) => wars += state == Treaty.War ? 1 : 0);
        foreach (var handler in context.EventHandlers)
        {
            game.Happened += handler;
        }

        var tech = game.Tables.OfKind(TechInfo.ArmorKind).Last();
        game.TeachTech(game.Players[0], tech);
        game.DeclareWar(game.Players[0], game.Players[1]);

        Assert.Equal([tech], learned);
        Assert.Equal(2, wars);
    }

    [Fact]
    public void AModReadsItsOwnTomlAndLogs()
    {
        using var temp = new TempFolder();
        temp.Write("settings.toml", "speed = 3\nname = \"fast\"\n");
        temp.Write("bad.toml", "speed = = 3\n");
        var context = new ModContext(new ModInfo(ModManifest.Parse("id = \"t\"\nname = \"T\"\nversion = \"1\"\n"), temp.Path));

        var settings = context.ReadToml("settings.toml");
        Assert.Equal(3L, settings["speed"]);
        Assert.Equal("fast", settings["name"]);
        Assert.Empty(context.ReadToml("missing.toml"));
        Assert.Empty(context.ReadToml("bad.toml"));
        Assert.Single(context.LogLines, l => l.StartsWith("t: bad.toml", StringComparison.Ordinal));
    }
}
