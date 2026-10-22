using OpenPax.Sim.Galaxy;

namespace OpenPax.Sim.Combat;

/// <summary>
/// A battle (docs/sim/combat.md): each player's side, its
/// ships and orbitals, the planet when there is one, and a grid that keeps
/// things from being placed on top of each other.
/// </summary>
public sealed class Battle
{
    public const int Players = 16;
    public const int GridSize = 180;
    public const int CellSize = 20;

    public Battle(int star)
    {
        Star = star;
        for (var p = 0; p < Players; p++)
        {
            Ships[p] = [];
            Orbitals[p] = [];
            Shots[p] = [];
            Missiles[p] = [];
            Fighters[p] = [];
            Mines[p] = [];
        }
    }

    public int Star { get; }

    public Commander?[] Commanders { get; } = new Commander?[Players];

    /// <summary>Ships per player, newest first (+0x4C).</summary>
    public List<BattleShip>[] Ships { get; } = new List<BattleShip>[Players];

    /// <summary>Orbitals per player, newest first (+0x8C).</summary>
    public List<Orbital>[] Orbitals { get; } = new List<Orbital>[Players];

    /// <summary>Shots of each player that do not home, newest first (+0x10C).</summary>
    public List<Shot>[] Shots { get; } = new List<Shot>[Players];

    /// <summary>Missiles of each player, newest first (+0x18C).</summary>
    public List<Shot>[] Missiles { get; } = new List<Shot>[Players];

    /// <summary>Fighters of each player, docked or out, newest first (+0xCC).</summary>
    public List<Fighter>[] Fighters { get; } = new List<Fighter>[Players];

    /// <summary>Mines of each player, newest first (+0x14C).</summary>
    public List<Mine>[] Mines { get; } = new List<Mine>[Players];

    /// <summary>Shots in flight (+0x1FC2C), kept count of and never read.</summary>
    internal int LiveShots { get; set; }

    /// <summary>Ships that left the field, newest first (+0x1FC24).</summary>
    public List<BattleShip> Graveyard { get; } = [];

    /// <summary>The planet fought over, or null in a fleet battle.</summary>
    public Planet? Planet { get; internal set; }

    /// <summary>Its improvement bands (+0x70 of the combat planet); -1 when the colony counts as absent.</summary>
    public int Bands { get; internal set; } = -1;

    /// <summary>The planet's blocks, highest index first.</summary>
    public List<PlanetBlock> Blocks { get; } = [];

    /// <summary>
    /// Explosions and flashes on the two effect lists (+0x1D0, +0x1D4),
    /// newest first. Which list holds one only matters for drawing.
    /// </summary>
    public List<Effect> OrbitalEffects { get; } = [];

    public List<Effect> ShipEffects { get; } = [];

    /// <summary>Players with a human side (+0x1FC3C): any means the battle is fought out step by step.</summary>
    public int Humans { get; internal set; }

    public int Steps { get; internal set; }

    /// <summary>The battle's number (+0x1FC34), which orders name it by.</summary>
    public int Id { get; internal set; }

    /// <summary>Where the step is in its cycle of 20 (+0x1FC38): sides think in 0 to 15, sensors look every 10, weapons fire every 5.</summary>
    public int SubStep { get; internal set; }

    /// <summary>The step of the last damaging hit (+0x1FC44) and of the last shot (+0x1FC28).</summary>
    public int LastHit { get; internal set; }

    public int LastShot { get; internal set; }

    /// <summary>The local side fights by itself (+0x1FC4B, AUTO).</summary>
    public bool AutoEngaged { get; internal set; }

    /// <summary>
    /// The field in 20 unit cells, by x * 180 + y (+0x1E4): 0 free, -1
    /// covered, and in a footprint's corner cell the id of what stands there.
    /// Marks are not counted: clearing one footprint also clears the cells
    /// of anything overlapping it, as in the original.
    /// </summary>
    internal int[] Grid { get; } = new int[(GridSize + 1) * GridSize + GridSize + 1];

    internal bool MarkerOrbits { get; set; }

    /// <summary>
    /// Where the battle's marker stands in its system (the CombatMarker,
    /// ): the first fleet's position as it joined. Strategic time
    /// stands still during a battle, so it never moves.
    /// </summary>
    public (int X, int Y)? Marker { get; internal set; }

    /// <summary>The cells a footprint covers: half = (size + 1) / 2 around the point.</summary>
    internal static (int X0, int Y0, int X1, int Y1) Footprint(int x, int y, int half)
    {
        var x0 = x - half;
        var y0 = y - half;
        var x1 = x + half;
        var y1 = y + half;
        if (x0 % CellSize > 9)
        {
            x0 += 10;
        }

        if (y0 % CellSize > 9)
        {
            y0 += 10;
        }

        if (x1 % CellSize < 11)
        {
            x1 -= 10;
        }

        if (y1 % CellSize < 11)
        {
            y1 -= 10;
        }

        return (Math.Clamp(x0 / CellSize, 0, GridSize), Math.Clamp(y0 / CellSize, 0, GridSize),
            Math.Clamp(x1 / CellSize, 0, GridSize), Math.Clamp(y1 / CellSize, 0, GridSize));
    }

    /// <summary>Marks a footprint: its cells covered, its corner holding the id.</summary>
    internal void MarkFootprint(int x, int y, int half, int id)
    {
        var (x0, y0, x1, y1) = Footprint(x, y, half);
        Fill(x0, y0, x1, y1, -1);
        Grid[x0 * GridSize + y0] = id;
    }

    /// <summary>Frees a footprint's cells, whatever else covered them.</summary>
    internal void ClearFootprint(int x, int y, int half)
    {
        var (x0, y0, x1, y1) = Footprint(x, y, half);
        Fill(x0, y0, x1, y1, 0);
    }

    /// <summary>How many cells of a footprint are taken.</summary>
    internal int Taken(int x, int y, int half)
    {
        var (x0, y0, x1, y1) = Footprint(x, y, half);
        var count = 0;
        for (var cx = x0; cx <= x1; cx++)
        {
            for (var cy = y0; cy <= y1; cy++)
            {
                if (Grid[cx * GridSize + cy] != 0)
                {
                    count++;
                }
            }
        }

        return count;
    }

    private void Fill(int x0, int y0, int x1, int y1, int value)
    {
        for (var cx = x0; cx <= x1; cx++)
        {
            for (var cy = y0; cy <= y1; cy++)
            {
                Grid[cx * GridSize + cy] = value;
            }
        }
    }

    /// <summary>A grid cell: 0 free, -1 covered, or the id of what stands with its footprint's corner there.</summary>
    public int Cell(int cx, int cy) => Grid[cx * GridSize + cy];

    internal bool Free(int cx, int cy)
    {
        var i = cx * GridSize + cy;
        return i < 0 || i >= Grid.Length || Grid[i] == 0;
    }

    /// <summary>The planet as an object of the battle (+0x1E0), or null in a fleet battle.</summary>
    public CombatPlanet? PlanetObject { get; internal set; }
}
