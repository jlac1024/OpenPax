using OpenPax.Sim.Numerics;

namespace OpenPax.Sim.Players;

/// <summary>
/// A ship design, or a ship: the original uses one class for both
/// (copy). A ship is a copy of a design with its
/// own <see cref="ShipId"/>. See docs/sim/ships.md.
/// </summary>
public sealed class Design
{
    public const int SingleSlots = 6;
    public const int ListSlots = 3;

    private const int StarDriveSlot = 0;
    private const int TacticalDriveSlot = 1;
    private const int ArmorSlot = 4;
    private const int ShieldSlot = 5;

    private static readonly double[] _qualityFactors = [0.5, 0.75, 1.0, 1.5, 2.0, 2.0, 1.75, 1.5];
    private static readonly int[] _hullDefense = [20, 10, -40, 0, -20, -30];
    private static readonly int[] _experienceDefense = [-20, -10, 0, 10, 15, 20, 30];
    private static readonly int[] _experienceSteps = [100, 1000, 1500, 2500, 4000, 6000];

    private readonly Component?[] _single = new Component?[SingleSlots];
    private readonly List<Component>[] _lists = [[], [], []];

    /// <summary>A blank design on a hull; no components.</summary>
    public Design(HullInfo hull)
    {
        Hull = hull;
        Recalculate();
    }

    public HullInfo Hull { get; private set; }

    public int Type => Hull.Type;

    /// <summary>The design's name; the Ship Design window names a design while it is made.</summary>
    public string Name { get; set; } = "";

    /// <summary>A ship's group in battle, 0 for none, 1 to 9 (+0x54): the battle window selects and sets groups by number keys.</summary>
    public int CombatGroup { get; set; }

    /// <summary>Class name and number, given when the design goes in a slot.</summary>
    public string ClassName { get; internal set; } = "";

    /// <summary>Design id from the second id counter, given when it goes in a slot; ships keep their design's. -1 for none.</summary>
    public int Id { get; internal set; } = -1;

    /// <summary>A ship's own id; -1 for a design.</summary>
    public int ShipId { get; internal set; } = -1;

    public int Slot { get; internal set; } = -1;

    /// <summary>Index into the structure factors; the builder sets 0 for type 2, else 4. Meaning not yet certain.</summary>
    public int Quality { get; private set; }

    public Player? Owner { get; internal set; }

    public Fleet? Fleet { get; internal set; }

    public int ExperiencePoints { get; private set; }

    public int ExperienceLevel { get; private set; }

    /// <summary>Experience as an original save holds it (+0x24 level, +0x28 points).</summary>
    internal void RestoreExperience(int level, int points)
    {
        ExperienceLevel = level;
        ExperiencePoints = points;
    }

    /// <summary>
    /// A component as an original save holds it: appended, never merged
    /// , with its own count and damage. The caller recalculates.
    /// </summary>
    internal void AddLoaded(Component component)
    {
        var slot = component.Tech.DesignSlot;
        if (slot < SingleSlots)
        {
            _single[slot] = component;
        }
        else
        {
            _lists[slot - SingleSlots].Add(component);
        }
    }

    /// <summary>Cost, space and structure from the components, keeping the structure left.</summary>
    internal void RecalculateKeeping(int structure)
    {
        Recalculate();
        Structure = structure;
    }

    /// <summary>
    /// A ship: the new design it is to be refitted to (+0x8C). On the
    /// refitted copy in a shipyard queue: the ship it replaces.
    /// </summary>
    public Design? Refit { get; internal set; }

    /// <summary>Wormhole jumps since the ship was last supplied (+0x4C).</summary>
    public int JumpsSinceSupply { get; internal set; }

    public int MaxStructure { get; private set; }

    public int Structure { get; internal set; }

    public int Cost { get; private set; }

    public int UsedSpace { get; private set; }

    public int RawSpace { get; private set; }

    public Component? StarDrive => _single[StarDriveSlot];

    public Component? TacticalDrive => _single[TacticalDriveSlot];

    public Component? Shield => _single[ShieldSlot];

    public Component? Targeting => _single[SensorSlot];

    public Component? Ecm => _single[EcmSlot];

    public Component? Armor => _single[ArmorSlot];

    /// <summary>A single slot's component by slot number: 0 star drive to 5 shield.</summary>
    public Component? SingleSlot(int slot) => _single[slot];

