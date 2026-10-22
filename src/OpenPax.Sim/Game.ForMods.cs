using OpenPax.Sim.Galaxy;
using OpenPax.Sim.Players;

namespace OpenPax.Sim;

/// <summary>
/// The game's rules a mod may call (docs/modding.md): its public surface for
/// rules mods, built on the game's own rules rather than opening the
/// internals. A mod's code runs where the game runs (single player, or a
/// network game's server), so it stays deterministic as long as it uses the
/// game's state and these, never the clock or its own random numbers.
/// </summary>
public sealed partial class Game
{
    // The defense structures a besieger fights: missile bases, battle stations, fighter bases, mine fields.
    private static readonly int[] _defenseKinds = [7, 8, 9, 10];

    /// <summary>
    /// A mod's say over a battle about to begin: given the attacking fleet and
    /// its target (a fleet, or a planet with a colony), true takes the battle
    /// over, so none opens and the game does not stop for it. A fleet
    /// attacking a planet then circles it. Not saved: the mods set it again.
    /// </summary>
    [ModApi]
    [field: Saving.NotSaved]
    public Func<Game, Fleet, ISpaceObject, bool>? TakeOverBattle { get; set; }

    /// <summary>
    /// A mod's rating of a colony's attack and defense, which the planet
    /// lists show when set; null shows nothing, as the original. Not saved:
    /// the mods set it again.
    /// </summary>
    [ModApi]
    [field: Saving.NotSaved]
    public Func<Game, Colony, ColonyRating?>? RateColony { get; set; }

    /// <summary>
    /// A mod's chance, 0 to 1, that a fleet's attack on a fleet or a colony
    /// succeeds, which the map shows beside the pointer with ATTACK waiting;
    /// null shows nothing, as the original. Not saved: the mods set it again.
    /// </summary>
    [ModApi]
    [field: Saving.NotSaved]
    public Func<Game, Fleet, ISpaceObject, double?>? RateAttack { get; set; }

    /// <summary>Mods' own state, saved with the game and sent to network clients: one string per key, by each mod's own keys.</summary>
    [ModApi]
    public Dictionary<string, string> ModState { get; } = [];

    // The fights a mod reports, in the mod state so they reach network clients and outlast a save.
    private const string FightsKey = "openpax.fights";

    /// <summary>Whether one player is at war with another.</summary>
    [ModApi]
    public static bool AtWar(Player a, Player b) => a != b && a.Treaties[b.Index].Level() == Treaty.War;

    /// <summary>Two players at war both ways, as the original's battles make them at the first hit.</summary>
    [ModApi]
    public void DeclareWar(Player a, Player b)
    {
        if (a == b)
        {
            return;
        }

        var events = StepEvents.None;
        SetTreaty(a, b, Treaty.War, Treaty.InForce, ref events);
        SetTreaty(b, a, Treaty.War, Treaty.InForce, ref events);
        Raise(a, events);
        Raise(b, events);
    }

