using System.Text;
using OpenPax.Sim;
using OpenPax.Sim.Galaxy;
using OpenPax.Sim.Players;

namespace OpenPax.Ai.Tests;

/// <summary>
/// Writes a port game in the original's save layout, field by field from
/// the study (build/scratch-ui/save-format.md), so the loader can be tested
/// without a save made by the original. Fields the port does not keep are
/// written as zeros.
/// </summary>
internal sealed class OriginalSaveWriter(Stream stream)
{
    private const int End = 0x7CD;

    private readonly BinaryWriter _out = new(stream, Encoding.Latin1, leaveOpen: true);

    public void Write(Game game, int currentStar, int selectionId)
    {
        _out.Write((short)0x4949);
        I(unchecked((int)0x5078476D));
        I(0x430);
        _out.Write((byte)1);
        I(game.SecondaryIds.Next);
        I(0);
        I((int)game.Setup.Seed);
        I(game.Setup.Difficulty);
        I(game.Calendar.Year);
        _out.Write((byte)game.Calendar.Step);
        I(End);

        I(game.Universe.Stars.Count);
        I(game.Universe.Stars.Count);
        foreach (var star in game.Universe.Stars)
        {
            Star(star);
        }

        I(currentStar);
        I(selectionId);
        I(End);
        _out.Write((byte)0);
        _out.Write((byte)(game.Over ? 1 : 0));
        I(game.Players.Count);
        foreach (var p in game.Players)
        {
            I(p.IsAi ? 0x3DD : 0x382);
            Player(game, p);
            I(End);
        }

        _out.Write((byte)0);
        I(End);
        I(game.ObjectIds.Next);
        I(End);
        _out.Flush();
    }

    private void I(int v) => _out.Write(v);

    private void D(double v) => _out.Write(v);

    private void S(string s)
    {
        var bytes = Encoding.Latin1.GetBytes(s);
        _out.Write((short)bytes.Length);
        _out.Write(bytes);
    }

    private void SpaceObject(int x, int y, string name, int id, int centerId, int star, int radius, double angle, double speed)
    {
        I(x);
        I(y);
        for (var i = 0; i < 4; i++)
        {
            I(0);
        }

        S(name);
        I(id);
        D(0);
        D(0);
        I(0);
        I(centerId);
        I(star);
        I(radius);
        D(angle);
        D(speed);
        I(End);
    }

    private static int IdOf(ISpaceObject? o, Game game) => o switch
    {
        null => -1,
        Planet p => p.ObjectId,
        Fleet f => f.ObjectId,
        Wormhole w => w.ObjectId,
        StarCenter c => game.Universe.Stars[c.Star].ObjectId,
        _ => -1,
    };

    private void Star(Star star)
    {
        for (var i = 0; i < 4; i++)
        {
            I(0);
        }

        I(star.X);
        I(star.Y);
        SpaceObject(220, 220, star.Name, star.ObjectId, -1, star.Index, 0, 0, 0);
        I(star.Type);
        I(End);
        I(star.Planets.Count);
        foreach (var p in star.Planets)
        {
            SpaceObject(p.X, p.Y, p.Name, p.ObjectId, star.ObjectId, star.Index, p.Orbit.Radius, p.Orbit.Angle, p.Orbit.Speed);
            I(p.Temperature);
            I(p.Class);
            I(p.Moons);
            I(p.Size);
            I(p.Type);
            I(p.Rating);
            D(p.Score);
            I(p.Slot + 1);
            foreach (var s in p.Satellites)
            {
                I(s.Radius);
                D(s.Speed);
            }

            I(End);
        }

        I(star.Wormholes.Count);
        foreach (var w in star.Wormholes)
        {
            SpaceObject(w.X, w.Y, w.Name, w.ObjectId, -1, w.Star, 0, 0, 0);
            I(w.Partner?.ObjectId ?? -1);
            I(w.Star);
            I(End);
        }

        foreach (var sight in star.Sight)
        {
            I(sight);
        }

        I(star.Index);
        I(End);
    }

