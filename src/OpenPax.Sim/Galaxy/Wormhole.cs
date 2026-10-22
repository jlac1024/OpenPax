namespace OpenPax.Sim.Galaxy;

/// <summary>A wormhole, one end of a link between two stars.</summary>
public sealed class Wormhole : ISpaceObject
{
    public Wormhole(int objectId, int animationFrame, int star)
    {
        ObjectId = objectId;
        AnimationFrame = animationFrame;
        Star = star;
    }

    public int ObjectId { get; }

    /// <summary>Starting animation frame, drawn from the game stream (+0x44).</summary>
    public int AnimationFrame { get; }

    /// <summary>Index of the star this end sits at.</summary>
    public int Star { get; }

    /// <summary>The other end, or null if never linked.</summary>
    public Wormhole? Partner { get; internal set; }

    public string Name { get; internal set; } = "";

    /// <summary>Position in the star system view.</summary>
    public int X { get; internal set; }

    public int Y { get; internal set; }

    /// <summary>Set once a fleet has jumped through either end (+0x8C).</summary>
    public bool Known { get; internal set; }
}
