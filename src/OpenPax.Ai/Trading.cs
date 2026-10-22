using OpenPax.Sim;
using OpenPax.Sim.Players;

namespace OpenPax.Ai;

/// <summary>
/// A computer player answers the trade packages waiting for it, in its
/// treaties and trade module: gifts are taken, exchanges weighed by value
/// and friendship, demands met only by a much stronger player and with
/// patience running out.
/// </summary>
internal static class Trading
{
    public static void Answer(Game game, Player p)
    {
        foreach (var k in p.Packages.ToList())
        {
            var q = k.Proposer;
            bool accept;
            if (k.IsGift)
            {
                accept = true;
            }
            else if (!k.IsDemand)
            {
                var (a, b) = (k.Offered, k.Asked);
                var cost = game.ShipsValue(b, q);
                cost = game.PlanetsValue(b, p) + cost;
                cost = Game.TechsValue(b, q) + cost;
                cost = Game.CreditsValue(b) + cost;
                cost = game.MapValue(b, p, q) + cost;
                var gain = game.ShipsValue(a, p);
                gain = game.PlanetsValue(a, q) + gain;
                gain = Game.TechsValue(a, p) + gain;
                gain = Game.CreditsValue(a) + gain;
                gain = game.MapValue(a, q, p) + gain;

                var level = p.Treaties[q.Index].Level();
                var wanted = level is < 1 or >= 7 ? 0.0
                    : level <= 3 ? gain - cost - -20.0
                    : gain - cost - -50.0;
                var r = game.Random.Next(100);
                accept = (double)r - -1.0 < wanted && !Veto(game, k);
            }
            else
            {
                var old = p.Patience[q.Index];
                p.Patience[q.Index] = old - 10.0;
                if (game.AverageStrength(q) <= game.AverageStrength(p) * 1.5)
                {
                    accept = false;
                }
                else
                {
                    var r = game.Random.Next(100);
                    accept = (double)r - -1.0 < old && !Veto(game, k);
                }
            }

            game.AnswerPackage(k, accept);
        }
    }

    /// <summary>
    /// Refuses to give ships worth a quarter of its fleets or planets worth a
    /// tenth of its colonies. Quirk kept: a player with no fleets refuses
    /// every exchange, and one whose colonies are all of strength 0 too.
    /// </summary>
    private static bool Veto(Game game, TradePackage k)
    {
        var p = k.Target;
        var ships = 0.0;
        foreach (var id in k.Asked.Ships)
        {
            if (game.FindShip(id) is { } ship)
            {
                ships = (double)ship.Strength(game.Tables.Tech) + ships;
            }
        }

        var fleets = 0.0;
        foreach (var f in p.Fleets)
        {
            fleets = f.Strength + fleets;
        }

        if (!(ships < fleets * 0.25))
        {
            return true;
        }

        var planets = 0.0;
        foreach (var id in k.Asked.Planets)
        {
            if (game.FindPlanet(id) is { Colony: { } c } && c.Owner == p)
            {
                planets = (double)game.ColonyStrength(c) + planets;
            }
        }

        var colonies = 0.0;
        foreach (var c in p.Colonies)
        {
            colonies = (double)game.ColonyStrength(c) + colonies;
        }

        return planets >= colonies * 0.1;
    }
}
