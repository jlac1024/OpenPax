namespace OpenPax.Sim.Galaxy;

/// <summary>Anything with a position in a star system view: planets, wormholes, fleets.</summary>
public interface ISpaceObject
{
    int X { get; }

    int Y { get; }
}
