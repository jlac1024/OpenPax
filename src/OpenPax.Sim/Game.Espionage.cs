using OpenPax.Sim.Galaxy;
using OpenPax.Sim.Players;

namespace OpenPax.Sim;

/// <summary>Espionage missions: paying for them and trying them (docs/sim/espionage.md).</summary>
public sealed partial class Game
{
    /// <summary>Starts a mission at the back of the owner's list.</summary>
    public SpyMission AddMission(Player owner, Player target, int kind, int level)
    {
        var mission = new SpyMission(NextMissionId++, owner, target, kind, level);
        owner.Missions.Add(mission);
        return mission;
    }

    /// <summary>Drops a mission and what was put into it.</summary>
    public void CancelMission(Player owner, int missionId)
    {
        owner.Missions.RemoveAll(m => m.Id == missionId);
    }

    private void RunEspionageTask(ref StepEvents events)
    {
        foreach (var player in Players)
        {
            SpendEspionage(player, ref events);
        }
    }

    /// <summary>
    /// The period's espionage points go into the missions in order; each
    /// one paid for is tried and goes.
    /// </summary>
    internal void SpendEspionage(Player p, ref StepEvents events)
    {
        var points = p.EspionagePoints;
        foreach (var mission in p.Missions.ToList())
        {
            if (!(0.0 < points))
            {
                break;
            }

            var owed = mission.Cost - mission.Progress;
            if (points <= owed)
            {
                mission.Progress = points + mission.Progress;
                points = 0.0;
            }
            else
            {
                // Quirk kept: the whole cost is added, not what was owed. It
                // only matters to the progress shown, and the mission ends now.
                points -= owed;
                mission.Progress = mission.Cost + mission.Progress;
            }

            if (mission.Paid)
            {
                TryMission(mission, ref events);
                p.Missions.Remove(mission);
            }
        }
    }

    /// <summary>
    /// One roll to get in (the mission's chance, doubled for spy species)
    /// and one against the target's defense (ten times its espionage per
    /// head, doubled for psychics). A spy who fails to get in and is seen
    /// is an incident.
    /// </summary>
    // forced  passes both rolls, which are still drawn.
    private void TryMission(SpyMission m, ref StepEvents events, bool forced = false)
    {
        var (owner, target) = (m.Owner, m.Target);
        var chance = m.Chance;
        if (owner.Species.Is(Species.Spy))
        {
            chance += chance;
        }

        var defense = target.EspionagePoints * 10.0;
        if (target.Species.Has(Species.Psychic))
        {
            defense += defense;
        }

        Raise(owner, StepEvents.EspionageReport);
        var kind = m.Kind;
        if ((double)(Random.Next(100) + 1) <= chance * 100.0 || forced)
        {
            if (defense < (double)(Random.Next(100) + 1) || forced)
            {
                var done = Carry(m);
                Notify(done != 0
                    ? new GameNotice(NoticeKind.SpySucceeded, owner, kind, target, Count: done)
                    : new GameNotice(NoticeKind.SpyFailed, owner, kind, target, Count: 0));
                if (done != 0 && m.Kind is not (SpyMission.TurnAdvisors or SpyMission.TurnGovernors or SpyMission.RevealSystems or SpyMission.StealTechs))
                {
                    // The target is told what happened, and by whom unless
                    // this roll beats its defense.
                    var named = (double)(Random.Next(100) + 1) <= defense;
                    Notify(new GameNotice(NoticeKind.SpyDamage, target, kind, owner, Count: done, Named: named));
                    Raise(target, StepEvents.EspionageReport);
                }
            }
            else
            {
                Notify(new GameNotice(NoticeKind.SpyFailed, owner, kind, target, Count: 1));
                Notify(new GameNotice(NoticeKind.SpyStopped, target, kind, owner));
                Raise(target, StepEvents.EspionageReport);
            }
        }
        else if ((double)(Random.Next(100) + 1) <= defense)
        {
            Notify(new GameNotice(NoticeKind.SpyFailed, owner, kind, target, Count: 1));
            Notify(new GameNotice(NoticeKind.SpyStopped, target, kind, owner));
            Raise(target, StepEvents.EspionageReport);
            Incident(owner, target, IncidentKind.SpyCaught);
        }
        else
        {
            Notify(new GameNotice(NoticeKind.SpyFailed, owner, kind, target, Count: 2));
        }

    }

    /// <summary>What a mission that got through does; how many things it hit (0 means nothing).</summary>
    private int Carry(SpyMission m)
    {
        return m.Kind switch
        {
            SpyMission.StealShips => StealShips(m.Owner, m.Target),
            SpyMission.DestroyShips => DestroyShips(m.Target),
            SpyMission.Sabotage => Sabotage(m.Target),
            SpyMission.LocateColony => PickColony(m.Target) is null ? 0 : 1,
            SpyMission.TurnAdvisors => TurnAdvisors(m.Owner, m.Target, false),
            SpyMission.TurnGovernors => TurnAdvisors(m.Owner, m.Target, true),
            SpyMission.Assassinate => Assassinate(m.Target),
            SpyMission.RevealSystems => RevealSystems(m.Owner, m.Target),
            _ => StealTechs(m.Owner, m.Target),
        };
    }