    /// <summary>
    /// The fights a mod is running without the battle screen, for the
    /// system view to show (a battle marker, and the health of each side).
    /// Each report replaces the last; none clears them.
    /// </summary>
    [ModApi]
    public void ReportFights(IEnumerable<ModFight> fights)
    {
        var text = string.Join(';', fights.Select(f => "f" + Id(f.Fleet.ObjectId) + ">" + (f.Target is Planet p ? "p" + Id(p.ObjectId) : "f" + Id(((Fleet)f.Target).ObjectId))));
        if (text.Length == 0)
        {
            ModState.Remove(FightsKey);
        }
        else
        {
            ModState[FightsKey] = text;
        }

        static string Id(int id) => id.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>The fights last reported in one system whose two sides are still there.</summary>
    [ModApi]
    public IReadOnlyList<ModFight> Fights(Star star)
    {
        if (!ModState.TryGetValue(FightsKey, out var text))
        {
            return [];
        }

        ISpaceObject? Find(string id)
        {
            if (id.Length < 2 || !int.TryParse(id.AsSpan(1), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var n))
            {
                return null;
            }

            return id[0] == 'p' ? star.Planets.FirstOrDefault(p => p.ObjectId == n) : star.Fleets.FirstOrDefault(f => f.ObjectId == n);
        }

        var found = new List<ModFight>();
        foreach (var pair in text.Split(';'))
        {
            var sides = pair.Split('>');
            if (sides.Length == 2 && Find(sides[0]) is Fleet attacker && attacker.Ships.Count > 0 && Find(sides[1]) is { } target)
            {
                found.Add(new ModFight(attacker, target));
            }
        }

        return found;
    }

    /// <summary>A mine field's level, its place among the mine field techs from 1: a minesweeper of that level or better sees its mines.</summary>
    [ModApi]
    public int MineFieldLevel(TechInfo field) => Tables.IndexInKind(field) + 1;

    /// <summary>A ship's attack: its damage per hit when a battle is resolved at once.</summary>
    [ModApi]
    public int ShipAttack(Design ship) => ship.AutoDamage(Tables.Tech);

    /// <summary>A ship's defense rating in battle.</summary>
    [ModApi]
    public static int ShipDefense(Design ship) => ship.CombatDefense();

    /// <summary>Damage to a ship's structure; at none the ship is lost from its fleet, which a credited player is told of. True when it is lost.</summary>
    [ModApi]
    public bool DamageShip(Design ship, int amount, Player? by = null)
    {
        if (ship.Fleet is not { } fleet || amount <= 0)
        {
            return false;
        }

        ship.Structure -= amount;
        if (ship.Structure > 0)
        {
            return false;
        }

        var owner = fleet.Owner;
        fleet.RemoveShip(ship, Tables.Tech);
        Happen(new ModEvent(ModEventKind.ShipDestroyed, owner, by, Ship: ship));
        if (by is not null)
        {
            Incident(by, owner, IncidentKind.ShipDestroyed);
        }

        return true;
    }

    /// <summary>A colony's defenses' attack: each defense structure's weapons' best damage, by its count.</summary>
    [ModApi]
    public int DefenseAttack(Colony colony) =>
        colony.Structures.Where(s => _defenseKinds.Contains(s.Tech.Kind))
            .Sum(s => s.Count * s.WeaponTechs.OfType<TechInfo>().Sum(w => w.Damage.Max()));

    /// <summary>What is left of a colony's defense structures.</summary>
    [ModApi]
    public static double DefenseHitPoints(Colony colony) =>
        colony.Structures.Where(s => _defenseKinds.Contains(s.Tech.Kind)).Sum(s => s.HitPoints);

    /// <summary>Damage to a colony's defense structures, the first stack first; a stack loses a unit for each unit's worth of hit points gone.</summary>
    [ModApi]
    public void DamageDefenses(Colony colony, double amount)
    {
        while (amount > 0 && colony.Structures.FirstOrDefault(s => _defenseKinds.Contains(s.Tech.Kind)) is { } stack)
        {
            amount = DamageStructure(stack, amount);
        }
    }

    /// <summary>
    /// Damage to one structure stack: it loses a unit for each unit's worth
    /// of hit points gone, and is gone with the last. What it could not take
    /// is given back.
    /// </summary>
    [ModApi]
    public double DamageStructure(Structure stack, double amount)
    {
        var colony = stack.Colony;
        if (amount <= 0 || !colony.Structures.Contains(stack))
        {
            return amount;
        }

        var taken = Math.Min(amount, stack.HitPoints);
        stack.HitPoints -= taken;
        var perUnit = stack.MaxHitPoints / stack.Count;
        while (stack.Count > 0 && stack.HitPoints <= perUnit * (stack.Count - 1) + 1e-9 && colony.Structures.Contains(stack))
        {
            RemoveStructureUnit(stack);
        }

        if (stack.HitPoints <= 0 && colony.Structures.Contains(stack))
        {
            RemoveStructureUnit(stack);
        }

        return amount - taken;
    }

    /// <summary>Bombardment of a colony's people: its population falls by the amount, to no less than none.</summary>
    [ModApi]
    public void Bombard(Colony colony, double amount)
    {
        var events = StepEvents.None;
        SetColonyResource(colony, Colony.Population, Math.Max(0.0, colony.CurrentPopulation - amount), ref events);
        Raise(colony.Owner, events);
    }

    /// <summary>A colony taken by another player, as a planet traded is handed over; the two are at war after.</summary>
    [ModApi]
    public void CaptureColony(Colony colony, Player taker)
    {
        var owner = colony.Owner;
        if (owner == taker)
        {
            return;
        }

        var events = StepEvents.None;
        var population = colony.CurrentPopulation;
        HandOverColony(colony, owner, taker, ref events);
        SetTreaty(taker, owner, Treaty.War, Treaty.InForce, ref events);
        SetTreaty(owner, taker, Treaty.War, Treaty.InForce, ref events);
        Incident(taker, owner, IncidentKind.ColonyTaken, population);
        Raise(owner, events);
        Raise(taker, events);
    }

    /// <summary>
    /// Tells a human player something, in the status line and the history
    /// like the game's own notices (a computer player is told nothing). In a
    /// network game it reaches the player's own machine.
    /// </summary>
    [ModApi]
    public void Tell(Player player, string text) => Notify(new GameNotice(NoticeKind.ModMessage, player, Text: text));

    /// <summary>Credits to a player, or taken from one with a negative amount.</summary>
    [ModApi]
    public static void GiveMoney(Player player, double amount) => player.Money += amount;

    /// <summary>
    /// A tech learned as if from outside research (espionage): known, taken
    /// up by the player's colonies, designs and best armor, and a research
    /// field working on it freed. Nothing when the player knows it already.
    /// </summary>
    [ModApi]
    public void TeachTech(Player player, TechInfo tech) => LearnTech(player, tech);

    /// <summary>
    /// A new fleet of the player's at a planet, with ships built to a design
    /// (one of the player's own, or any other). A human's fleet circles the
    /// planet, a computer player's sits on it, as when a shipyard makes one.
    /// At most 21 ships fit in a fleet; the rest are not made.
    /// </summary>
    [ModApi]
    public Fleet SpawnShips(Player player, Planet planet, Design design, int count)
    {
        var star = Universe.Stars.First(s => s.Planets.Contains(planet));
        var fleet = CreateFleet(player, star, planet);
        for (var i = 0; i < count && fleet.Ships.Count < Fleet.MaxShips; i++)
        {
            var ship = NewShip(design);
            ship.Owner = player;
            fleet.AddShip(ship, Tables.Tech);
        }

        return fleet;
    }

    /// <summary>A colony's population set, no lower than none; its owner hears of the change as of growth.</summary>
    [ModApi]
    public void SetPopulation(Colony colony, double population)
    {
        var events = StepEvents.None;
        SetColonyResource(colony, Colony.Population, Math.Max(0.0, population), ref events);
        Raise(colony.Owner, events);
    }

    /// <summary>Two players at peace both ways: Neutral in force, which ends a war, as an accepted peace does.</summary>
    [ModApi]
    public void MakePeace(Player a, Player b)
    {
        if (a == b)
        {
            return;
        }

        var events = StepEvents.None;
        SetTreaty(a, b, Treaty.Neutral, Treaty.InForce, ref events);
        SetTreaty(b, a, Treaty.Neutral, Treaty.InForce, ref events);
        Raise(a, events);
        Raise(b, events);
    }

    /// <summary>The highest treaty in force from one player toward another (Treaty.War to Treaty.ShareInformation), or Treaty.None when they have not met.</summary>
    [ModApi]
    public static int TreatyLevel(Player from, Player toward) => from.Treaties[toward.Index].Level();
}