    /// <summary>Weapons in list order.</summary>
    public IReadOnlyList<Component> Weapons => _lists[0];

    /// <summary>Fighters (and planetary improvements, which share the slot).</summary>
    public IReadOnlyList<Component> Fighters => _lists[1];

    /// <summary>Specials such as colonizers and repair systems (slot 8).</summary>
    public IReadOnlyList<Component> Specials => _lists[2];

    /// <summary>Every component in slot order, lists in their order.</summary>
    public IEnumerable<Component> Components => _single.OfType<Component>().Concat(_lists.SelectMany(l => l));

    /// <summary>Fits one of a tech: replaces a single slot, or adds to a list entry of the same tech and mode.</summary>
    public void Add(TechInfo tech, int mode)
    {
        var slot = tech.DesignSlot;
        if (slot < SingleSlots)
        {
            _single[slot] = new Component(tech, mode);
        }
        else if (_lists[slot - SingleSlots].FirstOrDefault(c => c.Tech.Id == tech.Id && c.Mode == mode) is { } same)
        {
            same.Count++;
        }
        else
        {
            _lists[slot - SingleSlots].Add(new Component(tech, mode));
        }

        Recalculate();
    }

    /// <summary>
    /// Takes one unit of a tech fitted with a mode away; the entry goes at
    /// none. A single slot matches whatever mode it holds.
    /// </summary>
    public void Remove(TechInfo tech, int mode)
    {
        var slot = tech.DesignSlot;
        if (slot < SingleSlots)
        {
            if (_single[slot] is { } single && single.Tech.Id == tech.Id && single.Mode == mode)
            {
                single.Count--;
                if (single.Count <= 0)
                {
                    _single[slot] = null;
                }
            }
        }
        else if (_lists[slot - SingleSlots].FirstOrDefault(c => c.Tech.Id == tech.Id && c.Mode == mode) is { } entry)
        {
            entry.Count--;
            if (entry.Count <= 0)
            {
                _lists[slot - SingleSlots].Remove(entry);
            }
        }

        Recalculate();
    }

    /// <summary>
    /// The damage a firing arc (mode 0 bow, 1 port, 2 starboard, 3 point
    /// defense) deals at a range band, from weapons other than planet bombs,
    /// times their condition in whole hundreds (so a damaged weapon counts
    /// nothing).
    /// </summary>
    public int Firepower(int mode, int band)
    {
        var sum = 0.0;
        foreach (var w in Weapons)
        {
            if (w.Mode == mode && w.Tech.WeaponType != 1)
            {
                sum += (double)(w.Condition / 100) * w.Tech.Damage[band] * w.Count;
            }
        }

        return X87.ToInt(sum);
    }

    /// <summary>An arc has firepower at some of its first three bands, or carries a bomb or area weapon.</summary>
    public bool ArcArmed(int mode)
    {
        return Math.Max(Firepower(mode, 0), Math.Max(Firepower(mode, 1), Firepower(mode, 2))) > 0
            || Weapons.Any(w => w.Mode == mode && w.Tech.WeaponType != 0);
    }

    /// <summary>Upkeep a year: the design's cost, times 0.8 for nomads.</summary>
    public double Upkeep()
    {
        var cost = (double)Cost;
        return Owner?.Species.Is(Species.Nomad) == true ? cost * 0.8 : cost;
    }

    public void SetHull(HullInfo hull)
    {
        Hull = hull;
        Recalculate();
    }

    public void SetQuality(int quality)
    {
        Quality = quality;
        Recalculate();
    }

    /// <summary>A copy for a new ship: same components, no experience, no fleet.</summary>
    public Design Copy()
    {
        var copy = new Design(Hull)
        {
            Name = Name,
            ClassName = ClassName,
            Id = Id,
            Slot = Slot,
            Quality = Quality,
            Owner = Owner,
        };
        for (var i = 0; i < SingleSlots; i++)
        {
            copy._single[i] = _single[i]?.Copy();
        }

        for (var i = 0; i < ListSlots; i++)
        {
            copy._lists[i].AddRange(_lists[i].Select(c => c.Copy()));
        }

        copy.Recalculate();
        return copy;
    }

