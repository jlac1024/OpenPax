using OpenPax.Sim.Galaxy;
using OpenPax.Sim.Setup;

namespace OpenPax.Sim.Tests;

public class NewGameTests
{
    private static GameSetup Setup(uint seed, int difficulty, int players = 4, int stars = 30)
    {
        var tables = GameTablesForTests.Load();
        var human = new HumanPlayerSetup("Commander", 0, 0, tables.Species[0]);
        return new GameSetup(seed, stars, players, difficulty, [human]);
    }

    [Fact]
    public void SameSetupSameGame()
    {
        var tables = GameTablesForTests.Load();

        Assert.Equal(NewGame.Create(Setup(1234, 3), tables).Hash(), NewGame.Create(Setup(1234, 3), tables).Hash());
        Assert.NotEqual(NewGame.Create(Setup(1234, 3), tables).Hash(), NewGame.Create(Setup(1235, 3), tables).Hash());
    }

    [Fact]
    public void TheGalaxyDependsOnlyOnSeedAndStarCount()
    {
        var tables = GameTablesForTests.Load();
        var game = NewGame.Create(Setup(1234, 3), tables);
        var alone = UniverseGenerator.Generate(1234, 30, tables.GalaxyNames, new GameRandom(0), new ObjectIds());

        Assert.Equal(alone.Stars.Select(s => (s.X, s.Y, s.Name)), game.Universe.Stars.Select(s => (s.X, s.Y, s.Name)));
    }

    [Theory]
    [InlineData(1u, 1)]
    [InlineData(99u, 3)]
    [InlineData(4242u, 5)]
    public void PlayersStartAsTheOriginalSetsThemUp(uint seed, int difficulty)
    {
        var tables = GameTablesForTests.Load();
        var game = NewGame.Create(Setup(seed, difficulty, players: 6), tables);

        Assert.Equal(6, game.Players.Count);
        Assert.Equal(6, game.Players.Select(p => p.HomeStar).Distinct().Count());
        Assert.Equal(6, game.Players.Select(p => p.Flag).Distinct().Count());
        Assert.Equal(6, game.Players.Select(p => p.FleetColor).Distinct().Count());
        foreach (var p in game.Players)
        {
            var planet = game.Universe.Stars[p.HomeStar].Planets.Single(x => x.Slot == p.HomePlanetSlot);
            Assert.Equal(3, planet.Type);
            Assert.Equal(p.Species.PreferredTemperature, planet.Temperature);
            Assert.Equal(p.IsAi ? 11.0 : 11.0 - 2.0 * (difficulty - 1), planet.Score);

            Assert.Equal(5, p.Advisors.Count);
            // Each new kind goes to the front: the missile base, then the shipyard, both finished.
            Assert.Equal([15, 10], p.Colonies.Single().Structures.Select(s => s.Tech.Id));
            Assert.All(p.Colonies[0].Structures, s => Assert.Equal(100.0, s.Percent));
            Assert.Equal(p.Colonies[0].MaxPopulation, p.Colonies[0].CurrentPopulation);
            Assert.InRange(p.Research[0], 0, 20);
            Assert.InRange(p.Research.Sum(), 0, 100);
            Assert.All(Enumerable.Range(0, 16), j => Assert.InRange(p.Relations[j], -3, 3));
            Assert.Equal(0, p.Relations[p.Index]);

            if (p.IsAi)
            {
                Assert.Equal(difficulty, p.Fleets.Count);
                Assert.Contains(2, p.KnownTechs);
                Assert.Contains(3, p.KnownTechs);
            }
            else
            {
                Assert.Equal(2, p.Fleets.Count);
                Assert.Equal(new[] { 2, 2, 1, 1, 0 }[difficulty - 1], p.Fleets[1].Ships.Count);
            }
        }
    }

    [Fact]
    public void ADifficultyFiveHumanStillDrawsForAnEmptyFleet()
    {
        var tables = GameTablesForTests.Load();
        var game = NewGame.Create(Setup(7, 5, players: 1), tables);
        var human = game.Players.Single();

        // Fleets are added to the front: the empty escort fleet is second.
        Assert.Empty(human.Fleets[1].Ships);
        Assert.Single(human.Fleets[0].Ships);
    }

    [Fact]
    public void ALoneHumanMakesExactlyTheOriginalsDraws()
    {
        var tables = GameTablesForTests.Load();
        var game = NewGame.Create(Setup(321, 2, players: 1), tables);
        var galaxy = new GameRandom(0);
        UniverseGenerator.Generate(321, 30, tables.GalaxyNames, galaxy, new ObjectIds());

        // 8 for the four advisors; no relations with nobody else; the
        // galaxy's own draws; 1 home star (any star is far enough when
        // alone); 1 governor; 2 per fleet for its orbit, two fleets.
        Assert.Equal(8 + galaxy.Draws + 1 + 1 + 4, game.Random.Draws);
    }

    [Fact]
    public void AStartingGameIsPinned()
    {
        var game = NewGame.Create(Setup(1234, 3), GameTablesForTests.Load());

        // Records the port's current result (the hash covers the clock, treaties, system fleets and
        // task countdowns too), so any change to the order of
        // draws shows up. Not yet checked against the original.
        Assert.Equal(353522822425306433UL, game.Hash());
    }
}
