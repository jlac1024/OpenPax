using OpenPax.Sim.Players;

namespace OpenPax.Sim.Galaxy;

/// <summary>A star system (the original's sector, 0x100 bytes).</summary>
public sealed class Star
{
    private StarCenter? _center;

    /// <summary>Sight level: never seen.</summary>
    public const int Unseen = 0;

    /// <summary>Sight level: seen before, not in view now.</summary>
    public const int Remembered = 1;

    /// <summary>Sight level: in view now.</summary>
    public const int InView = 2;

    public Star(int index, int objectId, int x, int y, int type, string name)
    {
        Index = index;
        ObjectId = objectId;
        X = x;
        Y = y;
        Type = type;
        Name = name;
    }

    public int Index { get; }

    /// <summary>Id of the star's own object (the original numbers stars first).</summary>
    public int ObjectId { get; }

    /// <summary>Position on the galaxy map, 20 to 430.</summary>
    public int X { get; }

    public int Y { get; }

    /// <summary>Star type 0 to 6.</summary>
    public int Type { get; }

    public string Name { get; }

    /// <summary>Planets from the innermost orbit out.</summary>
    public List<Planet> Planets { get; } = [];

    public List<Wormhole> Wormholes { get; } = [];

    /// <summary>Fleets in the system, newest first (+0xF8).</summary>
    public List<Fleet> Fleets { get; } = [];

    /// <summary>The star's own object at the center of its system, a fleet target.</summary>
    public StarCenter Center => _center ??= new StarCenter(Index);

    /// <summary>What each player sees of the system (+0xB8), by player index; see docs/sim/visibility.md.</summary>
    public int[] Sight { get; } = new int[Player.MaxPlayers];
}
