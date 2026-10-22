using OpenPax.Sim.Galaxy;
using OpenPax.Sim.Players;
using OpenPax.Sim.Setup;

namespace OpenPax.Sim;

/// <summary>Trade packages: offering, answering and carrying them out (docs/sim/trade.md).</summary>
public sealed partial class Game
{
    /// <summary>Sends a package: a copy waits at the front of the target's list. Nothing is checked yet.</summary>
    public StepEvents OfferPackage(TradePackage package)
    {
        package.Target.Packages.Insert(0, package.Copy());
        Notify(new GameNotice(NoticeKind.TradeOffered, package.Target, Other: package.Proposer));
        Raise(package.Target, StepEvents.TradeOffered);
        return StepEvents.None;
    }

    /// <summary>
    /// Answers a package. Accepting checks both halves and, if they hold,
    /// moves everything with an incident for each part; rejecting is an
    /// incident. Either way the first matching pending package goes.
    /// </summary>
    // actions 1 and 2
    public StepEvents AnswerPackage(TradePackage package, bool accept)
    {
        var (p, t) = (package.Proposer, package.Target);
        Raise(p, StepEvents.TradeAnswered);
        if (accept)
        {
            if (Holds(package.Offered, p) && Holds(package.Asked, t))
            {
                var (a, b) = (package.Offered, package.Asked);
                var events = StepEvents.None;
                Incident(p, t, IncidentKind.ShipsGiven, ShipsValue(a, t));
                GiveShips(a, t);
                Incident(p, t, IncidentKind.ShipsReceived, ShipsValue(b, p));
                GiveShips(b, p);
                Incident(p, t, IncidentKind.PlanetsGiven, PlanetsValue(a, p));
                GivePlanets(a, p, t, ref events);
                Incident(p, t, IncidentKind.PlanetsReceived, PlanetsValue(b, t));
                GivePlanets(b, t, p, ref events);
                Incident(p, t, IncidentKind.TechsGiven, TechsValue(a, t));
                GiveTechs(a, t);
                Incident(p, t, IncidentKind.TechsReceived, TechsValue(b, p));
                GiveTechs(b, p);
                Incident(p, t, IncidentKind.CreditsGiven, CreditsValue(a));
                GiveCredits(a, p, t);
                Incident(p, t, IncidentKind.CreditsReceived, CreditsValue(b));
                GiveCredits(b, t, p);
                Incident(p, t, IncidentKind.MapGiven, MapValue(a, p, t));
                GiveMap(a, p, t);
                Incident(p, t, IncidentKind.MapReceived, MapValue(b, t, p));
                GiveMap(b, t, p);
                RemovePending(package);
                Notify(new GameNotice(NoticeKind.TradeAccepted, p, Other: t));
                return events;
            }

            // A package that no longer holds is dropped; only a notice says so.
            Raise(t, StepEvents.TradeAnswered);
            Notify(new GameNotice(NoticeKind.TradeFailed, p, Other: t));
            Notify(new GameNotice(NoticeKind.TradeFailed, t, Other: p));
        }
        else
        {
            Incident(p, t, IncidentKind.TradeRejected);
            Notify(new GameNotice(NoticeKind.TradeRejected, p, Other: t));
        }

        RemovePending(package);
        return StepEvents.None;
    }

    private static void RemovePending(TradePackage package)
    {
        var list = package.Target.Packages;
        var index = list.FindIndex(k => k.Matches(package));
        if (index >= 0)
        {
            list.RemoveAt(index);
        }
    }

    /// <summary>The side's player is still in and has everything its half names.</summary>
    private bool Holds(TradeHalf half, Player owner)
    {
        return !owner.Eliminated
            && half.Planets.All(id => FindPlanet(id)?.Colony?.Owner == owner)
            && half.Techs.All(owner.Knows)
            && half.Ships.All(id => FindShip(id)?.Fleet?.Owner == owner)
            && (double)half.Credits <= owner.Money;
    }

    internal Planet? FindPlanet(int objectId)
    {
        return Universe.Stars.SelectMany(s => s.Planets).FirstOrDefault(p => p.ObjectId == objectId);
    }

