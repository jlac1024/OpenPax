using OpenPax.Sim.Numerics;
using OpenPax.Sim.Players;

namespace OpenPax.Sim;

/// <summary>Sampling empire statistics for the comparison graphs (docs/sim/history.md).</summary>
public sealed partial class Game
{
    /// <summary>Every 200 ticks, every player (eliminated ones too) records ten statistics.</summary>
    // (then a redraw notice to the local player, UI only)
    private void RecordHistory(ref StepEvents events)
    {
        foreach (var p in Players)
        {
            RecordHistory(p);
        }

        foreach (var p in Players)
        {
            Raise(p, StepEvents.HistoryRecorded);
        }
    }

    private void RecordHistory(Player p)
    {
        var money = X87.ToInt(p.Money);
        var colonies = p.Colonies.Count;
        var fleets = p.Fleets.Sum(f => f.Strength);
        var research = X87.ToInt(p.ResearchPoints * 10.0);
        var construction = p.Colonies.Sum(c => X87.ToInt(c.Resources[Colony.Construction]));
        var yards = 0;
        foreach (var c in p.Colonies)
        {
            if (c.FirstOfKind(TechInfo.ShipyardKind) is { } yard)
            {
                yards += X87.ToInt(YardWork(yard, c.Resources[Colony.Construction] * 10.0));
            }
        }

        var espionage = X87.ToInt(p.EspionagePoints * 10.0);
        var techs = p.KnownCount;
        var population = p.Colonies.Sum(c => X87.ToInt(c.Resources[Colony.Population]));

        // Quirk kept: the whole number quotients pass through a double and ToInt as well.
        var score = X87.ToInt((double)(money / 20000)) + colonies + X87.ToInt((double)(population / 5))
            + X87.ToInt(research * 0.01) + X87.ToInt((double)(fleets / 100)) + X87.ToInt(construction * 0.01)
            + X87.ToInt(yards * 0.01) + X87.ToInt(espionage * 0.01) + X87.ToInt(techs * 0.2);

        var h = p.History;
        h.Append(EmpireHistory.Money, money);
        h.Append(EmpireHistory.Colonies, colonies);
        h.Append(EmpireHistory.FleetStrength, fleets);
        h.Append(EmpireHistory.Population, population);
        h.Append(EmpireHistory.Research, research);
        h.Append(EmpireHistory.Construction, construction);
        h.Append(EmpireHistory.YardWork, yards);
        h.Append(EmpireHistory.Espionage, espionage);
        h.Append(EmpireHistory.TechsKnown, techs);
        h.Append(EmpireHistory.Score, score);
    }
}
