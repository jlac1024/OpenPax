namespace OpenPax.Sim.Galaxy;

/// <summary>A planet as the original creates it (object 0x220 bytes, init).</summary>
public sealed class Planet : ISpaceObject
{
    public Planet(int objectId, int animationFrame, IReadOnlyList<Satellite> satellites)
    {
        ObjectId = objectId;
        AnimationFrame = animationFrame;
        Satellites = satellites;
    }

    public int ObjectId { get; }

    /// <summary>Starting frame of the planet's spin animation (+0x44).</summary>
    public int AnimationFrame { get; }

    public IReadOnlyList<Satellite> Satellites { get; }

    /// <summary>Orbit slot 0 (innermost) to 5.</summary>
    public int Slot { get; internal set; }

    public string Name { get; internal set; } = "";

    /// <summary>+0x1F4: (star type - slot + 3) * 25 at creation.</summary>
    public int Temperature { get; internal set; }

    /// <summary>+0x200: 1 to 3, or 4 (gas giant look).</summary>
    public int Type { get; internal set; }

    /// <summary>+0x1FC: 53 for type 4, else 40. Probably the drawn size.</summary>
    public int Size { get; internal set; }

    /// <summary>+0x204: 1 to 5; meaning not yet known.</summary>
    public int Rating { get; internal set; }

    /// <summary>+0x1E8: 0 to type - 1.</summary>
    public int Moons { get; internal set; }

    /// <summary>+0x1F8: atmosphere 0 to 4, or 5 for type 4. A mod's planet type has the class it looks like.</summary>
    public int Class { get; internal set; }

    /// <summary>The port's own: who breathes the planet when its type says otherwise than its class (docs/modding/content.md).</summary>
    public const int BreathingLike = 0;

    public const int BreathingAll = 1;

    public const int BreathingNone = 2;

    public const int BreathingAnaerobic = 3;

    /// <summary>The port's own: a mod's planet type, its place in GameTables.PlanetKinds plus one; 0 for the original's.</summary>
    public int Kind { get; internal set; }

    /// <summary>The port's own: who breathes it (BreathingLike, the original's rule by Class, for every original planet).</summary>
    public int Breathing { get; internal set; }

    /// <summary>+0x208: rating plus a draw per moon, kept as a double: the Wealth Rating the Domestic Policy window shows.</summary>
    public double Score { get; internal set; }

    public Orbit Orbit { get; internal set; } = new(0, 0, 0, 0, 0);

    public int X => Orbit.X;

    public int Y => Orbit.Y;

    /// <summary>Held in a battle (+0x74).</summary>
    public bool InCombat { get; internal set; }

    /// <summary>The colony on the planet, if any (+0x210).</summary>
    public Players.Colony? Colony { get; internal set; }

    /// <summary>Who owns the planet's colony.</summary>
    public Players.Player? Owner { get; internal set; }
}
