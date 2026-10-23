using OpenPax.Sim;
using OpenPax.Sim.Players;

namespace SimpleCombat;

/// <summary>
/// A round's sums (docs/modding.md). The original's rolls become their
/// expected values, so a round draws nothing from the game's random
/// numbers: beams hit at attack minus hull defense plus 50 in 100, missiles
/// at tracking minus ECM (and fighters' missile defense) plus 100, never
/// below 5; point defense, short range guns and drones thin out missiles
/// and drones; shields cut beams and drones, never missiles.
/// </summary>
public static class Fight
{
    /// <summary>One side's fire at another, after hit chances and interception, before shields.</summary>
    public static Volley Fire(Firepower from, Firepower at, double smallCraftDefense, Settings settings)
    {
        var beams = from.Beam * Chance(from.BeamAim - at.HullDefense + 50);
        var missiles = from.Missile * Chance(from.MissileAim - at.MissileScreen - at.Ecm + 100);
        var drones = from.Drones * Chance(from.DroneAim - at.HullDefense + 50);

        // Point defense and the rest share themselves between missiles and drones by how many come.
        var small = missiles + drones;
        if (small > 0 && smallCraftDefense > 0)
        {
            var onMissiles = smallCraftDefense * missiles / small;
            var onDrones = (smallCraftDefense - onMissiles) * (1 - (Math.Clamp(at.DroneDodge, 0, 95) / 100));
            missiles *= 1 - (missiles > 0 ? Math.Min(settings.MostStopped, onMissiles / missiles) : 0);
            drones *= 1 - (drones > 0 ? Math.Min(settings.MostStopped, onDrones / drones) : 0);
        }

        return new Volley(beams, missiles, drones, from.Bombs).Times(settings.AttackShare);
    }

    /// <summary>A hit's chance in 100, as the original's roll: at least 5.</summary>
    private static double Chance(double chance) => Math.Clamp(chance, 5, 100) / 100;

    /// <summary>
    /// Fire onto ships, the weakest first: each ship's shield cuts the beams
    /// and drones by its percent, then soaks up to its capacity, both by its
    /// condition. What is left once every ship is gone is given back; bombs
    /// pass over ships.
    /// </summary>
    public static Volley OnShips(Game game, IEnumerable<Design> ships, Volley v, Player by) => OnShips(game, ships, v, by, out _);

    /// <summary>
    /// As <see cref="OnShips(Game, IEnumerable{Design}, Volley, Player)"/>, with
    /// the kill credit: a tenth of each ship destroyed, as the original's.
    /// The fire is shared over the ships, the bigger drawing more of it, so a
    /// fleet wears down together; what a ship destroyed could not take goes
    /// on to the rest, the weakest first. Each ship's share meets its own
    /// shield, and some of it lands on its components (Wear).
    /// </summary>
    public static Volley OnShips(Game game, IEnumerable<Design> ships, Volley v, Player by, out int credit)
    {
        credit = 0;
        var hit = ships.OrderBy(s => s.ShipId).ToList();
        var size = hit.Sum(s => (double)Math.Max(1, s.MaxStructure));
        if (hit.Count == 0)
        {
            // No ships: all of it goes on (to the orbitals, at a siege).
            return v;
        }

        if (v.Total < 1)
        {
            return v with { Beam = 0, Missile = 0, Drones = 0 };
        }

        var over = new Volley(0, 0, 0, 0);
        foreach (var ship in hit)
        {
            over += Land(game, ship, v.Times(Math.Max(1, ship.MaxStructure) / size) with { Bombs = 0 }, by, ref credit);
        }

        foreach (var ship in hit.Where(s => s.Structure > 0 && s.Fleet is not null).OrderBy(s => s.Structure).ThenBy(s => s.ShipId))
        {
            if (over.Total < 1)
            {
                break;
            }

            over = Land(game, ship, over, by, ref credit);
        }

        return over.Total < 1 ? v with { Beam = 0, Missile = 0, Drones = 0 } : over with { Bombs = v.Bombs };
    }

