using OpenPax.Sim.Numerics;
using OpenPax.Sim.Players;

namespace OpenPax.Sim;

/// <summary>The empire task: treaty income, then each player's economy (docs/sim/economy.md).</summary>
public sealed partial class Game
{
    private const int ResearchRate = 0;
    private const int ReproductionRate = 1;
    private const int ConstructionRate = 2;
    private const int EspionageRate = 3;
    private const int FinanceRate = 4;
    private const double TreatyRamp = 0.005;
    private const double FundingCredits = 20.0;

    private void RunEmpireTask(ref StepEvents events)
    {
        foreach (var player in Players)
        {
            TreatyIncome(player);
        }

        foreach (var player in Players.ToList())
        {
            RunEconomy(player, ref events);
        }
    }

    /// <summary>
    /// Trade (state 2) and research (state 5) treaties: the player receives
    /// a share of the partner's own income and research, ramping in.
    /// </summary>
    private void TreatyIncome(Player p)
    {
        foreach (var q in Players)
        {
            if (q == p)
            {
                continue;
            }

            var i = q.Index;
            if (p.Treaties[i].Has(2))
            {
                var t = (q.Income - OthersSum(q.TradeReceipts, q.Index)) * 0.1;
                if (p.Species.Is(Species.Merchant))
                {
                    t = t + t;
                }

                p.TradeRamp[i] = Math.Min(p.TradeRamp[i] + TreatyRamp, 1.0);
                if (p.TradeRamp[i] < 1.0)
                {
                    t = p.TradeRamp[i] * t;
                }

                p.TradeReceipts[i] = t;
                Incident(q, p, IncidentKind.TradeRoutes);
            }
            else
            {
                p.TradeRamp[i] = 0.0;
                p.TradeReceipts[i] = 0.0;
            }

            if (p.Treaties[i].Has(5))
            {
                var u = q.ResearchPoints - OthersSum(q.ResearchReceipts, q.Index);
                var own = p.ResearchPoints - OthersSum(p.ResearchReceipts, p.Index);
                if (own < u)
                {
                    u = own;
                }

                u *= 0.5;
                p.ResearchRamp[i] = Math.Min(p.ResearchRamp[i] + TreatyRamp, 1.0);
                if (p.ResearchRamp[i] < 1.0)
                {
                    u = p.ResearchRamp[i] * u;
                }

                p.ResearchReceipts[i] = u;
                Incident(q, p, IncidentKind.JointResearch);
            }
            else
            {
                p.ResearchRamp[i] = 0.0;
                p.ResearchReceipts[i] = 0.0;
            }
        }
    }

    /// <summary>Sum over every slot but one, each added in front of the running total, as the original does.</summary>
    private static double OthersSum(double[] values, int skip)
    {
        var sum = 0.0;
        for (var j = 0; j < Player.MaxPlayers; j++)
        {
            if (j != skip)
            {
                sum = values[j] + sum;
            }
        }

        return sum;
    }

    private void RunEconomy(Player p, ref StepEvents events)
    {
        p.ResearchPoints = 0.0;
        p.EspionagePoints = 0.0;
        p.Income = 0.0;

        foreach (var c in p.Colonies.ToList())
        {
            if (c.Destroyed)
            {
                continue;
            }

            var f = HabitabilityFactor(c);
            var credits = (c.Flat[Colony.Credits] + c.CurrentPopulation * c.PerPopulation[Colony.Credits]) * c.Planet.Score * Rate(p, FinanceRate) * 0.1;
            SetColonyResource(c, Colony.Credits, credits, ref events);
            p.Income = c.Resources[Colony.Credits] + p.Income;

            var g = Rate(p, ReproductionRate) * 0.1;
            c.Growth = c.CurrentPopulation * ((c.PerPopulation[Colony.Population] + g) * f) * 0.1;
            if (c.MaxPopulation < c.CurrentPopulation + c.Growth)
            {
                c.Growth = c.MaxPopulation - c.CurrentPopulation;
            }

            SetColonyResource(c, Colony.Population, c.Growth + c.CurrentPopulation, ref events);
        }

        for (var j = 0; j < Player.MaxPlayers; j++)
        {
            if (j != p.Index)
            {
                p.Income = p.TradeReceipts[j] + p.Income;
            }
        }

        p.Money = Math.Max(0.0, p.Money + p.Income);

        var upkeep = (((p.StructureUpkeep[2] + p.StructureUpkeep[1]) + p.StructureUpkeep[0]) + p.Upkeep) * 0.1;
        if (upkeep <= p.Money)
        {
            p.Money = Math.Max(0.0, p.Money - upkeep);
            p.Support = 100.0;
        }
        else
        {
            p.Support = p.Money / upkeep * 100.0;
            p.Money = 0.0;
        }

        Budget(p);

        foreach (var c in p.Colonies.ToList())
        {
            if (c.Destroyed)
            {
                continue;
            }

            var f = HabitabilityFactor(c);
            var research = p.Spending[0] * 0.01 * Output(c, Colony.ResearchResource) * (AdvisorSum(p, c.Star, 0) - -1.0) * Rate(p, ResearchRate) * 0.1;
            SetColonyResource(c, Colony.ResearchResource, research, ref events);
            var espionage = p.Spending[1] * 0.01 * Output(c, Colony.Espionage) * (AdvisorSum(p, c.Star, 1) + 1.0) * 0.1;
            SetColonyResource(c, Colony.Espionage, espionage, ref events);
            var construction = p.Spending[2] * 0.01 * Output(c, Colony.Construction) * f * (AdvisorSum(p, c.Star, 2) + 1.0) * Rate(p, ConstructionRate) * 0.1;
            SetColonyResource(c, Colony.Construction, construction, ref events);
            SetColonyResource(c, Colony.Construction, c.Funding * 0.1 + c.Resources[Colony.Construction], ref events);
            c.ConstructionPoints = c.Resources[Colony.Construction];
            p.ResearchPoints = Math.Max(0.0, c.Resources[Colony.ResearchResource] + p.ResearchPoints);
            p.EspionagePoints = Math.Max(0.0, c.Resources[Colony.Espionage] + p.EspionagePoints);
        }

        // Espionage per head: the empire's average, and each colony's own less it.
        var total = 0.0;
        foreach (var c in p.Colonies)
        {
            total = c.CurrentPopulation + total;
        }

        var spy = Rate(p, EspionageRate);
        p.EspionagePoints = total < 1.0 ? 0.0 : Math.Max(0.0, p.EspionagePoints / total * spy);
        foreach (var c in p.Colonies.ToList())
        {
            SetColonyResource(c, Colony.Espionage, c.Resources[Colony.Espionage] * spy / c.CurrentPopulation - p.EspionagePoints, ref events);
        }

        for (var j = 0; j < Player.MaxPlayers; j++)
        {
            if (j != p.Index)
            {
                p.ResearchPoints = Math.Max(0.0, p.ResearchReceipts[j] + p.ResearchPoints);
            }
        }

        foreach (var c in p.Colonies.ToList())
        {
            if (!c.Destroyed)
            {
                ConstructColony(c, false, ref events);
            }
        }

        foreach (var fleet in p.Fleets.ToList())
        {
            FleetUpkeep(fleet, false);
        }
    }

