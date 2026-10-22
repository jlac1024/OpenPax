using OpenPax.Sim;
using OpenPax.Sim.Galaxy;
using OpenPax.Sim.Players;
using OpenPax.Sim.Saving.Original;

namespace OpenPax.Ai.Tests;

/// <summary>
/// Loading the original's saves. There is no save made by the original to
/// test with, so the files come from a writer that follows the same study.
/// </summary>
public class OriginalSaveTests
{
    private static byte[] Written(Game game, int currentStar = 0, int selection = -1)
    {
        using var stream = new MemoryStream();
        new OriginalSaveWriter(stream).Write(game, currentStar, selection);
        return stream.ToArray();
    }

    private static (Game Game, int CurrentStar, int Selection) Load(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        return OriginalSave.Load(stream, AiGames.Tables(), new ComputerPlayer());
    }

    private static Game Played()
    {
        var game = AiGames.New(seed: 2024, players: 6);
        AiGames.Run(game, 4000);
        return game;
    }

    [Fact]
    public void TheHeaderIsRecognized()
    {
        var bytes = Written(Played());
        using (var stream = new MemoryStream(bytes))
        {
            Assert.True(OriginalSave.IsSave(stream));
        }

        bytes[2] ^= 0xFF;
        using (var stream = new MemoryStream(bytes))
        {
            Assert.False(OriginalSave.IsSave(stream));
        }

        using var empty = new MemoryStream([0x49]);
        Assert.False(OriginalSave.IsSave(empty));
    }

    [Fact]
    public void TheGalaxyComesBack()
    {
        var game = Played();
        var star = game.Universe.Stars[3];
        var (loaded, current, selection) = Load(Written(game, 3, star.Planets[0].ObjectId));
        Assert.Equal(3, current);
        Assert.Equal(star.Planets[0].ObjectId, selection);
        Assert.Equal(game.Setup.Seed, loaded.Setup.Seed);
        Assert.Equal(game.Setup.Difficulty, loaded.Setup.Difficulty);
        Assert.Equal((game.Calendar.Year, game.Calendar.Step), (loaded.Calendar.Year, loaded.Calendar.Step));
        Assert.Equal(game.ObjectIds.Next, loaded.ObjectIds.Next);
        Assert.Equal(game.SecondaryIds.Next, loaded.SecondaryIds.Next);
        Assert.Equal(game.Universe.Stars.Count, loaded.Universe.Stars.Count);
        for (var s = 0; s < game.Universe.Stars.Count; s++)
        {
            var (a, b) = (game.Universe.Stars[s], loaded.Universe.Stars[s]);
            Assert.Equal((a.Name, a.ObjectId, a.X, a.Y, a.Type), (b.Name, b.ObjectId, b.X, b.Y, b.Type));
            Assert.Equal(a.Sight, b.Sight);
            Assert.Equal(a.Planets.Select(p => (p.Name, p.ObjectId, p.Slot, p.Class, p.Temperature, p.Score, p.Orbit.Radius, p.Orbit.Angle, p.Orbit.Speed, p.X, p.Y)),
                b.Planets.Select(p => (p.Name, p.ObjectId, p.Slot, p.Class, p.Temperature, p.Score, p.Orbit.Radius, p.Orbit.Angle, p.Orbit.Speed, p.X, p.Y)));
            Assert.Equal(a.Planets.Select(p => p.Satellites.Select(m => (m.Radius, m.Speed))).SelectMany(x => x),
                b.Planets.Select(p => p.Satellites.Select(m => (m.Radius, m.Speed))).SelectMany(x => x));
            Assert.Equal(a.Wormholes.Select(w => (w.ObjectId, w.Partner?.ObjectId)), b.Wormholes.Select(w => (w.ObjectId, w.Partner?.ObjectId)));
            Assert.Equal(a.Fleets.Select(f => f.ObjectId).Order(), b.Fleets.Select(f => f.ObjectId).Order());
            Assert.All(b.Planets.Where(p => p.Colony is not null), p => Assert.Same(p, p.Colony!.Planet));
        }
    }