    /// <summary>
    /// Each ship at 10% goes to a new fleet of the spy's at its first
    /// colony (else the first planet of the first system).
    /// </summary>
    private int StealShips(Player owner, Player target)
    {
        var count = 0;
        foreach (var fleet in target.Fleets.ToList())
        {
            foreach (var ship in fleet.Ships.ToList())
            {
                if (Random.Next(100) + 1 >= 11)
                {
                    continue;
                }

                count++;
                fleet.RemoveShip(ship, Tables.Tech);
                var (star, planet) = owner.Colonies.Count > 0
                    ? (Universe.Stars[owner.Colonies[0].Star], owner.Colonies[0].Planet)
                    : (Universe.Stars[0], Universe.Stars[0].Planets.FirstOrDefault());
                if (planet is null)
                {
                    // The original would make the fleet with no planet; no galaxy has a planetless first system.
                    continue;
                }

                var taken = CreateFleet(owner, star, planet);
                taken.Name = Tables.StolenFleetName;

                // Quirk kept: a computer player's stolen ship is deleted,
                // leaving the new fleet empty for the star step to drop.
                if (!owner.IsAi || Fixed(Fixes.ComputerKeepsShips))
                {
                    taken.AddShip(ship, Tables.Tech);
                }
            }
        }

        return count;
    }

    /// <summary>Each ship at 10% is destroyed.</summary>
    private int DestroyShips(Player target)
    {
        var count = 0;
        foreach (var fleet in target.Fleets.ToList())
        {
            foreach (var ship in fleet.Ships.ToList())
            {
                if (Random.Next(100) + 1 < 11)
                {
                    count++;
                    fleet.RemoveShip(ship, Tables.Tech);
                }
            }
        }

        return count;
    }

    /// <summary>A colony picked at random; each structure stack loses a unit and each improvement is wiped out, at 50%.</summary>
    private int Sabotage(Player target)
    {
        if (PickColony(target) is not { } colony)
        {
            return 0;
        }

        var count = 0;
        foreach (var structure in colony.Structures.ToList())
        {
            if (Random.Next(100) + 1 < 51)
            {
                count++;
                RemoveStructureUnit(structure);
            }
        }

        // Any improvement with a slot counts, built or not.
        foreach (var tech in Tables.OfKind(TechInfo.ImprovementKind))
        {
            if (colony.Improvements[ImprovementSlot(tech)] is { } improvement && Random.Next(100) + 1 < 51)
            {
                count++;
                DamageImprovement(colony, improvement, 99999999.0);
            }
        }

        return count;
    }

    /// <summary>Walks the colonies round and round, taking each at 15%, until one is taken.</summary>
    private Colony? PickColony(Player target)
    {
        if (target.Colonies.Count == 0)
        {
            return null;
        }

        for (var i = 0; ; i = (i + 1) % target.Colonies.Count)
        {
            if (Random.Next(100) + 1 < 16)
            {
                return target.Colonies[i];
            }
        }
    }

    /// <summary>Field advisors at 25% (or governors at 15%) not already the spy's are turned.</summary>
    private int TurnAdvisors(Player owner, Player target, bool governors)
    {
        var count = 0;
        foreach (var a in target.Advisors)
        {
            if (a.IsGovernor == governors && a.TurnedBy != owner && Random.Next(100) + 1 < (governors ? 16 : 26))
            {
                a.TurnedBy = owner;
                count++;
            }
        }

        return count;
    }

    /// <summary>Each field advisor at 25% is killed; nobody takes the place.</summary>
    private int Assassinate(Player target)
    {
        var count = 0;
        foreach (var a in target.Advisors.ToList())
        {
            if (!a.IsGovernor && Random.Next(100) + 1 < 26)
            {
                target.Advisors.Remove(a);
                count++;
            }
        }

        return count;
    }

    /// <summary>Each system the target has seen and the spy never has, at 50%, is shown to the spy.</summary>
    private int RevealSystems(Player owner, Player target)
    {
        var count = 0;
        foreach (var star in Universe.Stars)
        {
            if (star.Sight[owner.Index] == Star.Unseen && star.Sight[target.Index] != Star.Unseen && Random.Next(100) + 1 < 51)
            {
                UpdateSight(star, owner);
                count++;
            }
        }

        return count;
    }

    /// <summary>Each tech the target knows and the spy does not, at 15%, is learned.</summary>
    private int StealTechs(Player owner, Player target)
    {
        var count = 0;
        for (var id = 0; id < 900; id++)
        {
            if (target.Knows(id) && !owner.Knows(id) && Random.Next(100) + 1 < 16)
            {
                LearnTech(owner, Tables.Tech(id));
                count++;
            }
        }

        return count;
    }
}
