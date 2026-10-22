using OpenPax.Sim;
using OpenPax.Sim.Players;

namespace OpenPax.Ai;

/// <summary>
/// How a computer player judges what it can pay for (docs/ai/computer-players.md).
/// The figures mix units on purpose: income times 10 against upkeep the
/// economy charges at a tenth, as the original does.
/// </summary>
internal sealed class Budget(Game game, Player player, double reserve)
{
    // Limits for Afford by mode: 0 defenses, 1 shipyards, 2 training bases, 3 ships, 4 funding.
    private static readonly double[] _limits = [0.2, 0.05, 0.01, 0.74, 0.0];

    public const int Defenses = 0;
    public const int Shipyards = 1;
    public const int Training = 2;
    public const int Ships = 3;

    /// <summary>Cost of every colony's next improvement (G).</summary>
    public double Reserve { get; } = reserve;

    public static double ReserveFor(Game game, Player p)
    {
        var sum = 0.0;
        foreach (var c in p.Colonies)
        {
            var id = game.NextImprovementId(c);
            if (id >= 0)
            {
                sum = TechLevels.Cost(game.Tables.Tech(id), p) + sum;
            }
        }

        return sum;
    }

    /// <summary>Upkeep of the ship at the head of each colony's first shipyard queue.</summary>
    public double ShipQueue()
    {
        var sum = 0.0;
        foreach (var c in player.Colonies)
        {
            if (c.FirstOfKind(TechInfo.ShipyardKind) is { Queue.Count: > 0 } yard)
            {
                var cost = (double)yard.Queue[0].Cost;
                sum = (player.Species.Is(Species.Nomad) ? cost * 0.8 : cost) + sum;
            }
        }

        return sum;
    }

    /// <summary>Income less what research, espionage and construction improvements take.</summary>
    private double Earning()
    {
        var p = player;
        return p.Income * 10.0 - p.Spending[0] * p.ImprovementUpkeep[1] * 0.01
            - p.Spending[1] * p.ImprovementUpkeep[2] * 0.01
            - p.Spending[2] * p.ImprovementUpkeep[3] * 0.01;
    }

    public double Free()
    {
        return Game.NetIncome(player);
    }

    /// <summary>Whether there is room for a cost once improvements and queued ships are allowed for.</summary>
    public bool Spare(double cost)
    {
        return Free() - Reserve - ShipQueue() - cost >= 0.0;
    }

    /// <summary>Whether a kind of spending stays under its share of earnings.</summary>
    public bool Afford(int mode, double extra)
    {
        var p = player;
        var earning = Earning();
        var spend = mode switch
        {
            Defenses => p.StructureUpkeep[2] + extra,
            Shipyards => KindCost(TechInfo.ShipyardKind, p.StructureUpkeep[0]),
            Training => KindCost(TechInfo.TrainingBaseKind, p.StructureUpkeep[1]),
            Ships => ShipQueue() + p.Upkeep + extra,
            _ => p.Funding * 20.0,
        };
        return earning > 0.0 && spend / earning < _limits[mode];
    }

    private double KindCost(int kind, double bucket)
    {
        var best = game.BestOfKind(player, kind);
        return best < 0 ? 999999999.9 : TechLevels.Cost(game.Tables.Tech(best), player) + bucket;
    }

    /// <summary>The best known tech of a kind passes Afford for defenses and leaves something spare.</summary>
    public bool CanAfford(int kind)
    {
        var best = game.BestOfKind(player, kind);
        if (best < 0)
        {
            return false;
        }

        var cost = (double)TechLevels.Cost(game.Tables.Tech(best), player);
        return Afford(Defenses, cost) && Spare(cost);
    }
}