    /// <summary>Cost, used and raw space, and structure from the components.</summary>
    internal void Recalculate()
    {
        Cost = 0;
        UsedSpace = 0;
        RawSpace = 0;
        foreach (var c in Components)
        {
            Cost += TechLevels.Cost(c.Tech, Owner) * c.Count;
            UsedSpace += TechLevels.Size(c.Tech, Owner) * c.Count;
            RawSpace += TechLevels.Size(c.Tech, null) * c.Count;
        }

        var factor = _qualityFactors[Quality];
        if (_single[ArmorSlot] is { } armor)
        {
            factor *= armor.Tech.StructureMultiplier;
        }

        MaxStructure = X87.ToInt(Hull.Structure * factor + 0.5);
        Structure = MaxStructure;
    }

    /// <summary>What building the ship costs: the hull's cost times the quality factor, plus every component's build cost.</summary>
    public int BuildCost()
    {
        var cost = X87.ToInt(Hull.Cost * _qualityFactors[Quality]);
        foreach (var c in Components)
        {
            cost += TechLevels.BuildCost(c.Tech, Owner) * c.Count;
        }

        return cost;
    }

    /// <summary>Structure left in percent, integers; 0 for a hull with no structure.</summary>
    public int ConditionPercent()
    {
        return MaxStructure < 1 ? 0 : Structure * 100 / MaxStructure;
    }

    /// <summary>A refitted ship keeps the old one's experience and id.</summary>
    internal void TakeOver(Design old)
    {
        ExperienceLevel = old.ExperienceLevel;
        ExperiencePoints = old.ExperiencePoints;
        ShipId = old.ShipId;
    }

    /// <summary>Experience points plus a fifth for warriors give levels 0 to 6.</summary>
    public void AddExperience(int points)
    {
        ExperiencePoints += points;
        var value = ExperiencePoints;
        if (Owner?.Species.Is(Species.Warrior) == true)
        {
            value += (int)(ExperiencePoints * 0.2);
        }

        ExperienceLevel = _experienceSteps.Count(step => value > step);
    }

    /// <summary>
    /// The tactical drive's combat speed while undamaged (any damage gives
    /// 0: condition / 100 is an integer, a quirk kept), times 1.2 for aquatic species.
    /// </summary>
    public double CombatSpeed()
    {
        var speed = 0.0;
        if (TacticalDrive is { } drive)
        {
            speed = (double)(drive.Condition / 100) * drive.Tech.CombatSpeed;
        }

        if (Owner?.Species.Has(Species.Aquatic) == true)
        {
            speed = speed * 0.2 + speed;
        }

        return speed;
    }

    /// <summary>Wormhole jumps the star drive allows away from supply, scaled by its condition.</summary>
    public int DriveRange()
    {
        if (StarDrive is not { } drive || Type > drive.Tech.MaxHullClass)
        {
            return 0;
        }

        return X87.ToInt((double)drive.Condition * drive.Tech.DriveRating * 0.01);
    }

    private const int SensorSlot = 2;
    private const int EcmSlot = 3;

    private static readonly int[] _combatExperience = [-20, -10, 0, 10, 15, 20, 30];

    /// <summary>Set while the ship is in a battle (+4).</summary>
    public bool InBattle { get; internal set; }

    /// <summary>Shield damage soaked (+0x34), cleared at a battle's start.</summary>
    public int ShieldSoaked { get; internal set; }

    /// <summary>Condition percent when its last battle began (+0x38).</summary>
    public int BattleCondition { get; internal set; }

    /// <summary>Readies the ship for battle: shields clear, weapons rearmed.</summary>
    internal void EnterBattle()
    {
        InBattle = true;
        ShieldSoaked = 0;
        BattleCondition = ConditionPercent();
        // every weapon loaded with its full count.
        foreach (var w in Weapons)
        {
            w.VolleyLeft = w.Count;
            w.ReloadLeft = 0;
        }
    }

    /// <summary>Sensor accuracy (undamaged share) plus experience; predators a quarter more.</summary>
    public int CombatAttack()
    {
        var a = _single[SensorSlot] is { } sensor ? sensor.Tech.SensorAccuracy * sensor.Condition / 100 : 0;
        a += _combatExperience[ExperienceLevel];
        if (Owner?.Species.Has(Species.Predator) == true)
        {
            a += a / 4;
        }

        return a;
    }

    /// <summary>The average of hull defense and ECM strength.</summary>
    public int CombatDefense()
    {
        return (HullDefense() + EcmRating()) / 2;
    }

