using OpenPax.Sim.Players;

namespace OpenPax.Ai.Tests;

public class WarTests
{
    [Fact]
    public void MissileBasesCarryTheirMissiles()
    {
        var game = AiGames.New();
        foreach (var p in game.Players)
        {
            var missileBase = p.Colonies[^1].FirstOfKind(TechInfo.MissileBaseKind)!;
            Assert.Contains(missileBase.WeaponTechs, t => t?.Kind == TechInfo.MissileKind);
            Assert.True(game.StructureStrength(missileBase) > 0);
        }
    }

    [Fact]
    public void PlayersAtWarBuildWarshipsAndSendThem()
    {
        var game = AiGames.New(1234, 2, 3);
        var (a, b) = (game.Players[0], game.Players[1]);

        // War by hand, with scores hostile enough that neither offers peace.
        a.Treaties[b.Index].Write(Treaty.War, Treaty.InForce);
        b.Treaties[a.Index].Write(Treaty.War, Treaty.InForce);
        a.RelationScores[b.Index] = -200.0;
        b.RelationScores[a.Index] = -200.0;
        var startShips = a.Fleets.Sum(f => f.Ships.Count(s => s.Type != 2));

        var sent = false;
        for (var i = 0; i < 20000 && !sent; i++)
        {
            game.Step();
            sent = a.Fleets.Any(f => !Fleets.IsColonyFleet(f) && f.Orders.Any(o => o.Target is Fleet { Owner: var owner } && owner == b
                || o.Target is Sim.Galaxy.Planet { Owner: var po } && po == b));
        }

        var warships = a.Fleets.Sum(f => f.Ships.Count(s => s.Type != 2))
            + a.Colonies.Sum(c => c.FirstOfKind(TechInfo.ShipyardKind)?.Queue.Count(s => s.Type != 2) ?? 0);
        Assert.True(warships > startShips || sent, "no warship built and no attack sent");
    }

    [Fact]
    public void WarGamesAreDeterministic()
    {
        ulong Run()
        {
            var game = AiGames.New(5, 3, 4);
            game.Players[0].Treaties[1].Write(Treaty.War, Treaty.InForce);
            game.Players[1].Treaties[0].Write(Treaty.War, Treaty.InForce);
            AiGames.Run(game, 10000);
            return game.Hash();
        }

        Assert.Equal(Run(), Run());
    }
}