    [Fact]
    public void ThePlayersComeBack()
    {
        var game = Played();
        var loaded = Load(Written(game)).Game;
        Assert.Equal(game.Players.Count, loaded.Players.Count);
        for (var i = 0; i < game.Players.Count; i++)
        {
            var (a, b) = (game.Players[i], loaded.Players[i]);
            Assert.Equal((a.Index, a.IsAi, a.Name, a.Flag, a.FleetColor, a.Eliminated), (b.Index, b.IsAi, b.Name, b.Flag, b.FleetColor, b.Eliminated));
            Assert.Equal(a.Species with { }, b.Species with { Atmospheres = a.Species.Atmospheres, PhysicalTraits = a.Species.PhysicalTraits, CulturalTraits = a.Species.CulturalTraits, RateModifiers = a.Species.RateModifiers, ResearchLevels = a.Species.ResearchLevels });
            Assert.Equal(a.Species.CulturalTraits, b.Species.CulturalTraits);
            Assert.Equal(a.Species.RateModifiers, b.Species.RateModifiers);
            Assert.Equal((a.Money, a.Income, a.Upkeep, a.Funding, a.Support, a.Caution, a.BestArmor), (b.Money, b.Income, b.Upkeep, b.Funding, b.Support, b.Caution, b.BestArmor));
            Assert.Equal(a.Spending, b.Spending);
            Assert.Equal(a.Relations, b.Relations);
            Assert.Equal(a.RelationScores, b.RelationScores);
            Assert.Equal(a.TradeReceipts, b.TradeReceipts);
            Assert.Equal(a.KnownTechs, b.KnownTechs);
            Assert.Equal(a.Research, b.Research);
            Assert.Equal(a.ResearchSlots.Select(s => (s.Tech, s.Cost, s.Progress, s.Fraction)), b.ResearchSlots.Select(s => (s.Tech, s.Cost, s.Progress, s.Fraction)));
            Assert.Equal(a.SavedProgress.Where(kv => kv.Value != 0).OrderBy(kv => kv.Key), b.SavedProgress.OrderBy(kv => kv.Key));
            Assert.Equal(a.DesignCounters, b.DesignCounters);
            Assert.Equal(a.Plans.OfType<int[]>().SelectMany(x => x), b.Plans.OfType<int[]>().SelectMany(x => x));
            for (var j = 0; j < Player.MaxPlayers; j++)
            {
                Assert.Equal(Enumerable.Range(0, Treaty.States).Select(a.Treaties[j].Slot), Enumerable.Range(0, Treaty.States).Select(b.Treaties[j].Slot));
            }

            for (var s = 0; s < 10; s++)
            {
                Assert.Equal(a.History.Series(s), b.History.Series(s));
            }

            Assert.Equal(a.Advisors.Select(x => (x.Id, x.Role, x.Star, x.Portrait, x.TurnedBy?.Index)), b.Advisors.Select(x => (x.Id, x.Role, x.Star, x.Portrait, x.TurnedBy?.Index)));
            Assert.Equal(a.Missions.Select(m => (m.Id, m.Kind, m.Level, m.Target.Index, m.Progress)), b.Missions.Select(m => (m.Id, m.Kind, m.Level, m.Target.Index, m.Progress)));

            Assert.Equal(a.Colonies.Count, b.Colonies.Count);
            for (var c = 0; c < a.Colonies.Count; c++)
            {
                var (x, y) = (a.Colonies[c], b.Colonies[c]);
                Assert.Equal((x.Planet.ObjectId, x.Star, x.MaxPopulation, x.Growth, x.Funding, x.ConstructionPoints, x.Plan, x.StructureUpkeep), (y.Planet.ObjectId, y.Star, y.MaxPopulation, y.Growth, y.Funding, y.ConstructionPoints, y.Plan, y.StructureUpkeep));
                Assert.Equal(x.Resources, y.Resources);
                Assert.Equal(x.ImprovementUpkeep, y.ImprovementUpkeep);
                Assert.Equal(x.Improvements.Select(m => (m?.Tech.Id, m?.Built)), y.Improvements.Select(m => (m?.Tech.Id, m?.Built)));
                Assert.Equal(x.Structures.Select(s => (s.Tech.Id, s.Count, s.HitPoints, s.Progress, s.Total, s.Queue.Count)), y.Structures.Select(s => (s.Tech.Id, s.Count, s.HitPoints, s.Progress, s.Total, s.Queue.Count)));
            }

            Assert.Equal(a.Fleets.Count, b.Fleets.Count);
            for (var f = 0; f < a.Fleets.Count; f++)
            {
                var (x, y) = (a.Fleets[f], b.Fleets[f]);
                Assert.Equal((x.ObjectId, x.Name, x.Star, x.X, x.Y, x.Mode, x.Intercepting, x.OrbitAngle, x.OrbitSpeed), (y.ObjectId, y.Name, y.Star, y.X, y.Y, y.Mode, y.Intercepting, y.OrbitAngle, y.OrbitSpeed));
                Assert.Equal((x.Speed, x.Strength, x.JumpRange, x.JumpsUsed), (y.Speed, y.Strength, y.JumpRange, y.JumpsUsed));
                Assert.Equal((x.OrbitCenter as Planet)?.ObjectId, (y.OrbitCenter as Planet)?.ObjectId);
                Assert.Equal(x.Orders.Select(o => o.Position), y.Orders.Select(o => o.Position));
                Assert.Equal(x.Ships.Select(Ship), y.Ships.Select(Ship));
            }

            Assert.Equal(a.Designs.Select(d => d is null ? null : Ship(d)), b.Designs.Select(d => d is null ? null : Ship(d)));
        }
    }

    private static string Ship(Design d) =>
        $"{d.Name}|{d.ClassName}|{d.Type}|{d.Quality}|{d.Id}|{d.ShipId}|{d.Slot}|{d.Structure}/{d.MaxStructure}|{d.Cost}|{d.UsedSpace}|{d.ExperienceLevel}|{d.ExperiencePoints}|"
        + string.Join(",", d.Components.Select(c => $"{c.Tech.Id}:{c.Mode}:{c.Count}:{c.Damage}"));

    [Fact]
    public void ALoadedGamePlaysOnTheSameWayEachTime()
    {
        var bytes = Written(Played());
        var first = Load(bytes).Game;
        var second = Load(bytes).Game;
        Assert.Equal(first.Hash(), second.Hash());
        for (var i = 0; i < 3000; i++)
        {
            first.Step();
            second.Step();
        }

        Assert.Equal(first.Hash(), second.Hash());
    }
}