    /// <summary>A ship by its id, looked for system by system, fleet by fleet.</summary>
    public Design? FindShip(int shipId)
    {
        foreach (var star in Universe.Stars)
        {
            foreach (var fleet in star.Fleets)
            {
                if (fleet.Ships.FirstOrDefault(s => s.ShipId == shipId) is { } ship)
                {
                    return ship;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// For a computer player only: each ship counts by how much weaker it
    /// is than the receiver's own design in that slot, at least 0.
    /// </summary>
    internal double ShipsValue(TradeHalf half, Player receiver)
    {
        var value = 0.0;
        if (!receiver.IsAi)
        {
            return value;
        }

        foreach (var id in half.Ships)
        {
            if (FindShip(id) is { } ship && receiver.Designs[ship.Slot] is { } own)
            {
                var v = ((double)own.Strength(Tables.Tech) * 0.9 - (double)ship.Strength(Tables.Tech)) * 0.1 - -1.0;
                value = Math.Max(v, 0.0) + value;
            }
        }

        return value;
    }

    /// <summary>Five times the score of each planet named that the giver still colonizes.</summary>
    internal double PlanetsValue(TradeHalf half, Player giver)
    {
        var sum = 0.0;
        foreach (var id in half.Planets)
        {
            if (FindPlanet(id) is { Colony: { } colony } planet && colony.Owner == giver)
            {
                sum = planet.Score + sum;
            }
        }

        return sum * 5.0;
    }

    /// <summary>10 for each tech the receiver does not know.</summary>
    internal static double TechsValue(TradeHalf half, Player receiver)
    {
        var value = 0.0;
        foreach (var id in half.Techs)
        {
            if (!receiver.Knows(id))
            {
                value -= -10.0;
            }
        }

        return value;
    }

    internal static double CreditsValue(TradeHalf half)
    {
        return (double)half.Credits * 2e-05;
    }

    /// <summary>5 for each system the giver has seen and the receiver never has.</summary>
    internal double MapValue(TradeHalf half, Player giver, Player receiver)
    {
        var value = 0.0;
        if (!half.Map)
        {
            return value;
        }

        foreach (var star in Universe.Stars)
        {
            if (star.Sight[giver.Index] != Star.Unseen && star.Sight[receiver.Index] == Star.Unseen)
            {
                value -= -5.0;
            }
        }

        return value;
    }

    /// <summary>
    /// The ships go to one new fleet of the receiver's at its newest colony
    /// (else the first planet of the first system).
    /// </summary>
    private void GiveShips(TradeHalf half, Player receiver)
    {
        if (half.Ships.Count == 0)
        {
            return;
        }

        var (star, planet) = receiver.Colonies.Count > 0
            ? (Universe.Stars[receiver.Colonies[0].Star], receiver.Colonies[0].Planet)
            : (Universe.Stars[0], Universe.Stars[0].Planets.FirstOrDefault());
        if (planet is null)
        {
            // The original would make the fleet with no planet; no galaxy has a planetless first system.
            return;
        }

        var fleet = TradedFleet(receiver, star, planet);
        for (var i = 0; i < half.Ships.Count; i++)
        {
            if (FindShip(half.Ships[i]) is not { Fleet: { } from } ship)
            {
                continue;
            }

            from.RemoveShip(ship, Tables.Tech);

            // Quirk kept: a computer player's ships are deleted, so it gets an empty fleet.
            if (receiver.IsAi && !Fixed(Fixes.ComputerKeepsShips))
            {
                continue;
            }

            // Bug kept: every 21 ships another fleet is made but left empty,
            // and the first one, full, refuses the rest, which are lost.
            if (Fixed(Fixes.TradeKeepsAllShips))
            {
                // The fix: a full fleet hands over to a new one.
                if (fleet.Ships.Count >= Fleet.MaxShips)
                {
                    fleet = TradedFleet(receiver, star, planet);
                }
            }
            else if (i != 0 && i % Fleet.MaxShips == 0)
            {
                TradedFleet(receiver, star, planet);
            }

            fleet.AddShip(ship, Tables.Tech);
        }
    }

    private Fleet TradedFleet(Player receiver, Star star, Planet planet)
    {
        var fleet = CreateFleet(receiver, star, planet);
        fleet.Name = Tables.TradedFleetName;
        return fleet;
    }

    private void GivePlanets(TradeHalf half, Player giver, Player receiver, ref StepEvents events)
    {
        foreach (var id in half.Planets)
        {
            if (FindPlanet(id) is { Colony: { } colony } && colony.Owner == giver)
            {
                HandOverColony(colony, giver, receiver, ref events);
            }
        }
    }

    /// <summary>
    /// A colony changes hands: it is destroyed and founded again for the
    /// receiver, keeping its improvements' construction, its defenses
    /// (rebuilt with the receiver's best techs) and its population.
    /// </summary>
    private void HandOverColony(Colony colony, Player giver, Player receiver, ref StepEvents events)
    {
        int[] kinds = [5, 6, 7, 8, 9, 10];
        var stacks = kinds.Select(k => colony.FirstOfKind(k) is { } s ? (s.Count, s.HitPoints) : (0, 0.0)).ToArray();
        var population = colony.CurrentPopulation;
        var improvements = Tables.OfKind(TechInfo.ImprovementKind)
            .Select(t => (Tech: t, Built: colony.Improvements[ImprovementSlot(t)]?.Built))
            .Where(x => x.Built is not null)
            .ToList();

        var planet = colony.Planet;
        _handingOver = true;
        DestroyColony(colony, ref events);
        var colonized = Colonize(receiver, planet, 1, ref events);
        _handingOver = false;
        Happen(new ModEvent(ModEventKind.ColonyChangedHands, receiver, giver, planet));

        foreach (var (tech, built) in improvements)
        {
            // makes the slot if the receiver did not know the improvement.
            var slot = ImprovementSlot(tech);
            var improvement = colonized.Improvements[slot] ??= new Improvement(tech);

            // Quirk kept: the construction put in is passed as building points.
            BuildImprovement(colonized, improvement, built ?? 0.0);
        }

        for (var i = 0; i < kinds.Length; i++)
        {
            var best = PlayerCreation.BestKnown(receiver, Tables, kinds[i]);
            for (var n = 0; n < stacks[i].Item1 && best > 0; n++)
            {
                AddStructure(colonized, Tables.Tech(best));
            }

            if (colonized.FirstOfKind(kinds[i]) is { } stack)
            {
                BuildStructure(stack, stacks[i].Item2);
            }
        }

        SetColonyResource(colonized, Colony.Population, population, ref events);
    }

    private void GiveTechs(TradeHalf half, Player receiver)
    {
        foreach (var id in half.Techs)
        {
            LearnTech(receiver, Tables.Tech(id));
        }
    }

    private static void GiveCredits(TradeHalf half, Player giver, Player receiver)
    {
        if (half.Credits > 0)
        {
            giver.Money -= half.Credits;
            receiver.Money += half.Credits;
        }
    }

    private void GiveMap(TradeHalf half, Player giver, Player receiver)
    {
        if (!half.Map)
        {
            return;
        }

        foreach (var star in Universe.Stars)
        {
            if (star.Sight[giver.Index] != Star.Unseen && star.Sight[receiver.Index] == Star.Unseen)
            {
                UpdateSight(star, receiver);
            }
        }
    }
}
