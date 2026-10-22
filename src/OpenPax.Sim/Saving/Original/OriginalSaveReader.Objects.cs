using OpenPax.Sim.Galaxy;
using OpenPax.Sim.Players;

namespace OpenPax.Sim.Saving.Original;

/// <summary>Colonies, fleets, ships and the smaller objects a player holds.</summary>
internal sealed partial class OriginalSaveReader
{
    private readonly List<Player> _players = [];

    private Player PlayerAt(int index) => _players.First(p => p.Index == index);

    private TechInfo? TechOrNone(int id) => id > 0 && id <= _tables.Techs.Count ? _tables.Tech(id) : null;

    /// <summary>A trade package offered to the player; no end marker.</summary>
    private void ReadPackage(Player target)
    {
        var proposer = _in.Int32();
        var offered = ReadHalf();
        var targetIndex = _in.Int32();
        var asked = ReadHalf();
        _relinks.Add(() =>
        {
            var package = new TradePackage(PlayerAt(proposer), PlayerAt(targetIndex));
            Copy(offered, package.Offered);
            Copy(asked, package.Asked);
            target.Packages.Add(package);
        });
    }

    private TradeHalf ReadHalf()
    {
        var half = new TradeHalf();
        foreach (var list in new[] { half.Planets, half.Techs, half.Ships })
        {
            var count = _in.Int32();
            for (var i = 0; i < count; i++)
            {
                list.Add(_in.Int32());
            }
        }

        half.Map = _in.Bool();
        half.Credits = _in.Int32();
        return half;
    }

    private static void Copy(TradeHalf from, TradeHalf to)
    {
        to.Planets.AddRange(from.Planets);
        to.Techs.AddRange(from.Techs);
        to.Ships.AddRange(from.Ships);
        to.Map = from.Map;
        to.Credits = from.Credits;
    }

    private Advisor ReadAdvisor()
    {
        var skills = new[] { _in.Double(), _in.Double(), _in.Double(), _in.Double() };
        var star = _in.Int32();
        var role = _in.Int32();
        var portrait = _in.Int32();
        var turnedBy = _in.Int32();
        var id = _in.Int32();
        _in.End();
        var advisor = new Advisor(id, role, portrait, skills) { Star = star < 0 ? Advisor.NoStar : star };
        if (turnedBy >= 0)
        {
            _relinks.Add(() => advisor.TurnedBy = PlayerAt(turnedBy));
        }

        return advisor;
    }

    private Colony ReadColony(Player owner)
    {
        var maxPopulation = _in.Int32();
        var espionageUpkeep = _in.Double();
        var researchUpkeep = _in.Double();
        var constructionUpkeep = _in.Double();
        var structureUpkeep = _in.Double();
        var growth = _in.Double();
        double[] Five() => [_in.Double(), _in.Double(), _in.Double(), _in.Double(), _in.Double()];
        var resources = Five();
        var perPopulation = Five();
        var flat = Five();

        var improvements = new List<(int Slot, Improvement Improvement)>();
        var count = _in.Int32();
        for (var i = 0; i < count; i++)
        {
            var slot = _in.Int32();
            var built = _in.Double();
            _in.Double();
            var tech = _in.Int32();
            _in.Int32();
            _in.End();
            improvements.Add((slot, new Improvement(_tables.Tech(tech)) { Built = built }));
        }

        var funding = _in.Double();
        var points = _in.Double();
        var plan = _in.Int32();
        var planet = (Planet)(Find(_in.Int32()) ?? throw new InvalidDataException(Strings.SaveNotOurs));
        var colony = new Colony(owner, planet, _planetStars[planet.ObjectId])
        {
            MaxPopulation = maxPopulation,
            StructureUpkeep = structureUpkeep,
            Growth = growth,
            Funding = funding,
            ConstructionPoints = points,
            Plan = plan,
        };
        colony.ImprovementUpkeep[2] = espionageUpkeep;
        colony.ImprovementUpkeep[1] = researchUpkeep;
        colony.ImprovementUpkeep[3] = constructionUpkeep;
        resources.CopyTo(colony.Resources, 0);
        perPopulation.CopyTo(colony.PerPopulation, 0);
        flat.CopyTo(colony.Flat, 0);
        foreach (var (slot, improvement) in improvements)
        {
            colony.Improvements[slot] = improvement;
        }

        var structures = _in.Int32();
        for (var i = 0; i < structures; i++)
        {
            colony.Structures.Add(ReadStructure(colony));
        }

        _in.End();
        planet.Colony = colony;
        planet.Owner = owner;
        return colony;
    }