    /// <summary>Hull, experience and drive defense, a tenth more for aquatic species: what beams must beat.</summary>
    public int HullDefense()
    {
        var d = _hullDefense[Type] + _experienceDefense[ExperienceLevel];
        if (TacticalDrive is { } drive)
        {
            d += drive.Tech.DefenseBonus * drive.Condition / 100;
        }

        if (Owner?.Species.Has(Species.Aquatic) == true)
        {
            d += d / 10;
        }

        return d;
    }

    /// <summary>ECM strength by its condition: what missiles must beat.</summary>
    public int EcmRating()
    {
        return _single[EcmSlot] is { } ecm ? ecm.Tech.EcmStrength * ecm.Condition / 100 : 0;
    }

    /// <summary>
    /// A beam's damage through the shield: cut by its percent,
    /// truncated (unlike an orbital's), then soaked up to its capacity, both
    /// by the shield's condition.
    /// </summary>
    internal int ShieldAbsorb(int d)
    {
        if (Shield is not { } shield)
        {
            return d;
        }

        var f = shield.Condition * 0.01;
        if (f == 0.0)
        {
            return d;
        }

        var cap = X87.ToInt(shield.Tech.ShieldCapacity * f);
        var p = shield.Tech.ShieldPercent * f;
        if (p != 0.0)
        {
            d = X87.ToInt((1.0 - p * 0.01) * d);
        }

        if (ShieldSoaked < cap)
        {
            var take = Math.Min(d, cap - ShieldSoaked);
            d -= take;
            ShieldSoaked += take;
        }

        return d;
    }

    /// <summary>The shield regains what its regeneration allows each step.</summary>
    internal void ShieldRegain()
    {
        if (Shield is { } shield)
        {
            var g = X87.ToInt(shield.Tech.ShieldRegeneration * (double)shield.Condition * 0.01);
            ShieldSoaked -= Math.Min(ShieldSoaked, g);
        }
    }

    /// <summary>
    /// How much a hit can strike a component: 30 in 1000 times
    /// the reciprocal of condition times armor class times the armor's
    /// multiplier, as the threshold a roll of 1000 must not pass.
    /// </summary>
    internal int ComponentHitThreshold()
    {
        var q = _qualityFactors[Quality];
        if (_single[ArmorSlot] is { } armor)
        {
            q *= armor.Tech.StructureMultiplier;
        }

        var s = ConditionPercent() * 0.01 * q;
        if (s <= 0.01)
        {
            s = 0.01;
        }

        return X87.ToInt(30.0 / s - -0.5);
    }

    /// <summary>
    /// Damage per hit when a battle is resolved at once: for weapon modes 0
    /// to 2 the best of three range bands (undamaged weapons, no bombs),
    /// plus fighters, over 4.
    /// </summary>
    public int AutoDamage(Func<int, TechInfo> techById)
    {
        var weapons = 0;
        for (var mode = 0; mode < 3; mode++)
        {
            var best = 0;
            for (var band = 0; band < 3; band++)
            {
                var sum = 0.0;
                foreach (var w in Weapons)
                {
                    if (w.Mode == mode && w.Tech.WeaponType != 1)
                    {
                        sum += (double)(w.Condition / 100) * w.Tech.Damage[band] * w.Count;
                    }
                }

                best = Math.Max(best, (int)sum);
            }

            weapons += best;
        }

        var fighters = 0;
        foreach (var f in Fighters)
        {
            if (f.Tech.Kind != TechInfo.FighterKind)
            {
                continue;
            }

            var w = 0;
            foreach (var id in (int[])[f.Tech.FighterWeapon1, f.Tech.FighterWeapon2])
            {
                if (id != 0)
                {
                    w += techById(id).Damage.Sum();
                }
            }

            fighters += f.Operational * w * 3;
        }

        return (weapons + fighters) / 4;
    }

    /// <summary>Whether a weapon can strike planets (a planet bomb or an area weapon).</summary>
    public bool CanAttackPlanets => Weapons.Any(w => w.Tech.WeaponType != 0);