    private void Player(Game game, Player p)
    {
        S(p.Name);
        I(p.Flag);
        I(p.FleetColor);
        I(p.Index);
        for (var j = 0; j < OpenPax.Sim.Players.Player.MaxPlayers; j++)
        {
            for (var k = 0; k < Treaty.States; k++)
            {
                I(p.Treaties[j].Slot(k));
            }

            I(End);
            D(p.RelationScores[j]);
            D(p.TradeRamp[j]);
            D(p.TradeReceipts[j]);
            D(p.ResearchRamp[j]);
            D(p.ResearchReceipts[j]);
        }

        foreach (var v in p.Spending)
        {
            D(v);
        }

        foreach (var v in p.ImprovementUpkeep)
        {
            D(v);
        }

        D(p.Income);
        _out.Write((byte)(p.Eliminated ? 1 : 0));
        I(End);

        var sp = p.Species;
        I(sp.RaceId);
        S(sp.Name);
        S("description");
        I(sp.Picture);
        I(sp.PreferredTemperature);
        I(sp.TemperatureTolerance);
        foreach (var b in sp.Atmospheres.Concat(sp.PhysicalTraits).Concat(sp.CulturalTraits))
        {
            _out.Write((byte)(b ? 1 : 0));
        }

        foreach (var v in sp.RateModifiers.Concat(sp.ResearchLevels))
        {
            _out.Write(v);
        }

        foreach (var c in p.DesignCounters)
        {
            I(c);
        }

        I(p.BestArmor);
        D(p.Money);
        D(p.ResearchPoints);
        D(p.EspionagePoints);

        var plans = p.Plans.TakeWhile(x => x is not null).ToList();
        I(plans.Count);
        for (var i = 0; i < plans.Count; i++)
        {
            S(p.PlanNames[i] ?? "");
            var ids = plans[i]!; // TakeWhile stopped at the first empty slot
            for (var k = 0; k < 50; k++)
            {
                I(k < ids.Length ? ids[k] : 0);
            }

            I(ids.Length);
            I(End);
        }

        I(End);

        I(p.ResearchSlots.Length);
        foreach (var slot in p.ResearchSlots)
        {
            I(slot.Tech);
            I(slot.Cost);
            I(slot.Progress);
            D(slot.Fraction);
            I(End);
        }

        I(game.Tables.Techs.Count + 1);
        for (var id = 0; id <= game.Tables.Techs.Count; id++)
        {
            I(p.SavedProgress.GetValueOrDefault(id));
        }

        var bits = new byte[113];
        foreach (var id in p.KnownTechs.Where(id => id < 900))
        {
            bits[id >> 3] |= (byte)(1 << (id & 7));
        }

        _out.Write(bits);
        foreach (var r in p.Research)
        {
            I(r);
        }

        I(p.KnownCount);
        I(p.ResearchTotal);
        _out.Write((byte)(p.AutoResearch ? 1 : 0));
        I(End);

        I(p.Missions.Count);
        foreach (var m in p.Missions)
        {
            I(m.Target.Index);
            I(m.Owner.Index);
            I(m.Kind);
            D(m.Cost);
            D(m.Progress);
            I(m.Level);
            I(m.Id);
            I(End);
        }

        I(game.NextMissionId);
        I(End);
        I(p.FleetCounter);
        D(p.Upkeep);
        foreach (var v in p.StructureUpkeep)
        {
            D(v);
        }

        D(p.Support);
        D(p.Funding);
        for (var sample = 0; sample < EmpireHistory.Samples; sample++)
        {
            for (var s = 0; s < 10; s++)
            {
                I(p.History.Series(s)[sample]);
            }
        }

        I(p.Packages.Count);
        foreach (var k in p.Packages)
        {
            I(k.Proposer.Index);
            Half(k.Offered);
            I(k.Target.Index);
            Half(k.Asked);
        }

        I(p.Advisors.Count);
        foreach (var a in p.Advisors)
        {
            foreach (var s in a.Skills)
            {
                D(s);
            }

            I(a.Star);
            I(a.Role);
            I(a.Portrait);
            I(a.TurnedBy?.Index ?? -1);
            I(a.Id);
            I(End);
        }

        I(p.Colonies.Count);
        foreach (var c in p.Colonies)
        {
            Colony(c);
        }

        I(p.Fleets.Count);
        foreach (var f in p.Fleets)
        {
            Fleet(game, f);
        }

        var designs = p.Designs.Select((d, i) => (d, i)).Where(x => x.d is not null).ToList();
        I(designs.Count);
        foreach (var (d, i) in designs)
        {
            I(i);
            Design(d!); // filtered on not null just above
        }

        I(-1);

        // Interface settings: 5 bytes, 11 ints, a byte, 4 ints, 16 bytes, end.
        _out.Write(new byte[5]);
        for (var i = 0; i < 11; i++)
        {
            I(0);
        }

        _out.Write((byte)0);
        for (var i = 0; i < 4; i++)
        {
            I(0);
        }

        _out.Write(new byte[16]);
        I(End);
        _out.Write((byte)0);
        D(p.Caution);
        foreach (var r in p.Relations)
        {
            I(r);
        }

        I(End);
    }