    private const string RandomId = "simple-combat";

    // Fire on one ship: through its shield, then shared between its components
    // and its hull as the original's hits would be (ComponentHitChance); what
    // was more than the ship could take comes back.
    private static Volley Land(Game game, Design ship, Volley v, Player by, ref int credit)
    {
        var (percent, capacity) = ship.Shield is { } shield
            ? (shield.Tech.ShieldPercent * shield.Condition / 100.0, shield.Tech.ShieldCapacity * shield.Condition / 100.0)
            : (0.0, 0.0);
        var rest = Strike(v, percent, capacity, ship.Structure, out var dealt);

        // The parts' share is often a fraction of a point a round: the
        // fraction lands now and then, by the mod's own random draws (kept
        // with the game, so a replay draws the same), averaging out right.
        var share = dealt * Game.ComponentHitChance(ship);
        var onParts = (int)share + (new OpenPax.Modding.ModRandom(game, RandomId).NextDouble() < share - Math.Floor(share) ? 1 : 0);
        var onHull = (int)Math.Ceiling(dealt) - onParts + Game.DamageComponents(ship, onParts);
        if (game.DamageShip(ship, onHull, by))
        {
            credit += ship.MaxStructure / 10;
        }

        return rest;
    }

    /// <summary>Fire onto structures in turn, through each one's shield as onto ships; what is left is given back.</summary>
    public static Volley OnStructures(Game game, IEnumerable<Structure> stacks, Volley v) => OnStructures(game, stacks, v, out _);

    /// <summary>As <see cref="OnStructures(Game, IEnumerable{Structure}, Volley)"/>, with the kill credit: a tenth of each unit's hit points destroyed.</summary>
    public static Volley OnStructures(Game game, IEnumerable<Structure> stacks, Volley v, out int credit)
    {
        credit = 0;
        foreach (var stack in stacks.ToList())
        {
            if (v.Total < 1)
            {
                return v with { Beam = 0, Missile = 0, Drones = 0 };
            }

            var (percent, capacity) = stack.ShieldTech is { } shield ? (shield.ShieldPercent * 1.0, shield.ShieldCapacity * 1.0) : (0.0, 0.0);
            v = Strike(v, percent, capacity, stack.HitPoints, out var dealt);
            var units = stack.Colony.Structures.Contains(stack) ? stack.Count : 0;
            game.DamageStructure(stack, dealt);
            var after = stack.Colony.Structures.Contains(stack) ? stack.Count : 0;
            credit += (units - after) * stack.Tech.HitPoints / 10;
        }

        return v;
    }

    /// <summary>
    /// Experience for the ships of a side that fought a round, as the
    /// original gives every ship left at the end of a battle: the kill
    /// credit shared out over them, plus a point each. It raises their aim
    /// and hull defense (Design.CombatAttack, HullDefense) level by level.
    /// </summary>
    public static void Experience(IEnumerable<Fleet> fleets, int credit, Settings settings)
    {
        var ships = fleets.SelectMany(f => f.Ships).ToList();
        foreach (var ship in ships)
        {
            ship.AddExperience((credit / ships.Count) + settings.ExperiencePerRound);
        }
    }

    /// <summary>What one target takes of a volley, up to what it can take, and what passes on to the next.</summary>
    private static Volley Strike(Volley v, double percent, double capacity, double canTake, out double dealt)
    {
        var shielded = v.Beam + v.Drones;
        var through = Math.Max(0, (shielded * (1 - (Math.Clamp(percent, 0, 100) / 100))) - capacity);
        var landing = through + v.Missile;
        if (landing <= canTake)
        {
            dealt = landing;
            return v with { Beam = 0, Missile = 0, Drones = 0 };
        }

        // Killed: the share of the volley it took to do it is spent, the rest goes on.
        dealt = canTake;
        return v.Times(1 - (canTake / landing)) with { Bombs = v.Bombs };
    }
}
