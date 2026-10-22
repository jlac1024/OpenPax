using OpenPax.Sim.Galaxy;

namespace OpenPax.Sim.Players;

/// <summary>
/// A fleet: ships, newest first, in one star system, with a
/// queue of orders, an orbit and stats from its ships. See
/// docs/sim/ships.md and docs/sim/movement.md.
/// </summary>
public sealed class Fleet(int objectId, string name, Player owner, int star) : ISpaceObject
{
    public const int MaxShips = 21;
    public const int OrbitRadius = 30;
    public const double OrbitRate = 50.0;

    // An empty fleet's stats start here and fall back to 0.
    private const double NoSpeed = 9999.0;
    private const int NoRange = 9999;

    public int ObjectId { get; } = objectId;

    public string Name { get; internal set; } = name;

    public Player Owner { get; } = owner;

    /// <summary>Index of the star system the fleet is in.</summary>
    public int Star { get; internal set; } = star;

    public int X { get; private set; }

    public int Y { get; private set; }

    /// <summary>What the fleet circles, or null when it sits still or travels.</summary>
    public ISpaceObject? OrbitCenter { get; private set; }

    public double OrbitAngle { get; internal set; }

    public double OrbitSpeed { get; private set; }

    public List<Design> Ships { get; } = [];

    public List<Order> Orders { get; } = [];

    public FleetMode Mode { get; internal set; }

    /// <summary>Ticks of ten times speed left (+0xAC).</summary>
    public int BoostTicks { get; internal set; }

    /// <summary>The flag +0xA8, cleared when the boost runs out; presumed a retreat. Arrival skips combat with a fleet that has it.</summary>
    public bool Retreating { get; internal set; }

    /// <summary>Held in combat (+0x74 set): the fleet neither checks encounters nor moves.</summary>
    public bool InCombat { get; internal set; }

    /// <summary>
    /// Set to intercept (+0xA0): toggled by the player's Interdict order. The
    /// encounter check (Game.Encounters.cs) sends such a fleet
    /// after hostile fleets in its system.
    /// </summary>
    public bool Intercepting { get; internal set; }

    /// <summary>Lowest combat speed of the ships; 0 for none.</summary>
    public double Speed { get; private set; }

    /// <summary>Lowest star drive range of the ships.</summary>
    public int JumpRange { get; private set; }

    /// <summary>Highest jump count since supply among the ships.</summary>
    public int JumpsUsed { get; private set; }

    public int Strength { get; private set; }

    public int JumpsLeft => Math.Max(0, JumpRange - JumpsUsed);

    internal void MoveTo(int x, int y)
    {
        X = x;
        Y = y;
    }

    /// <summary>
    /// Starts circling an object (or sitting still with no center) at the
    /// fleet radius: a new angle and direction are drawn either way.
    /// </summary>
    internal void EnterOrbit(GameRandom random, ISpaceObject? center)
    {
        var orbit = Orbit.Draw(random, 0, 0, OrbitRadius, OrbitRate);
        OrbitAngle = orbit.Angle;
        OrbitSpeed = orbit.Speed;
        OrbitCenter = center;
    }

    /// <summary>The orbit as an original save holds it: center, angle and speed, no draw.</summary>
    internal void RestoreOrbit(ISpaceObject? center, double angle, double speed)
    {
        OrbitCenter = center;
        OrbitAngle = angle;
        OrbitSpeed = speed;
    }

    /// <summary>A new fleet split from this one takes its place: its orbit, else its position.</summary>
    internal void TakePlaceOf(Fleet other)
    {
        if (other.OrbitCenter is { } center)
        {
            RestoreOrbit(center, other.OrbitAngle, other.OrbitSpeed);
        }
        else
        {
            MoveTo(other.X, other.Y);
        }
    }

    internal void LeaveOrbit()
    {
        OrbitCenter = null;
    }

    /// <summary>One step around the center, which may itself have moved; nothing without one.</summary>
    internal void OrbitStep()
    {
        if (OrbitCenter is not { } center)
        {
            return;
        }

        OrbitAngle = Orbit.Wrap(OrbitAngle + OrbitSpeed);
        (X, Y) = Orbit.Place(center.X, center.Y, OrbitRadius, OrbitAngle);
    }

    /// <summary>Adds a ship at the front; refused once the fleet has 21.</summary>
    internal bool AddShip(Design ship, Func<int, TechInfo> techById)
    {
        if (Ships.Count > MaxShips - 1)
        {
            return false;
        }

        Ships.Insert(0, ship);
        ship.Fleet = this;
        if (Owner.Species.Is(Species.Warrior))
        {
            while (ship.ExperienceLevel < 2)
            {
                ship.AddExperience(2);
            }
        }

        UpdateStats(techById);
        Owner.Upkeep += Upkeep(ship);
        return true;
    }

    internal void RemoveShip(Design ship, Func<int, TechInfo> techById)
    {
        if (!Ships.Remove(ship))
        {
            return;
        }

        ship.Fleet = null;
        UpdateStats(techById);
        Owner.Upkeep -= Upkeep(ship);
    }

    /// <summary>Speed, jump range, jumps used and strength from the ships, newest first.</summary>
    internal void UpdateStats(Func<int, TechInfo> techById)
    {
        Speed = NoSpeed;
        Strength = 0;
        JumpsUsed = 0;
        JumpRange = NoRange;
        foreach (var ship in Ships)
        {
            Strength += ship.Strength(techById);
            var speed = ship.CombatSpeed();
            if (!(Speed <= speed))
            {
                Speed = speed;
            }

            JumpRange = Math.Min(JumpRange, ship.DriveRange());
            JumpsUsed = Math.Max(JumpsUsed, ship.JumpsSinceSupply);
        }

        if (Speed == NoSpeed)
        {
            Speed = 0.0;
        }

        if (JumpRange == NoRange)
        {
            JumpRange = 0;
        }
    }

    /// <summary>A ship's cost, times 0.8 for nomads.</summary>
    private double Upkeep(Design ship)
    {
        var cost = (double)ship.Cost;
        return Owner.Species.Is(Species.Nomad) ? cost * 0.8 : cost;
    }

    public void AddTo(StateHash hash)
    {
        hash.Add(ObjectId).Add(Name).Add(Star).Add(X).Add(Y).Add(OrbitAngle).Add(OrbitSpeed);
        hash.Add(OrbitCenter is null ? 0 : 1).Add((int)Mode).Add(BoostTicks).Add(Retreating ? 1 : 0).Add(InCombat ? 1 : 0).Add(Intercepting ? 1 : 0);
        hash.Add(Speed).Add(JumpRange).Add(JumpsUsed).Add(Strength);
        hash.Add(Orders.Count);
        foreach (var o in Orders)
        {
            var (x, y) = o.Position;
            hash.Add(x).Add(y);
        }

        hash.Add(Ships.Count);
        foreach (var s in Ships)
        {
            s.AddTo(hash);
        }
    }
}