    private void Half(TradeHalf h)
    {
        foreach (var list in new[] { h.Planets, h.Techs, h.Ships })
        {
            I(list.Count);
            foreach (var v in list)
            {
                I(v);
            }
        }

        _out.Write((byte)(h.Map ? 1 : 0));
        I(h.Credits);
    }

    private void Colony(Colony c)
    {
        I(c.MaxPopulation);
        D(c.ImprovementUpkeep[2]);
        D(c.ImprovementUpkeep[1]);
        D(c.ImprovementUpkeep[3]);
        D(c.StructureUpkeep);
        D(c.Growth);
        foreach (var v in c.Resources.Concat(c.PerPopulation).Concat(c.Flat))
        {
            D(v);
        }

        var improvements = c.Improvements.Select((x, i) => (x, i)).Where(x => x.x is not null).ToList();
        I(improvements.Count);
        foreach (var (x, i) in improvements)
        {
            I(i);
            D(x!.Built); // filtered on not null just above
            D(x.Total);
            I(x.Tech.Id);
            I(c.Planet.ObjectId);
            I(End);
        }

        D(c.Funding);
        D(c.ConstructionPoints);
        I(c.Plan);
        I(c.Planet.ObjectId);
        I(c.Structures.Count);
        foreach (var s in c.Structures)
        {
            I(0);
            I(0);
            I((int)s.ShieldAbsorbed);
            for (var i = 0; i < 4; i++)
            {
                I(0);
            }

            _out.Write((byte)0);
            D(s.HitPoints);
            D(s.MaxHitPoints);
            foreach (var v in s.SubsystemDamage)
            {
                D(v);
            }

            foreach (var t in new[] { s.ShieldTech, s.EcmTech, s.SensorTech, s.FighterTechs[0], s.FighterTechs[1], s.FighterTechs[2], s.WeaponTechs[0], s.WeaponTechs[1], s.SecondEnergyTech })
            {
                I(t?.Id ?? -1);
            }

            I(s.Count);
            I(s.Tech.Id);
            I(c.Planet.ObjectId);
            D(s.Progress);
            D(s.Total);
            _out.Write((byte)(s.UnderWork ? 1 : 0));
            I(s.Queue.Count);
            foreach (var d in s.Queue)
            {
                Design(d);
            }

            I(End);
        }

        I(End);
    }

    private void Fleet(Game game, Fleet f)
    {
        SpaceObject(f.X, f.Y, f.Name, f.ObjectId, IdOf(f.OrbitCenter, game), f.Star, 30, f.OrbitAngle, f.OrbitSpeed);
        I(f.Ships.Count);
        foreach (var s in f.Ships)
        {
            Design(s);
        }

        I(f.Orders.Count);
        foreach (var o in f.Orders)
        {
            I(IdOf(o.Target, game));
            var (x, y) = o.Position;
            I(x);
            I(y);
            I(End);
            I(End);
        }

        I(f.Star);
        _out.Write((byte)(f.Intercepting ? 1 : 0));
        I((int)f.Mode);
        I(f.Star);
        D(f.Speed);
        I(f.Strength);
        I(f.JumpsUsed);
        I(f.JumpRange);
        I(End);
    }

    private void Design(Design d)
    {
        _out.Write((byte)0);
        S(d.Name);
        S(d.ClassName);
        I(d.Quality);
        _out.Write((byte)d.ExperienceLevel);
        I(d.ExperiencePoints);
        I(d.MaxStructure);
        I(d.Structure);
        I(0);
        I(0);
        I(d.Cost);
        I(d.ShipId);
        I(d.Slot);
        I(d.Id);
        I(d.JumpsSinceSupply);
        _out.Write((byte)0);
        I(0);
        I(d.UsedSpace);
        I(d.RawSpace);
        I(d.Type);
        var parts = d.Components.ToList();
        I(parts.Count);
        foreach (var c in parts)
        {
            I(c.Tech.Id);
            I(c.Damage);
            I(c.Mode);
            I(c.Count);
            I(End);
        }

        if (d.Refit is { } refit && refit.Refit != d)
        {
            I(refit.Slot);
            Design(refit);
        }
        else
        {
            I(-1);
        }

        I(End);
    }
}