    /// <summary>
    /// Charges improvement upkeep at the spending percents and construction
    /// funding at 2 credits a unit. Short of money, funding is cut first,
    /// then the percents are scaled down.
    /// </summary>
    private static void Budget(Player p)
    {
        var money = p.Money;
        double Charge(int k) => p.ImprovementUpkeep[k + 1] * p.Spending[k] * 0.001;

        var cR = Charge(0);
        var cE = Charge(1);
        var cC = Charge(2);
        var rest = money - cR - cE - cC;
        var funding = p.Funding * FundingCredits * 0.1;
        if (rest - funding < 0.0 && funding > 0.0)
        {
            var target = Math.Max(0.0, ((rest - funding) / FundingCredits - 1.0) * 10.0 + p.Funding);
            CutFunding(p, target);
            funding = p.Funding * FundingCredits * 0.1;
        }

        if (rest - funding >= 0.0)
        {
            p.Money = Math.Max(0.0, money - cR - cE - cC - funding);
            return;
        }

        var total = funding + cC + cE + cR;
        var charges = new[] { cR, cE, cC };
        for (var k = 0; k < 3; k++)
        {
            var u = p.ImprovementUpkeep[k + 1];
            if (u * 0.1 > 0.0)
            {
                // truncates again and keeps 0 to 100.
                var percent = X87.ToInt(charges[k] * money / total / (u * 0.1) * 100.0);
                p.Spending[k] = Math.Clamp(percent, 0, 100);
            }
        }

        cR = Charge(0);
        cE = Charge(1);
        cC = Charge(2);
        p.Money = Math.Max(0.0, money - cR - cE - cC - funding);
    }

    /// <summary>Brings total funding down toward a target, each funded colony giving up its share.</summary>
    private static void CutFunding(Player p, double target)
    {
        var old = p.Funding;
        var sum = 0.0;
        foreach (var c in p.Colonies)
        {
            if (c.Funding > 0.0)
            {
                sum = sum + c.Funding;
            }
        }

        foreach (var c in p.Colonies)
        {
            if (c.Funding > 0.0)
            {
                var d = c.Funding / sum * (old - target) * -1.0;
                c.Funding += d;
                p.Funding = Math.Max(p.Funding + d, 0.0);
                c.Funding = Math.Max(c.Funding, 0.0);
            }
        }
    }

    /// <summary>1, 0.5 or 0.05 for habitability 2, 1 or 0.</summary>
    private static double HabitabilityFactor(Colony c)
    {
        return Colony.Habitability(c.Owner.Species, c.Planet) switch
        {
            2 => 1.0,
            1 => 0.5,
            _ => 0.05,
        };
    }

    /// <summary>1 + 0.1 per point of a rate modifier, written as 1 - m * -0.1.</summary>
    private static double Rate(Player p, int modifier)
    {
        return 1.0 - p.Species.RateModifiers[modifier] * -0.1;
    }

    private static double Output(Colony c, int resource)
    {
        return c.Flat[resource] + c.CurrentPopulation * c.PerPopulation[resource];
    }

    /// <summary>Every advisor's value for a field, counting governors only at their own star.</summary>
    private static double AdvisorSum(Player p, int star, int field)
    {
        var sum = 0.0;
        foreach (var a in p.Advisors)
        {
            if (a.Role != Advisor.GovernorRole || a.Star == star)
            {
                sum = a.Value(field) + sum;
            }
        }

        return sum;
    }
}