    /// <summary>
    /// A hit lands on a component picked by space: sensors, ECM, shield,
    /// weapons and specials only. Returns what it could not take (all of it
    /// when nothing is picked); a battle resolved at once loses that.
    /// </summary>
    internal int HitComponent(GameRandom random, int amount)
    {
        var u = random.Next(RawSpace);
        var nodes = new List<Component>();
        foreach (var slot in (int[])[SensorSlot, EcmSlot, ShieldSlot])
        {
            if (_single[slot] is { } c)
            {
                nodes.Add(c);
            }
        }

        nodes.AddRange(_lists[0]);
        nodes.AddRange(_lists[2]);
        foreach (var c in nodes)
        {
            u -= TechLevels.Size(c.Tech, null) * c.Count;
            if (u < 1)
            {
                var room = c.Count * c.Tech.HitPoints - c.Damage;
                if (room == 0)
                {
                    return amount;
                }

                var taken = Math.Min(room, amount);
                c.Damage += taken;
                return amount - taken;
            }
        }

        return amount;
    }

    /// <summary>
    /// The port's own, for battles resolved by mods (Game.DamageComponents):
    /// damage spread over the components a hit can land on (HitComponent's
    /// sensors, ECM, shield, weapons and specials) in proportion to the space
    /// each takes, as many hits would leave it on average, each up to what it
    /// has left. Returns what none could take.
    /// </summary>
    internal int SpreadOverComponents(int amount)
    {
        var nodes = new List<Component>();
        foreach (var slot in (int[])[SensorSlot, EcmSlot, ShieldSlot])
        {
            if (_single[slot] is { } c)
            {
                nodes.Add(c);
            }
        }

        nodes.AddRange(_lists[0]);
        nodes.AddRange(_lists[2]);
        var space = nodes.Sum(c => TechLevels.Size(c.Tech, null) * c.Count);
        if (amount <= 0 || space <= 0)
        {
            return amount;
        }

        var left = amount;
        foreach (var c in nodes)
        {
            var share = (int)((long)amount * TechLevels.Size(c.Tech, null) * c.Count / space);
            var taken = Math.Min(Math.Max(0, (c.Count * c.Tech.HitPoints) - c.Damage), Math.Min(share, left));
            c.Damage += taken;
            left -= taken;
        }

        return left;
    }

    /// <summary>A strength rating used by diplomacy and computer players.</summary>
    public int Strength(Func<int, TechInfo> techById)
    {
        var defense = _hullDefense[Type] + _experienceDefense[ExperienceLevel];
        if (TacticalDrive is { } drive)
        {
            defense += drive.Condition * drive.Tech.DefenseBonus / 100;
        }

        var aquatic = Owner?.Species.Has(Species.Aquatic) == true;
        if (aquatic)
        {
            defense += defense / 10;
        }

        var armor = (defense + 100) * Structure / 100;
        if (Shield is { } shield)
        {
            var c = shield.Condition * 0.01;
            armor += X87.ToInt(shield.Tech.ShieldCapacity * c)
                + X87.ToInt((double)Structure * shield.Tech.ShieldPercent * c * 0.01);
        }

        var offense = 0;
        for (var mode = 0; mode < 4; mode++)
        {
            var sum = 0.0;
            foreach (var w in Weapons)
            {
                if (w.Mode == mode && w.Tech.WeaponType != 1)
                {
                    sum += (double)(w.Condition / 100) * w.Tech.Damage[mode] * w.Count;
                }
            }

            offense += X87.ToInt(sum);
        }

        foreach (var f in Fighters)
        {
            var weapons = 0;
            if (f.Tech.Kind == TechInfo.FighterKind)
            {
                foreach (var id in (int[])[f.Tech.FighterWeapon1, f.Tech.FighterWeapon2])
                {
                    if (id != 0)
                    {
                        weapons += techById(id).Damage.Sum();
                    }
                }
            }

            armor += f.Tech.FighterHitPoints * (f.Tech.FighterDefense + 100) / 100;
            offense += f.Operational * weapons * 6;
        }

        return X87.ToInt(Math.Sqrt(offense + armor));
    }

    public void AddTo(StateHash hash)
    {
        hash.Add(Id).Add(ShipId).Add(Slot).Add(Type).Add(Quality).Add(Name).Add(ClassName);
        hash.Add(ExperiencePoints).Add(ExperienceLevel).Add(JumpsSinceSupply).Add(InBattle ? 1 : 0).Add(ShieldSoaked).Add(BattleCondition);
        hash.Add(MaxStructure).Add(Structure).Add(Cost).Add(UsedSpace).Add(RawSpace);
        foreach (var c in Components)
        {
            c.AddTo(hash);
        }
    }
}