    private Structure ReadStructure(Colony colony)
    {
        var ints = new int[7];
        for (var i = 0; i < ints.Length; i++)
        {
            ints[i] = _in.Int32();
        }

        _in.Byte();
        var hitPoints = _in.Double();
        _in.Double();
        var damage = new[] { _in.Double(), _in.Double(), _in.Double() };
        var parts = new TechInfo?[9];
        for (var i = 0; i < parts.Length; i++)
        {
            parts[i] = TechOrNone(_in.Int32());
        }

        var count = _in.Int32();
        var tech = TechOrNone(_in.Int32()) ?? throw new InvalidDataException(Strings.SaveNotOurs);
        _in.Int32();
        var structure = new Structure(tech, colony)
        {
            Count = count,
            HitPoints = hitPoints,
            ShieldAbsorbed = ints[2],
            ShieldTech = parts[0],
            EcmTech = parts[1],
            SensorTech = parts[2],
            SecondEnergyTech = parts[8],
            Progress = _in.Double(),
            Total = _in.Double(),
            UnderWork = _in.Bool(),
        };
        damage.CopyTo(structure.SubsystemDamage, 0);
        structure.FighterTechs[0] = parts[3];
        structure.FighterTechs[1] = parts[4];
        structure.FighterTechs[2] = parts[5];
        structure.WeaponTechs[0] = parts[6];
        structure.WeaponTechs[1] = parts[7];

        var queue = _in.Int32();
        for (var i = 0; i < queue; i++)
        {
            structure.Queue.Add(ReadDesign(colony.Owner));
        }

        _in.End();
        return structure;
    }

    /// <summary>
    /// A fleet. Quirk kept: a retreat and a speed boost are not
    /// saved, so they end on load. Its stats come back from its ships.
    /// </summary>
    private Fleet ReadFleet(Player owner)
    {
        var o = ReadSpaceObject();
        var ships = new List<Design>();
        var count = _in.Int32();
        for (var i = 0; i < count; i++)
        {
            ships.Add(ReadDesign(owner));
        }

        var orders = new List<(int Target, int X, int Y)>();
        var orderCount = _in.Int32();
        for (var i = 0; i < orderCount; i++)
        {
            var target = _in.Int32();
            var x = _in.Int32();
            var y = _in.Int32();
            _in.End();
            _in.End();
            orders.Add((target, x, y));
        }

        _in.Int32();
        var intercepting = _in.Bool();
        var mode = _in.Int32();
        var star = _in.Int32();
        _in.Double();
        _in.Int32();
        _in.Int32();
        _in.Int32();
        _in.End();

        var fleet = new Fleet(o.Id, o.Name, owner, star) { Intercepting = intercepting, Mode = (FleetMode)mode };
        fleet.MoveTo(o.X, o.Y);
        foreach (var ship in ships)
        {
            fleet.Ships.Add(ship);
            ship.Fleet = fleet;
        }

        fleet.UpdateStats(_tables.Tech);
        _objects[o.Id] = fleet;
        _relinks.Add(() =>
        {
            fleet.RestoreOrbit(Find(o.CenterId), o.Angle, o.Speed);
            foreach (var (target, x, y) in orders)
            {
                fleet.Orders.Add(Find(target) is { } t ? Order.To(t) : Order.ToPoint(x, y));
            }
        });
        return fleet;
    }

    /// <summary>A design or ship, with a nested refit when one is marked.</summary>
    private Design ReadDesign(Player owner)
    {
        _in.Byte();
        var name = _in.String();
        var className = _in.String();
        var quality = _in.Int32();
        var level = _in.Byte();
        var points = _in.Int32();
        _in.Int32();
        var structure = _in.Int32();
        _in.Int32();
        _in.Int32();
        _in.Int32();
        var shipId = _in.Int32();
        var slot = _in.Int32();
        var id = _in.Int32();
        var jumps = _in.Int32();
        _in.Byte();
        _in.Int32();
        _in.Int32();
        _in.Int32();
        var hull = _in.Int32();

        var design = new Design(_tables.Hulls[hull])
        {
            Owner = owner,
            Name = name,
            ClassName = className,
            ShipId = shipId,
            Slot = slot,
            Id = id,
            JumpsSinceSupply = jumps,
        };
        design.SetQuality(quality);
        var components = _in.Int32();
        for (var i = 0; i < components; i++)
        {
            var tech = TechOrNone(_in.Int32());
            var damage = _in.Int32();
            var mode = _in.Int32();
            var count = _in.Int32();
            _in.End();

            // Quirk kept: the built count is not saved and comes back as the constructor's 1.
            if (tech is not null)
            {
                design.AddLoaded(new Component(tech, mode) { Count = count, Damage = damage });
            }
        }

        if (_in.Int32() != -1)
        {
            design.Refit = ReadDesign(owner);
        }

        _in.End();
        design.RecalculateKeeping(structure);
        design.RestoreExperience(level, points);
        return design;
    }
}
